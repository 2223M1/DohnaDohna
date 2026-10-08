"""Build the local image-review proposal; never upload or submit training.

v1 stays frozen. Public originals, reference-only merchandise and unresolved
leads have distinct records. Only visually reviewed nonexplicit outputs appear
in the proposed training/holdout grids.
"""
from __future__ import annotations

from collections import Counter
from datetime import datetime, timezone
import hashlib
import html
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from dataset import ROOT, ROSTER, FONT, load, save, sha, prepared_image, visible_hash

REVISION = ROOT / 'revisions/official-gyokai-v2'

# Each entry was inspected at its saved public resolution. Card derivatives
# are aliases of these drawings, never additional training examples.
ADDITIONS = {
    'alyce-official-keyvisual': ('train', 'battle', 'holding a large blue and purple parasol overhead, airborne with bent knees, long blonde hair flowing sideways, black rabbit-ear ribbons, blue and white ruffled dress, patterned stockings and dark boots, full body, plain light gray background', ['LO-3976', 'game title / ALyCE']),
    'medhico-official-keyvisual': ('train', 'battle', 'teal twin tails and green eyes, white and purple nurse cap, futuristic nurse outfit, white gloves, purple leggings and pale boots, leaning forward with bent knees, looking up toward the viewer, full body, plain light gray background', ['LO-4014', 'game title / Medhico']),
    'kirakira-official-keyvisual': ('holdout', 'battle', 'pink and yellow twin tails, winking, a purple cropped jacket over a teal top, dark gloves and purple leggings, carrying a large pink chainsaw across her shoulders, airborne with bent knees, full body, plain light gray background', ['LO-4008', 'game title / Kirakira']),
    'antena-official-keyvisual': ('train', 'battle', 'lime green bobbed hair, green eyes and cat-ear headphones, teal and purple cropped jacket and shorts, mismatched patterned boots, jumping with one knee raised and one hand beside her head, smiling, full body, plain light gray background', ['LO-4004', 'game title / Antena']),
    'kikuchiyo-official-keyvisual': ('train', 'kimono_promo', 'long dark hair with pink and purple highlights, red eyes and a gold hair ornament, short white floral kimono with red trim and a red waist sash, dark thigh-high legwear and red gloves, a white fox mask sitting above her forehead, holding a katana with both arms raised overhead, dynamic full-body pose with one knee bent, cyan and yellow graphic outlines, plain light gray background', ['LO-3975', 'game title / Kikuchiyo']),
    'antena-artist-E0ihsxVVcAQVAvB': ('train', 'casual', 'lime green bobbed hair, green eyes and cat-ear headphones, pink and black jacket with silver overall straps, holding a pen horizontally in her mouth and raising one finger, close portrait, colorful geometric yellow and teal background', ['LO-4004-A', 'LO-4004-K']),
    'medhico-artist-E0ihtgMVEAAcuph': ('train', 'casual', 'teal hair in a ponytail with a yellow bow, glasses, pink vest over a white short-sleeved blouse, both arms raised behind her head, holding a yellow tie in her mouth, upper-body portrait, teal and pink geometric background', ['LO-4014-K']),
    'kikuchiyo-artist-E0V-QidVgAAu8m7': ('train', 'casual', 'long black hair with pink and purple highlights, red eyes and a gold hair ornament, white blouse and mint blue school vest, gripping a sheathed katana in both hands, looking toward the viewer with a focused expression, tilted upper-body view, multicolored geometric background', ['LO-4110-A']),
    'antena-pixiv-90252459-p1': ('holdout', 'battle', 'lime green bobbed hair, green eyes and cat-ear headphones, teal and purple cropped jacket, shorts and patterned boots, sitting cross-legged with her chin resting on one hand and a smartphone in the other, smiling, green and yellow patterned background', ['Pixiv 90252459 p0: same drawing with birthday text']),
}

