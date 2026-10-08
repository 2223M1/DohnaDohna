"""Prepare and run traceable Krea2 screening, controls and visual acceptance.

Preparing presets does not run inference. `run` requires real converted weights
and conversion receipts. Visual review is explicit; loadability is not quality.
"""
import argparse
from collections import defaultdict
from datetime import datetime, timezone
import html
from pathlib import Path
import subprocess
import sys
from dataset import ROOT, ROSTER, load, save, sha, require

MUSUBI=Path('D:/AI/ComfyUI-aki/training/tools/musubi-tuner')
MODELS=Path('D:/AI/ComfyUI-aki/training/ninja-slayer-krea2-mg-v1/models')
SEEDS=[42,271828,314159]
ACTIONS={'kuma':'leaping sideways while aiming a pistol', 'alyce':'aiming a long rifle in a balanced standing pose',
         'antena':'operating a handheld electronic device while wearing cat-ear headphones',
         'tora':'swinging a long segmented staff', 'kikuchiyo':'drawing a katana in a balanced combat stance',
         'medhico':'lifting a medical case while running', 'joker':'swinging a baseball bat',
         'zappa':'throwing a punch with chain-wrapped forearms',
         'kirakira':'running forward with a determined expression', 'porno':'pointing ahead with a confident expression'}


def prepare(root=ROOT):
    runtime={'python':str(MUSUBI/'.venv/Scripts/python.exe'), 'musubi':str(MUSUBI),
             'dit':str(MODELS/'turbo.safetensors'), 'text_encoder':str(MODELS/'qwen3vl_4b_bf16.safetensors'),
             'vae':'D:/AI/ComfyUI-aki/models/vae/qwen_image_vae.safetensors',
             'width':1024,'height':1024,'steps':8,'guidance_scale':1.0,'mu':1.15,
             'blocks_to_swap':10,'block_swap_ring_size':2,'fp8_scaled':True,'text_encoder_cpu':True}
    runtime_path=root/'evaluation/runtime.json'
    if not runtime_path.exists():save(runtime_path,runtime)
    for role in ROSTER:
        trigger=f'dohna_{role}'
        scenarios={
            'closeup':f'{trigger}, front-facing head and shoulders portrait, calm expression, plain gray background, colorful anime illustration',
            'side':f'{trigger}, side-view waist-up portrait, looking into the distance, plain gray background, colorful anime illustration',
            'fullbody':f'{trigger}, standing, complete full body including hair and shoes, relaxed pose, plain gray background, colorful anime illustration',
            'action':f'{trigger}, {ACTIONS[role]}, complete full body, dynamic composition, colorful anime illustration',
            'background':f'{trigger}, walking in a sunlit green park, complete full body, colorful anime illustration',
            'style':f'{trigger}, complete full body, textured watercolor illustration on paper, soft colors, simple background',
        }
        path=root/'evaluation'/role/'preset.json'
        if not path.exists():
            save(path,{'character':role,'trigger':trigger,'status':'WAITING_FOR_TRAINED_WEIGHTS',
                       'seeds':SEEDS,'screen_weights':[0.75,1.0],'scenarios':scenarios,
                       'checkpoints':[], 'selection':None,'style_lora':None,
                       'checkpoint_schema':{'label':'early|middle|late','step':'actual integer','path':'native .safetensors','receipt':'conversion JSON'},
                       'selection_schema':{'label':'selected checkpoint label','weight':'visually selected number'}})


