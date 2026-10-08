"""DohnaDohna character data preparation. Never modifies original assets."""
from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import html
import io
import json
import math
from datetime import datetime, timezone
from pathlib import Path
import re
import urllib.parse
import urllib.request
import zipfile

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path('D:/AI/ComfyUI-aki/training/dohnadohna-krea2-v1')
ORIGINAL = Path(__file__).resolve().parents[3] / 'DohnaDohnaOriginal'
ROSTER = dict(zip(
    ['kuma', 'alyce', 'antena', 'tora', 'kikuchiyo', 'medhico', 'joker', 'zappa', 'kirakira', 'porno'],
    ['阿熊', '爱丽丝', '安缇娜', '虎太郎', '菊千代', '梅蒂可', '小丑', '扎帕', '绮菈绮菈', '珀尔诺']))
ALIASES = {'tora': 'トラ', 'kikuchiyo': 'キクチヨ'}
FONT = Path('C:/Windows/Fonts/msyh.ttc')


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as f:
        for chunk in iter(lambda: f.read(8 * 1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def save(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def opaque_image(path, background=(240, 240, 240), crop=None):
    with Image.open(path) as src:
        im = src.convert('RGBA')
    if crop is not None:
        x0, y0, x1, y1 = crop
        if not (0 <= x0 < x1 <= im.width and 0 <= y0 < y1 <= im.height):
            raise ValueError(f'Invalid crop: {path}: {crop}')
        im = im.crop(crop)
    else:
        bbox = im.getchannel('A').getbbox()
        if bbox is None:
            raise ValueError(f'Fully transparent image: {path}')
        im = im.crop(bbox)
    bg = Image.new('RGBA', im.size, (*background, 255))
    return Image.alpha_composite(bg, im).convert('RGB')


def visible_hash(path, crop=None):
    im = opaque_image(path, crop=crop)
    return hashlib.sha256(str(im.size).encode() + im.tobytes()).hexdigest()


def inventory(root=ROOT, original=ORIGINAL):
    rows = list(csv.DictReader((original / 'assets/manifest.csv').open(encoding='utf-8-sig')))
    nsfw = set(re.findall(r'"([^"]+)"', (original / 'reverse/data/current/0_NSFWCG信息.x').read_text(encoding='utf-8')))
    candidates, excluded = [], collections.Counter()
    for role, name in ROSTER.items():
        sequences = collections.defaultdict(list)
        for row in rows:
            if row['kind'] != 'image' or '\\current\\' not in row['output']:
                continue
            n = row['original_name']
            stem = n.rsplit('.', 1)[0]
            if name not in n and not n.startswith(ALIASES.get(role, name) + '／'):
                continue
            if stem in nsfw or any(s in stem for s in ('脱衣', '失手切入', '败北', '特殊', '坏坏技能')):
                excluded[role] += 1
                continue
            parts = stem.split('／')
            kind = None
            if row['category'] == '立绘' and len(parts) == 4 and parts[1] == name:
                kind, group = 'portrait', '/'.join(parts[:-1])
            elif row['category'] == '角色与敌人' and len(parts) == 3 and parts[0] == ALIASES.get(role, name):
                if not any(t in parts[1] for t in ('攻击', '待机', 'ジャンプ', 'シャンプ', 'とび', '物品', '超必杀')):
                    continue
                kind, group = 'battle', '/'.join(parts[:-1])
            elif stem in (f'系统／打斗结算／立绘／{name}', f'系统／标题／キャラ／{name}'):
                kind, group = 'illustration', stem.replace('／', '/')
            elif row['category'] == '剧情图像' and (stem.startswith(f'事件／{name}／') or stem.startswith('事件／主要／')):
                # NSFW table exclusion is necessary, not sufficient: all candidates require visual review.
                kind = 'scene'
                group = re.sub(r'[Ａ-Ｚ]+$', '', stem).replace('／', '/')
            if not kind:
                continue
            record = {'character': role, 'source': str(original / row['output']), 'source_name': n,
                      'source_sha256': row['sha256'], 'kind': kind, 'group': group,
                      'source_package': row['source'], 'source_index': int(row['index']),
                      'width': int(row['width']), 'height': int(row['height'])}
            sequences[(kind, group)].append(record)
        for (kind, group), records in sorted(sequences.items()):
            if kind == 'portrait':
                keep = [r for r in records if Path(r['source']).stem in ('基本', '乐', '怒', '哀', '驚', '乐Ｂ')]
            elif kind == 'battle':
                records.sort(key=lambda r: r['source_name'])
                keep = [records[i] for i in sorted({0, len(records)//3, 2*len(records)//3, len(records)-1})]
            elif kind == 'scene':
                keep = sorted(records, key=lambda r: (len(r['source_name']), r['source_name']))[:1]
            else:
                keep = records
            for r in keep:
                r['id'] = role + '-' + hashlib.sha256(r['source_name'].encode()).hexdigest()[:10]
                r['decision'] = 'pending'
                candidates.append(r)
    official_path = root / 'official/manifest.json'
    if official_path.exists():
        for r in load(official_path):
            if r.get('character'):
                candidates.append({**r, 'id': r['character'] + '-web-' + r['sha256'][:10],
                                   'source': r['path'], 'source_name': r['url'], 'source_sha256': r['sha256'],
                                   'kind': 'official', 'group': r['group'], 'decision': 'pending'})
    save(root / 'candidates.json', candidates)
    save(root / 'inventory-summary.json', {'candidates': dict(collections.Counter(r['character'] for r in candidates)),
                                          'excluded_nsfw_or_nondefault': dict(excluded),
                                          'source_manifest_sha256': sha(original / 'assets/manifest.csv')})
    print(json.dumps(load(root / 'inventory-summary.json'), ensure_ascii=False))


def sheets(root=ROOT, role=None, kind=None):
    rows = load(root / 'candidates.json')
    font = ImageFont.truetype(str(FONT), 15)
    for char in ([role] if role else ROSTER):
        chosen = [r for r in rows if r['character'] == char and (not kind or r['kind'] == kind)]
        for start in range(0, len(chosen), 24):
            batch = chosen[start:start+24]
            canvas = Image.new('RGB', (1440, math.ceil(len(batch)/6)*310), 'white')
            draw = ImageDraw.Draw(canvas)
            for j, r in enumerate(batch):
                im = opaque_image(r['source'])
                im.thumbnail((228, 240))
                x, y = (j % 6)*240, (j//6)*310
                canvas.paste(im, (x+(240-im.width)//2, y+(242-im.height)//2))
                draw.text((x+4, y+242), r['id'], font=font, fill='black')
                label = r['source_name'].replace('／', '/')
                draw.text((x+4, y+262), label[-28:], font=font, fill='black')
                draw.text((x+4, y+284), f"{r['kind']} {r.get('width','?')}x{r.get('height','?')}", font=font, fill='black')
            path = root / 'review' / f'{char}-{kind or "all"}-{start//24+1:02}.jpg'
            path.parent.mkdir(parents=True, exist_ok=True)
            canvas.save(path, quality=93)
            print(path)


def prepared_image(path, crop=None, background=(240, 240, 240)):
    # Keep native detail. The trainer, not the source exporter, performs 1024-area
    # bucketing. Reducing a tall drawing to 960px first needlessly loses detail.
    with Image.open(path) as source:
        rgba = source.convert('RGBA')
        if crop is not None:
            rgba = rgba.crop(crop)
        transparent = rgba.getchannel('A').getextrema()[0] < 255
    im = opaque_image(path, background=background, crop=crop)
    if not transparent:
        return im  # Preserve scene backgrounds without introducing a gray frame.
    margin = max(8, round(min(im.size) * 0.04))
    return ImageOps.expand(im, border=margin, fill=background)


def build(root=ROOT, version='v1', require_holdout=True):
    candidates = {r['id']: r for r in load(root / 'candidates.json')}
    reviews = load(root / 'curation.json')
    if len({r['id'] for r in reviews}) != len(reviews):
        raise ValueError('Duplicate curation IDs')
    for r in reviews:
        if r['id'] not in candidates:
            raise ValueError(f'Unknown reviewed candidate: {r["id"]}')
    # Never overwrite a paid run's data or its submission evidence.
    for role in ROSTER:
        config_path = root/'configs'/f'{role}.json'
        if config_path.exists() and load(config_path)['submission']['status'] != 'NOT_SUBMITTED':
            raise ValueError(f'{role}: submitted dataset is frozen; use a new workspace version')
    built = []
    for role in ROSTER:
        records = []
        out = root / 'datasets' / role
        selected = [r for r in reviews if r['id'] in candidates and candidates[r['id']]['character'] == role and r['split'] in ('train','holdout')]
        groups, visuals, sources = {}, {}, {}
        for review in selected:
            r = candidates[review['id']]
            if r['kind'] not in ('portrait','battle','illustration','scene','official'):
                raise ValueError(f'Only official original-game/website sources are allowed: {r["id"]}')
            if not review.get('reviewed_nonexplicit') or not review.get('caption'):
                raise ValueError(f'Missing visual review or caption: {r["id"]}')
            group = review.get('group', r['group'])
            split = review['split']
            if group in groups and groups[group] != split:
                raise ValueError(f'Source group leakage: {group}')
            groups[group] = split
            if sources.setdefault(r['source_sha256'], split) != split:
                raise ValueError(f'Same source crop crosses splits: {r["id"]}')
            if sha(r['source']) != r['source_sha256']:
                raise ValueError(f'Source changed: {r["source"]}')
            vh = visible_hash(r['source'], review.get('crop'))
            if vh in visuals:
                raise ValueError(f'Duplicate visible pixels: {r["id"]} and {visuals[vh]}')
            visuals[vh] = r['id']
            caption = review['caption']
            if not caption.startswith(f'dohna_{role}, '):
                raise ValueError(f'Wrong trigger: {r["id"]}')
            im = prepared_image(r['source'], review.get('crop'), tuple(review.get('background_rgb', (240,240,240))))
            target = out / split / (r['id']+'.png')
            target.parent.mkdir(parents=True, exist_ok=True)
            im.save(target)
            txt = target.with_suffix('.txt')
            txt.write_text(caption+'\n', encoding='utf-8')
            records.append({**r, **review, 'group': group, 'visible_sha256': vh, 'output': str(target),
                            'output_sha256': sha(target), 'caption_sha256': sha(txt), 'output_size': list(im.size)})
        train = [r for r in records if r['split']=='train']
        hold = [r for r in records if r['split']=='holdout']
        low = [r for r in train if r['kind']=='battle' and max(r['width'],r['height'])<1024]
        if len(low) > len(train)/4:
            raise ValueError(f'{role}: low-resolution battle images exceed 25%')
        # Same pose in a distinct costume is a separate drawing, but both still
        # share the leakage group across train/holdout.
        for group, count in collections.Counter((r['group'], r.get('outfit')) for r in records if r['kind']=='portrait').items():
            if count>3:
                raise ValueError(f'Too many expression variants: {group}')
        if not train or (require_holdout and not hold):
            raise ValueError(f'{role}: train and independent holdout are required')
        allowed = {Path(r['output']).resolve() for r in records}
        allowed |= {p.with_suffix('.txt') for p in list(allowed)}
        stale = [p for split in ('train','holdout') for p in (out/split).glob('*') if p.resolve() not in allowed]
        for path in stale:
            # Archive only recognized generated files, never delete user files.
            if path.suffix not in ('.png','.txt') or path.stem not in candidates:
                raise ValueError(f'Unexpected dataset file: {path}')
            dest = root/'archive'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')/role/path.parent.name/path.name
            if not path.resolve().is_relative_to((root/'datasets').resolve()) or not dest.resolve().is_relative_to(root.resolve()):
                raise ValueError('Archive path escaped workspace')
            dest.parent.mkdir(parents=True, exist_ok=True)
            path.rename(dest)
        identity = [{'id':r['id'],'group':r['group'],'split':r['split'],'image':r['output_sha256'],'caption':r['caption_sha256']} for r in records]
        fingerprint = hashlib.sha256(json.dumps(identity, sort_keys=True,ensure_ascii=False).encode()).hexdigest()
        steps=6000 if role in ('kuma','alyce') else 4000
        nominal_epoch_steps=math.ceil(10*len(train)/2)
        summary={'character':role,'display_name':ROSTER[role],'trigger':f'dohna_{role}','train':len(train),'holdout':len(hold),
                 'source_groups':len(groups),'holdout_groups':sum(s=='holdout' for s in groups.values()),
                 'low_resolution_battle':len(low),'fingerprint':fingerprint,'planned_steps':steps,
                 'step_policy':'6000-step pilots; 4000 provisional remainder to reserve all first runs; select checkpoints visually',
                 'nominal_presentations_per_image':steps*2/len(train),
                 'preprocessing':'native resolution; alpha-composited neutral backgrounds; trainer does 1024-area buckets',
                 'below_target':len(train)<20,'status':'PREPARED_NOT_TRAINED',
                 'coverage':sorted({r.get('outfit','unspecified') for r in records}),
                 'data_readiness':'READY_FOR_PLATFORM_CHECK',
                 'quantity_policy':'2026-10-08: user accepts fewer than 20 images; official sources only',
                 'source_policy':'OFFICIAL_AND_VERIFIED_ORIGINAL_ARTIST',
                 'independent_holdout_available':bool(hold),
                 'quality_risk':'limited independent poses and views' if len(train)<20 else None}
        package=root/'packages'/f'{role}-{fingerprint[:12]}.zip'
        package.parent.mkdir(parents=True,exist_ok=True)
        with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
            for r in train:
                for p in (Path(r['output']),Path(r['output']).with_suffix('.txt')):
                    info=zipfile.ZipInfo(p.name,(2026,10,8,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
                    z.writestr(info,p.read_bytes())
        summary['package']=str(package);summary['package_sha256']=sha(package)
        save(out / 'manifest.json', {'summary':summary,'images':records})
        save(out/'summary.json',summary)
        config={'character':role,'dataset_fingerprint':fingerprint,'dataset_package_sha256':summary['package_sha256'],
                'required_training_base':'Krea-2-Raw','inference_base':'Krea-2-Turbo','requested_parameters':{
                'networkDim':32,'networkAlpha':32,'learningRate':0.0001,'unetLr':0.0001,'textEncoderLr':0.00001,
                'optimizerType':'AdamW','lrScheduler':'cosine_with_restarts','lrSchedulerNumCycles':3,
                'trainBatchSize':2,'repeat':10,'maxTrainEpochs':math.ceil(steps/nominal_epoch_steps),'maxTrainSteps':steps,
                'imageWidth':1024,'imageHeight':1024,'enableBucket':True,'bucketResoSteps':64,
                'seed':42,'sampleSeed':42,'shuffleCaption':False,'caption_rewrite':False,
                'saveEveryNEpochs':1,'isModelscopePublic':True,'isProtectedMode':False,
                'triggerWord':f'dohna_{role}','outputName':f'dohna_{role}-krea2-{version}'},
                'experiment':{'revision':'2026-10-08-documentation-review','pilot':role in ('kuma','alyce'),
                    'nominal_epoch_steps':nominal_epoch_steps,'epoch_estimate_note':'before bucket rounding; actual trainer may differ',
                    'checkpoint_targets':[1000,3000,6000] if steps==6000 else [1000,2000,4000],
                    'checkpoint_rule':'Choose closest real early/middle/late checkpoints; never relabel actual steps. Review earlier saves when late checkpoints overfit.',
                    'remaining_roster_gate':'Pilot conversion and visual acceptance must pass before submitting other eight',
                    'platform_parameters_note':'Requested values are not evidence of accepted values; snapshot visible controls and returned task configuration',
                    'budget':{'max_jobs_per_character':2,'recharge_allowed':False,'aliyun_billing_allowed':False,
                              'prefer_free_queue':True,'reserve_first_runs_for_all_characters':True},
                    'submission_requirements':['current quote and balance','Raw training confirmation','checkpoint retrieval evidence and cadence limitations recorded',
                                               'caption import checked','actual parameter differences recorded']},
                'submission':{'status':'NOT_SUBMITTED','actual_parameters':None,'quote':None,'balance':None,'task_id':None}}
        save(root/'configs'/f'{role}.json',config)
        built.append(summary)
    save(root/'dataset-summary.json',built)
    print(json.dumps(built,ensure_ascii=False,indent=2))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify(root=ROOT):
    verified=[]
    for summary in load(root/'dataset-summary.json'):
        role=summary['character'];m=load(root/'datasets'/role/'manifest.json');groups={}
        expected={}; files=set(); visuals=set(); sources={}
        for r in m['images']:
            path=Path(r['output']);caption=path.with_suffix('.txt')
            files.update((path.resolve(),caption.resolve()))
            require(sha(path)==r['output_sha256'],str(path))
            require(sha(caption)==r['caption_sha256'],str(caption))
            require(sha(r['source'])==r['source_sha256'],r['source'])
            require(groups.setdefault(r['group'],r['split'])==r['split'],'split leakage')
            require(sources.setdefault(r['source_sha256'],r['split'])==r['split'],'source crop leakage')
            vh=visible_hash(r['source'],r.get('crop'))
            require(vh==r['visible_sha256'] and vh not in visuals,'duplicate or changed visible content')
            visuals.add(vh)
            require(caption.read_text(encoding='utf-8').startswith(f'dohna_{role}, '),'wrong trigger')
            with Image.open(path) as im:
                require(im.mode=='RGB' and list(im.size)==r['output_size'],f'{path}: wrong format or dimensions')
            if r['split']=='train':
                expected[path.name]=path.read_bytes();expected[caption.name]=caption.read_bytes()
        actual={p.resolve() for split in ('train','holdout') for p in (root/'datasets'/role/split).glob('*')}
        require(actual==files,f'{role}: stale or untracked files')
        require(sha(summary['package'])==summary['package_sha256'],'package hash mismatch')
        with zipfile.ZipFile(summary['package']) as z:
            require(len(z.namelist())==len(expected) and set(z.namelist())==set(expected),'unexpected ZIP entries')
            require(all(z.read(n)==data for n,data in expected.items()),'ZIP bytes mismatch')
        config=load(root/'configs'/f'{role}.json')
        require(config['dataset_fingerprint']==summary['fingerprint'] and config['dataset_package_sha256']==summary['package_sha256'],'config data mismatch')
        verified.append({'character':role,'files_verified':len(m['images'])*2,'status':'PASS'})
    save(root/'verification.json',verified)
    print(json.dumps(verified,ensure_ascii=False))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('action',choices=['inventory','sheets','build','verify'])
    p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--original',type=Path,default=ORIGINAL)
    p.add_argument('--version',default='v1')
    p.add_argument('--role',choices=list(ROSTER));p.add_argument('--kind')
    args=p.parse_args()
    if args.action=='inventory':inventory(args.root,args.original)
    elif args.action=='sheets':sheets(args.root,args.role,args.kind)
    elif args.action=='build':build(args.root,args.version)
    else:verify(args.root)