# Reconsidered individually rather than inheriting v1's casual-only blanket.
GAME_ADDITIONS = {
    'antena-a2cf26e6c7': ('train', 'battle', 'portrait/antena/neutral', 'lime green bobbed hair, green eyes and cat-ear headphones, teal and purple cropped jacket, teal shorts, asymmetric patterned boots and a backpack, standing with one hand at her hip, smiling, full-body character portrait'),
    'antena-aff8f42ab9': ('train', 'battle', 'portrait/antena/乐', 'lime green bobbed hair and cat-ear headphones, teal and purple cropped jacket, shorts and patterned boots, standing with folded arms and closed eyes, full-body character portrait'),
    'antena-fc0890b9fa': ('holdout', 'battle', 'portrait/antena/leaning', 'lime green bobbed hair, green eyes and cat-ear headphones, teal and purple cropped jacket and shorts, leaning forward with hands lowered, wide eyes and surprised expression, character portrait'),
    'kirakira-3cc4286199': ('train', 'battle', 'portrait/kirakira/neutral', 'pink twin tails with yellow streaks, turquoise eyes, a purple cropped jacket over a teal top, dark gloves, purple leggings and thigh straps, standing with a pink chainsaw resting across her shoulders, full-body character portrait'),
    'kirakira-3ae4ee4c6a': ('train', 'battle', 'portrait/kirakira/乐', 'pink twin tails with yellow streaks, a purple cropped jacket over a teal top and dark gloves, holding a pink chainsaw across her shoulders, smiling with eyes closed, upper-body character portrait'),
    'kirakira-166304eaff': ('train', 'battle', 'portrait/kirakira/leaning', 'pink twin tails with yellow streaks, turquoise eyes, a purple cropped jacket over a teal top and dark gloves, leaning forward with a worried expression, holding a lowered pink chainsaw, character portrait'),
}


def unresolved():
    leads = []
    supplements_path=REVISION/'official/review-supplements/manifest.json'
    supplied=load(supplements_path) if supplements_path.exists() else []
    available_ids={r['id'] for r in supplied}
    for role, product in [('alyce',1909),('antena',1912),('kikuchiyo',1910),('medhico',1911),('kirakira',1908),('porno',1907)]:
        available=f'{role}-tamatoys-crop' in available_ids
        leads.append({'id':f'{role}-tamatoys-original','character':role,'title':f'{ROSTER[role]} · Tamatoys 包装新绘',
            'status':'PACKAGING_CROP_AVAILABLE' if available else 'ORIGINAL_NOT_FOUND',
            'reason':'已核实独立鱼介新绘，并提供完整正面提案。无字原文件仍未取得；依据用户最新指示保留原有标题与文字以保全肢体。同一幅画的包装/平面稿/再版不重复计数。' if available else '已核实鱼介新绘，完整无字源未取得。',
            'urls':['https://www.alicesoft.com/information/2021/entry002692.html',
                    'https://www.alicesoft.com/information/2026/entry003886.html',
                    f'https://tamatoys.tma.co.jp/item/detail/TMT-{product}'],
            'training_eligible':available})
    for role, card, title in [('alyce','LO-3976-L','持伞前倾插画'),('kikuchiyo','LO-3975-S','女仆与甜点插画')]:
        leads.append({'id':card,'character':role,'title':f'{ROSTER[role]} · {card} {title}',
            'status':'ORIGINAL_NOT_FOUND','reason':'官方卡牌署名鱼介；独立完整原图未取得。保留卡牌作为追溯线索，不裁卡框加入训练。',
            'urls':[f'https://lycee-tcg.com/card/card_detail.pl?cardno={card}'], 'training_eligible':False})
    status = load(REVISION/'official/artist-x/acquisition-status.json')
    for row in status['failed']:
        if any(ident.endswith(row['media_id']) for ident in available_ids):continue
        leads.append({'id':row['media_id'],'character':row['character'],
            'title':f"{ROSTER[row['character']]} · 已定位画师原帖 {row['media_id']}",
            'status':'LOCATED_DOWNLOAD_FAILED','reason':'作者原帖及图片已目视核对；公开图片连接失败，未保存有效原文件。网页截图不作训练原图。',
            'urls':[row['page_url']], 'training_eligible':False})
    return leads