def jobs(preset, stage):
    result=[];scenarios=preset['scenarios'];checkpoints=preset['checkpoints']
    if stage=='screen':
        require(len(checkpoints)==3 and len({c['step'] for c in checkpoints})==3,'screen requires three distinct real checkpoints')
        for checkpoint in checkpoints:
            for weight in preset['screen_weights']:
                for scenario in ('closeup','action'):
                    result.append({'id':f'{checkpoint["label"]}-w{weight}-{scenario}-42',
                                   'checkpoint':checkpoint,'weight':weight,'scenario':scenario,
                                   'seed':42,'prompt':scenarios[scenario],'control':None})
    else:
        require(preset['selection'] is not None,'select a screened checkpoint and weight first')
        selection=preset['selection'];checkpoint=next(c for c in checkpoints if c['label']==selection['label'])
        for scenario,prompt in scenarios.items():
            for seed in preset['seeds']:
                result.append({'id':f'{scenario}-{seed}','checkpoint':checkpoint,'weight':selection['weight'],
                               'scenario':scenario,'seed':seed,'prompt':prompt,'control':None})
        for control in ('no_lora','no_trigger'):
            for scenario in ('closeup','fullbody','action'):
                result.append({'id':f'{control}-{scenario}-42','checkpoint':None if control=='no_lora' else checkpoint,
                               'weight':selection['weight'],'scenario':scenario,'seed':42,'control':control,
                               'prompt':scenarios[scenario].replace(preset['trigger']+', ','a character, ',1) if control=='no_trigger' else scenarios[scenario]})
    return result


def command(runtime, prompt_file, output, checkpoint=None, weight=1.0):
    args=[runtime['python'],'-X','utf8',str(Path(runtime['musubi'])/'krea2_generate_image.py'),
          '--from_file',str(prompt_file),'--save_path',str(output)]
    for key in ('dit','vae','text_encoder','width','height','steps','guidance_scale','mu','blocks_to_swap','block_swap_ring_size'):
        args += ['--'+key,str(runtime[key])]
    args += ['--seed','42','--num-images','1','--device','cuda','--attn_mode','torch','--block_swap_h2d_only']
    for key in ('text_encoder_cpu','fp8_scaled'):
        if runtime[key]:args.append('--'+key)
    if checkpoint:args += ['--lora_weight',checkpoint['path'],'--lora_multiplier',str(weight)]
    return args


def run(root, role, stage):
    preset=load(root/'evaluation'/role/'preset.json');runtime=load(root/'evaluation/runtime.json')
    planned=jobs(preset,stage)
    hashes={key:sha(runtime[key]) for key in ('dit','vae','text_encoder')}
    for checkpoint in preset['checkpoints']:
        require(isinstance(checkpoint['step'],int) and checkpoint['step']>0,'step must be actual training step')
        receipt=load(checkpoint['receipt'])
        require(sha(checkpoint['path'])==receipt['output_sha256'],'converted model hash mismatch')
        require(hashes['dit']==receipt['base_sha256'],'conversion validated against a different inference base')
        require(receipt['tensor_value_verification']=='BITWISE_EQUAL','unverified conversion')
        require(f'dohna_{role}' in Path(checkpoint['path']).name and f'step{checkpoint["step"]}' in Path(checkpoint['path']).name,'model name must include character and actual step')
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    dest=root/'evaluation'/role/'runs'/f'{stage}-{stamp}';dest.mkdir(parents=True)
    record={'character':role,'stage':stage,'runtime':runtime,'model_hashes':hashes,
            'musubi_commit':subprocess.check_output(['git','-C',runtime['musubi'],'rev-parse','HEAD'],text=True).strip(),
            'preset':preset,'jobs':planned,'execution':'RUNNING','visual_quality':'NOT_REVIEWED'}
    save(dest/'run.json',record)
    groups=defaultdict(list)
    for job in planned:groups[(job['checkpoint']['label'] if job['checkpoint'] else 'no_lora',job['weight'])].append(job)
    try:
        for index,batch in enumerate(groups.values()):
            group=dest/f'batch-{index:02}';group.mkdir();out=group/'images';out.mkdir()
            prompt=group/'prompts.txt'
            prompt.write_text('\n'.join(f'{j["prompt"]} --d {j["seed"]}' for j in batch)+'\n',encoding='utf-8')
            args=command(runtime,prompt,out,batch[0]['checkpoint'],batch[0]['weight'])
            save(group/'command.json',args)
            with (group/'inference.log').open('w',encoding='utf-8') as log:
                process=subprocess.run(args,cwd=runtime['musubi'],stdout=log,stderr=subprocess.STDOUT)
            images=sorted(out.glob('*.png'))
            for job,path in zip(batch,images):
                require(path.stem.endswith('_'+str(job['seed'])),'output order/seed mismatch')
                job.update({'image':str(path),'image_sha256':sha(path)})
            save(dest/'run.json',record)
            require(process.returncode==0 and len(images)==len(batch),f'inference failed: {group}; retain log and partial outputs')
        record['execution']='PASS'
    except Exception as exc:
        record['execution']='FAILED';record['error']=str(exc)
        raise
    finally:
        save(dest/'run.json',record)
        gallery(dest,record)
    reviews=[{'id':j['id'],'identity':None,'usable':None,'reason':'',
              'features':{'hair':None,'colors':None,'outfit':None,'accessories':None,'weapon':None,'character_confusion':None}}
             for j in planned]
    save(dest/'review.json',{'images':reviews,'systematic_confusion':None,'large_scale_copying':None,
                            'controls_reviewed':False,'control_findings':'','reviewer':'','status':'NOT_REVIEWED'})
    print(dest)


def gallery(dest,record):
    cards=[]
    for job in record['jobs']:
        img='<p>未生成／失败</p>'
        if job.get('image'):
            rel=Path(job['image']).relative_to(dest).as_posix()
            img=f'<img src="{html.escape(rel,quote=True)}" loading="lazy">'
        cards.append(f'<article>{img}<b>{html.escape(job["id"])}</b><p>{html.escape(job["prompt"])}</p></article>')
    (dest/'gallery.html').write_text('<!doctype html><meta charset="utf-8"><title>Krea2 review</title><style>body{font:16px sans-serif;margin:24px;background:#eee}main{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}article{background:white;padding:12px}img{width:100%}</style><h1>'+html.escape(record['character'])+' · '+html.escape(record['execution'])+'</h1><p>生成成功不代表视觉合格。失败图和对照图均保留。</p><main>'+''.join(cards)+'</main>',encoding='utf-8')


def assess(run_path):
    record=load(run_path/'run.json');review=load(run_path/'review.json')
    require(record['stage']=='final' and record['execution']=='PASS','completed final run required')
    rows={r['id']:r for r in review['images']}
    require(len(rows)==len(review['images']) and set(rows)=={j['id'] for j in record['jobs']},'review coverage mismatch')
    for job in record['jobs']:
        r=rows[job['id']]
        require(sha(job['image'])==job['image_sha256'],'review image changed')
        require(type(r['identity']) is bool and type(r['usable']) is bool and r['reason'].strip(),'every image needs a reasoned review')
        require(all(v in ('pass','fail','not_visible','not_applicable') for v in r['features'].values()),'feature checklist incomplete')
    formal=[j for j in record['jobs'] if j['control'] is None]
    require(len(formal)==18,'formal evaluation must contain 18 images')
    identity=sum(rows[j['id']]['identity'] for j in formal)
    usable={s:sum(rows[j['id']]['identity'] and rows[j['id']]['usable'] for j in formal if j['scenario']==s) for s in ('action','style')}
    require(review['controls_reviewed'] is True and review['control_findings'].strip() and review['reviewer'].strip(),'controls and reviewer must be recorded')
    require(type(review['systematic_confusion']) is bool and type(review['large_scale_copying']) is bool,'global failure checks incomplete')
    passed=identity>=15 and all(n>=2 for n in usable.values()) and not review['systematic_confusion'] and not review['large_scale_copying']
    result={'status':'PASS' if passed else 'CANDIDATE_FAILED','identity':identity,'formal_images':18,
            'action_and_style_usable':usable,'review_sha256':sha(run_path/'review.json'),
            'run_sha256':sha(run_path/'run.json'),'style_lora_compatibility':'NOT_TESTED'}
    save(run_path/'acceptance.json',result);print(result)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('action',choices=['prepare','run','assess'])
    p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--role',choices=list(ROSTER))
    p.add_argument('--stage',choices=['screen','final']);p.add_argument('--run-path',type=Path)
    a=p.parse_args()
    if a.action=='prepare':prepare(a.root)
    elif a.action=='run':
        require(a.role and a.stage,'--role and --stage required');run(a.root,a.role,a.stage)
    else:
        require(a.run_path is not None,'--run-path required');assess(a.run_path)