def make_proposal():
    candidates = {r['id']:r for r in load(ROOT/'candidates.json')}
    reviews = load(ROOT/'curation.json')
    for r in reviews:
        r['change'] = 'retained_v1'
        r['selection_note'] = '沿用已审查的 v1 选择；等待用户审阅确认。'
        if r['id']=='kuma-extra-567ade5d1d':
            r.update(crop=[560,60,1230,720],change='expanded_scene_crop',
                caption='dohna_kuma, teal and yellow streaked dark hair, scarf and battle jacket, seated passenger in a car, upper body partly hidden behind another person\'s foreground arm and the steering wheel, warm evening light, colorful anime illustration.',
                selection_note='扩大裁切保留阿熊左肩和可见上身；前景握方向盘的手属于另一人物，不计作阿熊的手部参考。')
        if r['id']=='porno-extra-7b8e21c405':
            r.update(crop=[650,130,1120,720],change='expanded_scene_crop',
                caption='dohna_porno, short silver hair with pastel streaks, eyes closed, black studded choker and pale blue dress, leaning forward toward food offered on a spoon, upper body behind a plate of food, restaurant background, colorful anime illustration.',
                selection_note='向上扩大裁切恢复原有头顶；自身双手与腿脚在原画中被遮挡或未入画，不能算手足训练样本。')
    for section in ('keyvisuals','artist-x','artist-pixiv'):
        for row in load(REVISION/f'official/{section}/manifest.json'):
            candidates[row['id']] = row
    for ident,(split,outfit,description,aliases) in ADDITIONS.items():
        if ident not in candidates:
            raise ValueError(f'Reviewed source is missing: {ident}')
        role = candidates[ident]['character']
        reviews.append({'id':ident,'split':split,'group':candidates[ident]['group'],
            'outfit':outfit,'caption':f'dohna_{role}, {outfit} outfit, {description}, colorful anime illustration.',
            'crop':None,'background_rgb':[240,240,240],'reviewed_nonexplicit':True,
            'reviewer':'Codex visual review 2026-10-09','change':'new_official_art',
            'same_drawing_aliases':aliases,
            'selection_note':'新增官网/作者完整构图；网页公开尺寸，未声称为原始工作文件。'})
    for ident,(split,outfit,group,description) in GAME_ADDITIONS.items():
        role=candidates[ident]['character']
        reviews.append({'id':ident,'split':split,'group':group,'outfit':outfit,
            'caption':f'dohna_{role}, {outfit} outfit, {description}, plain light gray background, colorful anime illustration.',
            'crop':None,'background_rgb':[240,240,240],'reviewed_nonexplicit':True,
            'reviewer':'Codex visual review 2026-10-09','change':'reconsidered_game_art',
            'selection_note':'逐图复核后补入原作战斗装；同身体/姿势的表情与换装仍归于同一组。'})
    supplemental_path=REVISION/'official/review-supplements/manifest.json'
    if supplemental_path.exists():
        for row in load(supplemental_path):
            review=row.pop('review')
            candidates[row['id']]=row
            reviews.append({'id':row['id'],'group':row['group'],'background_rgb':[240,240,240],
                'reviewed_nonexplicit':True,'reviewer':'Codex visual review 2026-10-09',**review})
    # A nonexplicit face crop is visible for review, but is not counted as a
    # ready training sample because its 330 x 300 native detail is limited.
    reviews.append({'id':'porno-f8ce9c28f9','split':'reserve','group':'portrait/porno/neutral',
        'outfit':'battle_face_only','crop':[550,490,880,790],'background_rgb':[240,240,240],
        'caption':'dohna_porno, close-up face, short silver hair with pastel pink and yellow streaks, pink eye, pale head straps and a side earpiece, a gloved hand near her chin, plain light gray background.',
        'reviewed_nonexplicit':True,'reviewer':'Codex visual review 2026-10-09','change':'review_only_crop',
        'selection_note':'仅非露骨头部裁切，330×300 原生细节偏低，暂列备选；未用全身装束图作为训练输出。'})
    save(REVISION/'candidates.json',list(candidates.values()))
    save(REVISION/'curation.json',reviews)
    return candidates,reviews


def prepare_outputs(candidates,reviews):
    records=[]; ids=set(); split_groups={}; source_splits={}; seen={}
    out=REVISION/'review'
    coverage_audit=load(REVISION/'limb-coverage-audit.json')['images']
    for review in reviews:
        ident=review['id']; source=candidates[ident]; role=source['character']
        if ident in ids:raise ValueError(f'Duplicate ID {ident}')
        ids.add(ident)
        if sha(source['source']) != source['source_sha256']:raise ValueError(f'Changed source: {ident}')
        if source.get('parent_source') and sha(source['parent_source'])!=source['parent_source_sha256']:
            raise ValueError(f'Changed original parent: {ident}')
        if not review['reviewed_nonexplicit']:raise ValueError(f'Unreviewed image: {ident}')
        if not review['caption'].startswith(f'dohna_{role}, '):raise ValueError(f'Wrong trigger: {ident}')
        if review['split'] != 'reserve':
            group=(role,review['group']); split=review['split']
            if split_groups.setdefault(group,split)!=split:raise ValueError(f'Group leakage: {ident}')
            source_key=(role,source['source_sha256'])
            if source_splits.setdefault(source_key,split)!=split:raise ValueError(f'Source/crop leakage: {ident}')
            vh=(role,visible_hash(source['source'],review.get('crop')))
            if vh in seen:raise ValueError(f'Duplicate drawing pixels: {ident}, {seen[vh]}')
            seen[vh]=ident
        im=prepared_image(source['source'],review.get('crop'),tuple(review.get('background_rgb',[240,240,240])))
        path=out/'images'/role/(ident+'.png');path.parent.mkdir(parents=True,exist_ok=True);im.save(path)
        thumb=im.copy();thumb.thumbnail((500,560));thumbpath=out/'thumbs'/role/(ident+'.jpg');thumbpath.parent.mkdir(parents=True,exist_ok=True);thumb.save(thumbpath,quality=91)
        record={**source,**review,'output':str(path),'output_sha256':sha(path),'output_size':list(im.size),
                'preview_url':path.relative_to(out).as_posix(),'thumb_url':thumbpath.relative_to(out).as_posix()}
        coverage=coverage_audit[ident]
        if coverage['output_sha256']!=record['output_sha256']:
            raise ValueError(f'Image changed; repeat visual limb review: {ident}')
        record['limb_coverage']=coverage
        records.append(record)
    for role in ROSTER:
        train=[r for r in records if r['character']==role and r['split']=='train']
        low=[r for r in train if r['kind']=='battle' and max(r['width'],r['height'])<1024]
        if len(low)>len(train)/4:raise ValueError(f'Too many low-resolution battle sprites: {role}')
        poses=Counter((r['group'],r['outfit']) for r in records if r['character']==role and r['kind']=='portrait' and r['split']!='reserve')
        if any(n>3 for n in poses.values()):raise ValueError(f'Too many expression variants: {role}')
    return records


def contact_sheet(records,path,title):
    font=ImageFont.truetype(str(FONT),18);small=ImageFont.truetype(str(FONT),13)
    cols=5;cellw=270;cellh=345
    canvas=Image.new('RGB',(cols*cellw,65+cellh*((len(records)+cols-1)//cols)),(247,247,248))
    draw=ImageDraw.Draw(canvas);draw.text((16,17),title,font=font,fill=(20,30,45))
    for i,r in enumerate(records):
        x=(i%cols)*cellw;y=65+(i//cols)*cellh
        with Image.open(r['output']) as image:im=image.copy()
        im.thumbnail((250,265));canvas.paste(im,(x+(cellw-im.width)//2,y))
        split={'train':'拟训练','holdout':'拟验证','reserve':'备选'}[r['split']]
        draw.text((x+8,y+272),f"{ROSTER[r['character']]} · {split} · {r['output_size'][0]}×{r['output_size'][1]}",font=small,fill='black')
        draw.text((x+8,y+294),r['id'][:32],font=small,fill='black')
        draw.text((x+8,y+315),r['group'][-32:],font=small,fill=(75,80,90))
    canvas.save(path,quality=93)


def render(records,leads):
    out=REVISION/'review'; summaries=[]
    for role,name in ROSTER.items():
        rs=[r for r in records if r['character']==role]
        selected=[r for r in rs if r['split']!='reserve']
        groups={r['group']:r['split'] for r in selected}
        summary={'id':role,'name':name,'train':sum(r['split']=='train' for r in rs),
            'holdout':sum(r['split']=='holdout' for r in rs),'reserve':sum(r['split']=='reserve' for r in rs),
            'groups':len(groups),'holdout_groups':sum(s=='holdout' for s in groups.values()),
            'new':sum(r['change']!='retained_v1' and r['split']!='reserve' for r in rs),
            'coverage':sorted({r['outfit'] for r in selected}),
            'open_leads':sum(r['character']==role and not r['training_eligible'] for r in leads)}
        summary['limb_coverage']={split:{
            'full_body':sum(r['limb_coverage']['framing']=='full_body' for r in rs if r['split']==split),
            'own_hand_visible':sum(r['limb_coverage']['own_hand_visible'] for r in rs if r['split']==split),
            'own_foot_or_shoe_visible':sum(r['limb_coverage']['own_foot_or_shoe_visible'] for r in rs if r['split']==split),
            'full_body_groups':len({r['group'] for r in rs if r['split']==split and r['limb_coverage']['framing']=='full_body'})
        } for split in ('train','holdout')}
        summaries.append(summary);contact_sheet(rs,out/f'{role}-proposal.jpg',f'{name} · 选图审阅提案 · {summary["train"]} 训练 / {summary["holdout"]} 验证 / {summary["reserve"]} 备选')
    originals=[r for r in records if r['change']=='new_official_art']
    contact_sheet(originals,out/'new-originals.jpg',f'官网与鱼介原帖图 · 完整公开构图 · {len(originals)} 张')
    packaging=[r for r in records if r['change']=='licensed_packaging_crop']
    if packaging:contact_sheet(packaging,out/'packaging-crops.jpg','Tamatoys 六幅独立新绘 · 完整正面 · 五图按145:210校正，绮菈绮菈用画师平面稿')
    more_artist=[r for r in records if r['change']=='new_artist_crop']
    if more_artist:contact_sheet(more_artist,out/'more-artist.jpg','本轮新核实的鱼介 X 作品 · 拟选与备选裁切')
    fingerprint=hashlib.sha256(json.dumps([{'id':r['id'],'split':r['split'],'group':r['group'],'sha256':r['output_sha256'],'caption':r['caption'],'limb_coverage':r['limb_coverage']} for r in records],sort_keys=True,ensure_ascii=False).encode()).hexdigest()
    manifest={'created_at':datetime.now(timezone.utc).isoformat(),'status':'AWAITING_USER_REVIEW_WITH_SOURCE_GAPS',
        'training_gate':'WAIT_FOR_EXPLICIT_USER_DATASET_APPROVAL','allow_upload':False,'allow_submit':False,
        'fingerprint':fingerprint,'summaries':summaries,'images':records,'unresolved':leads,
        'limits':['数量是文件数，不能解释为独立原画数量；同姿势的表情和换装共享来源组。',
                  '包装六图提供裁切提案，无字原文件未取得；两张卡牌原图仍未取得。X搜索不保证覆盖作者所有历史作品。',
                  '原始分辨率不足不放大伪装高清；1024分桶发生在后续训练平台。',
                  '本次只建立审阅输出，未创建新版上传包、上传素材或提交任务。']}
    save(REVISION/'review-manifest.json',manifest);save(out/'review-data.json',manifest)
    e=html.escape
    stats=''.join(f'<tr><td><a href="#" class="role-link" data-role="{s["id"]}">{s["name"]}</a></td><td>{s["train"]}</td><td>{s["holdout"]}</td><td>{s["groups"]} / {s["holdout_groups"]}</td><td>{s["new"]}</td><td>{s["open_leads"]}</td><td><a href="{s["id"]}-proposal.jpg">联系表</a></td></tr>' for s in summaries)
    limb_stats=''.join(f'<tr><td>{s["name"]}</td><td>{s["limb_coverage"]["train"]["full_body"]} / {s["limb_coverage"]["holdout"]["full_body"]}</td><td>{s["limb_coverage"]["train"]["full_body_groups"]}</td><td>{s["limb_coverage"]["train"]["own_hand_visible"]} / {s["limb_coverage"]["holdout"]["own_hand_visible"]}</td><td>{s["limb_coverage"]["train"]["own_foot_or_shoe_visible"]} / {s["limb_coverage"]["holdout"]["own_foot_or_shoe_visible"]}</td></tr>' for s in summaries)
    cards=[]
    for r in records:
        source_link=f'<a href="{e(r["page_url"],quote=True)}" target="_blank" rel="noreferrer">作者／官方页面 ↗</a>' if r.get('page_url') else '<span>本地原作 · 见来源记录</span>'
        if r.get('comparison_url'):source_link+=f' · <a href="{e(r["comparison_url"],quote=True)}" target="_blank">编辑前后对照</a>'
        parent_info=f" · 包装源 {r['parent_size'][0]}×{r['parent_size'][1]}" if r.get('parent_size') else ''
        aliases='、'.join(r.get('same_drawing_aliases',[])) or '未新增网站衍生副本'
        label={'train':'拟训练','holdout':'拟验证','reserve':'备选，不计入训练'}[r['split']]
        change='沿用 v1' if r['change']=='retained_v1' else ('本轮扩大裁切' if r['change']=='expanded_scene_crop' else '本轮新增')
        cov=r['limb_coverage'];framing={'full_body':'全身构图','partial_body':'局部身体构图','head_crop':'头部备选'}[cov['framing']]
        limb_label=f"{framing} · 自身手部{'可见' if cov['own_hand_visible'] else '不可见'} · 足/鞋{'可见' if cov['own_foot_or_shoe_visible'] else '不可见'}"
        cards.append(f'''<article class="card" data-role="{r['character']}" data-split="{r['split']}" data-new="{int(r['change']!='retained_v1')}" data-frame="{cov['framing']}" data-hands="{int(cov['own_hand_visible'])}" data-feet="{int(cov['own_foot_or_shoe_visible'])}">
          <a class="art" href="{r['preview_url']}" target="_blank"><img loading="lazy" src="{r['thumb_url']}" alt="{e(r['id'])}"></a>
          <div class="body"><div class="labels"><span class="badge {r['split']}">{label}</span><span>{change}</span></div>
          <h3>{ROSTER[r['character']]} <small>{e(r['id'])}</small></h3>
          <p class="coverage">{limb_label}</p><p class="dim">{e(cov['note'])}</p>
          <p>{e(r['selection_note'])}</p><p class="dim">输出 {r['output_size'][0]}×{r['output_size'][1]} · 输入文件 {r['width']}×{r['height']}{parent_info}</p>
          <p class="source">{source_link} · <a href="{r['preview_url']}" target="_blank">查看原尺寸输出</a></p>
          <details><summary>分组、裁切与 Caption</summary><p>来源：{e(r['source_name'])}</p><p>组：{e(r['group'])}</p><p>裁切：{e(str(r.get('crop') or '透明边界裁切 / 不裁掉画面内容'))}</p><p>同画别名：{e(aliases)}</p><p>{e(r['caption'])}</p><code>SHA256 {r['source_sha256']}</code></details>
          <label class="decision">审阅意见 <select data-id="{r['id']}"><option value="unreviewed">未审阅</option value="keep">保留</option><option value="exclude">移除</option><option value="adjust">需调整</option></select></label>
          </div></article>''')
    states={'LOCATED_DOWNLOAD_FAILED':'已定位，下载失败','PACKAGING_CROP_AVAILABLE':'已提供可用裁切','ORIGINAL_NOT_FOUND':'原图未取得'}
    gaprows=''.join(f'<tr><td>{e(r["title"])}</td><td>{states[r["status"]]}</td><td>{e(r["reason"])}</td><td>'+ ' / '.join(f'<a href="{e(u,quote=True)}" target="_blank" rel="noreferrer">来源{j+1}</a>' for j,u in enumerate(r['urls']))+'</td></tr>' for r in leads)
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>多娜多娜 · 十人选图审阅 v2</title>
<style>
:root{color-scheme:light;font-family:"Microsoft YaHei",system-ui,sans-serif;color:#182133;background:#f3f5f8}*{box-sizing:border-box}body{margin:0}header{padding:34px 4vw 26px;background:#17243b;color:white}header h1{font-size:29px;margin:5px 0 12px}header p{max-width:1100px;line-height:1.8;margin:8px 0}.eyebrow{font-size:13px;letter-spacing:2px;color:#9de6d1}.stop{display:inline-block;padding:7px 12px;border:1px solid #e9c475;border-radius:6px;color:#ffe2a0}main{padding:24px 4vw 70px}h2{font-size:22px;margin:24px 0 14px}a{color:#146989}header a{color:#9de6d1}.panel{background:white;border:1px solid #d8e0e9;border-radius:12px;padding:20px;margin:0 0 24px;overflow:auto}table{width:100%;border-collapse:collapse;font-size:14px}td,th{padding:10px 12px;text-align:left;border-bottom:1px solid #e2e7ef;line-height:1.55}th{color:#627084;font-weight:500}nav{display:flex;gap:10px;flex-wrap:wrap;align-items:center;background:#f3f5f8;padding:12px 0;position:sticky;top:0;z-index:2;border-bottom:1px solid #d8e0e9;margin-bottom:18px}select,button,input,textarea{font:inherit;border:1px solid #b7c6d5;border-radius:6px;padding:8px;background:white;color:#1c2b42}button{cursor:pointer}#count{color:#53657d;font-size:14px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(270px,1fr));gap:18px}.card{background:white;border:1px solid #d8e0e9;border-radius:10px;overflow:hidden}.art{height:330px;display:flex;justify-content:center;align-items:center;background:#e9edf1;padding:10px}.art img{width:100%;height:100%;object-fit:contain}.body{padding:16px}.labels{display:flex;gap:8px;align-items:center;font-size:12px;color:#637289}.badge{padding:4px 8px;border-radius:4px}.train{background:#dff1e8;color:#126146}.holdout{background:#dceafb;color:#215ba0}.reserve{background:#fff0ce;color:#825a13}h3{font-size:17px;margin:13px 0 10px}small{display:block;font-size:11px;font-weight:400;color:#53657d;word-break:break-all;margin-top:5px}p{font-size:13px;line-height:1.75}.dim,.source{color:#53657d}details{font-size:12px;line-height:1.8;margin:12px 0;overflow-wrap:anywhere}summary{cursor:pointer;color:#215ba0}code{font-size:10px}.decision{font-size:12px;display:flex;align-items:center;gap:10px}.decision select{font-size:12px;flex:1}.note{border-left:4px solid #d7a13d;padding-left:14px;color:#5e4a2c}.gaps td:nth-child(3){min-width:280px}textarea{width:100%;min-height:90px;margin:12px 0}footer{color:#68788b;font-size:12px;overflow-wrap:anywhere;margin-top:30px}[hidden]{display:none!important}@media(max-width:650px){header{padding:24px}main{padding:16px}header h1{font-size:24px}.art{height:330px}nav{position:static}.grid{grid-template-columns:1fr}}
</style><header><div class="eyebrow">DOHNA DOHNA / KREA2 / SOURCE REVIEW</div><h1>十人选图审阅 · v2 补充版</h1><span class="stop">等待你审阅；未上传、未开始新版训练</span><p>四张用户补充的鱼介 X 图已归档；另核实四幅原帖作品。以角色肢体完整度为先，保留可用全身、手臂、腿脚；包装文字保留，不为去字裁去身体。</p><p>五张斜拍包装按厂家宽145、高210毫米的真实盒面比例校正透视和横向压缩；绮菈绮菈直接用鱼介平面图。全部保留完整正面，附编辑前后对照；几何重采样不会增加原图细节。仍有 <strong>__GAPCOUNT__ 项未取得的卡牌原图</strong>；六张包装的无字原文件也尚未找到，但已有可审阅候选。<a href="#gaps">查看来源追溯状态 ↓</a></p></header><main>
<section class="panel"><h2>本轮提案概览</h2><p>共 __TOTAL__ 张拟选文件，另有 __RESERVE__ 张备选。文件数量包含表情与换装，同来源组不得跨训练／验证。<a href="packaging-crops.jpg" target="_blank">包装六图完整正面总览</a> · <a href="more-artist.jpg" target="_blank">新核实的四幅 X 图</a></p><table><thead><tr><th>角色</th><th>拟训练</th><th>拟验证</th><th>来源组 / 验证组</th><th>较 v1 新增/调整</th><th>未取得原图</th><th>总览</th></tr></thead><tbody>__STATS__</tbody></table><p class="note">三张备选均未计入训练：旧战斗立绘头部裁切分辨率低；两张新画师图的头部裁切有表情、轮廓或节日服饰限制。多数角色的独立侧面、背面仍不足；四位男性暂未找到新的独立官方绘。</p></section>
<section class="panel"><h2>肢体覆盖 · 逐图目视核对</h2><p>数量按「拟训练 / 拟验证」显示，备选不计。全身构图表示头至脚的轮廓在画内，允许衣物与自然姿态遮挡；手、足/鞋可见表示至少局部可见，不保证手指细节。旁人的手不计入角色自身。文件数包含表情差分，独立来源组另列。</p><table><thead><tr><th>角色</th><th>全身文件</th><th>训练全身来源组</th><th>自身手部可见</th><th>足/鞋可见</th></tr></thead><tbody>__LIMB_STATS__</tbody></table><p class="note">包装六图都没有足部，不能填补脚部缺口。低分辨率战斗图只作动作参考；后续卡图必须另外验收手指、鞋足和关节。立绘中原本只画到腰/大腿的图已标为局部身体，不伪称完整全身。</p></section><nav><select id="role"><option value="all">全部十人</option>__OPTIONS__</select><select id="split"><option value="all">全部用途</option><option value="train">拟训练</option><option value="holdout">拟验证</option><option value="reserve">备选</option></select><select id="limbs" aria-label="肢体覆盖筛选"><option value="all">全部构图</option><option value="full_body">全身构图</option><option value="hands">自身手部可见</option><option value="feet">足/鞋可见</option><option value="partial_body">局部身体</option></select><label><input type="checkbox" id="newOnly"> 只看新增/调整</label><span id="count"></span><button id="export">导出审阅意见</button></nav><div class="grid">__CARDS__</div>
<section id="gaps" class="panel" style="margin-top:32px"><h2>原图追溯状态</h2><p>包装六图与生日/特典卡图是不同作品；爱丽丝 LO-4111-A 的抱辫子插画不等于包装的双手托脸绘。六张包装完整正面已经计入提案，另外两张卡牌线索仍未计入。</p><table class="gaps"><thead><tr><th>作品</th><th>状态</th><th>原因</th><th>证据</th></tr></thead><tbody>__GAPS__</tbody></table></section>
<section class="panel"><h2>补充意见</h2><p>可以逐图选择保留／移除／需调整，或直接在对话中指出编号。页面只暂存本浏览器审阅意见；导出为 JSON 后由你决定如何使用，不会提交训练。</p><textarea id="notes" placeholder="还有哪些作品、服装或角度遗漏？哪些图应删除或调整裁切？"></textarea><p><a href="review-data.json">完整来源、Caption、SHA256 清单</a> · <a href="new-originals.jpg" target="_blank">完整官网／作者图联系表</a></p></section>
<footer>提案指纹：__FP__<br>v1 阿熊任务 183237 已取消，最后观察到 30/6000 步，没有可用检查点；爱丽丝未提交。本审阅页不改变云端状态。</footer></main>
<script>
const proposal='__FP__',key='dohna-review-'+proposal,all=[...document.querySelectorAll('.card')];let saved={};try{saved=JSON.parse(localStorage.getItem(key)||'{}')}catch{}
function persist(){const decisions={};document.querySelectorAll('select[data-id]').forEach(s=>decisions[s.dataset.id]=s.value);saved={proposal,decisions,notes:document.querySelector('#notes').value};try{localStorage.setItem(key,JSON.stringify(saved))}catch{}}
document.querySelectorAll('select[data-id]').forEach(s=>{s.value=saved.decisions?.[s.dataset.id]||'unreviewed';s.addEventListener('change',persist)});document.querySelector('#notes').value=saved.notes||'';document.querySelector('#notes').addEventListener('input',persist);
function filter(){const role=document.querySelector('#role').value,split=document.querySelector('#split').value,limbs=document.querySelector('#limbs').value,newOnly=document.querySelector('#newOnly').checked;let n=0;all.forEach(c=>{c.hidden=!((role==='all'||c.dataset.role===role)&&(split==='all'||c.dataset.split===split)&&(!newOnly||c.dataset.new==='1')&&(limbs==='all'||c.dataset.frame===limbs||(limbs==='hands'&&c.dataset.hands==='1')||(limbs==='feet'&&c.dataset.feet==='1')));if(!c.hidden)n++});document.querySelector('#count').textContent=n+' 张可审阅'}
['role','split','newOnly','limbs'].forEach(id=>document.getElementById(id).addEventListener('change',filter));document.querySelectorAll('.role-link').forEach(a=>a.addEventListener('click',ev=>{ev.preventDefault();document.querySelector('#role').value=a.dataset.role;filter();document.querySelector('nav').scrollIntoView({behavior:'smooth'})}));filter();
document.querySelector('#export').addEventListener('click',()=>{persist();const url=URL.createObjectURL(new Blob([JSON.stringify({...saved,exportedAt:new Date().toISOString()},null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='dohna-v2-review-decisions.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)});
</script></html>'''
    for key,value in {'__TOTAL__':str(sum(s['train']+s['holdout'] for s in summaries)),
        '__RESERVE__':str(sum(s['reserve'] for s in summaries)),
        '__GAPCOUNT__':str(sum(not r['training_eligible'] for r in leads)),
        '__STATS__':stats,'__LIMB_STATS__':limb_stats,'__OPTIONS__':''.join(f'<option value="{r}">{n}</option>' for r,n in ROSTER.items()),'__CARDS__':''.join(cards),'__GAPS__':gaprows,'__FP__':fingerprint}.items():page=page.replace(key,value)
    (out/'index.html').write_text(page,encoding='utf-8')
    save(REVISION/'review-verification.json',{'status':'PASS','checked_at':datetime.now(timezone.utc).isoformat(),
        'source_and_output_hashes_checked':len(records),'group_leakage':False,'duplicate_visible_images':False,
        'low_resolution_battle_share':'<=25% per training proposal','html_images':len(records),'visual_limb_annotations_checked':len(records),
        'training_submitted':False,'user_approved':False})
    return manifest


def main():
    status=load(REVISION/'status.json')
    if status['allow_upload'] or status['allow_submit']:raise ValueError('Review gate must remain closed')
    candidates,reviews=make_proposal();records=prepare_outputs(candidates,reviews);manifest=render(records,unresolved())
    status['phase']='DATASET_REVIEW_WITH_SOURCE_GAPS';status['review_manifest']=str(REVISION/'review-manifest.json')
    status['proposal_fingerprint']=manifest['fingerprint'];save(REVISION/'status.json',status)
    print(json.dumps(manifest['summaries'],ensure_ascii=False,indent=2))
    print(REVISION/'review/index.html')


if __name__=='__main__':main()
