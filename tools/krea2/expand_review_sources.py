"""Reviewed source additions, deterministic packaging correction and crops.

No inpainting, invented character detail, uploads or training. Originals and
their hashes stay intact. All derived images have explicit parent receipts.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from dataset import ROOT, ROSTER, FONT, load, save, sha

REV=ROOT/'revisions/official-gyokai-v2'
X_ART=[
    ('alyce','EqkyFYMVgAEKem4','1344661076634345475','medium','new-year-2021',[430,0,1000,853],'train',
     'long blonde hair with braided sidelocks, black rabbit-ear ribbons, visible cyan eye, blue and white ruffled dress over a black high collar, black gloves, leaning sideways with raised hands, framing to the upper thighs, another person\'s forearm at the left edge, bright pink background with New Year lettering',
     '保留右侧爱丽丝原画高度、双手和腰腿；左缘有另一角色的手臂但不含其脸。原图只到大腿，并无足部；年贺文字保留。'),
    ('porno','EkbypiqU0AA4QJ5','1317007173667287042','900x900','reservation-promo-2020',None,'train',
     'short silver hair with pastel pink and yellow streaks, pink eyes, a black studded choker and pale blue floral-trimmed dress, one hand cupped beside her mouth, looking toward the viewer, upper-body portrait, bright geometric background with Japanese promotional text',
     '独立预约宣传绘；保留完整原画，底部标题及背景文字保留，原图本来就是半身。'),
    ('porno','EoidN8AU0AExiWi','1335490612179607552','900x900','deluxe-promo-2020',[210,0,623,380],'reserve',
     'close-up face, silver hair with pastel streaks, pink eye, pale head straps and an earpiece, tongue slightly out, tilted head, bright pink patterned background',
     '仅非露骨头部裁切。表情偏强且原图已截头顶，暂列备选，不计入训练。'),
    ('porno','EqFldh9VoAMePOp','1342465774313566209','medium','christmas-2020',[150,30,510,350],'reserve',
     'close-up face, silver hair with pastel streaks, pink eye, pale head straps, red and white fur-trimmed festive hat with holly, three-quarter view',
     '仅非露骨头部裁切。左侧轮廓不完整、节日服饰和有效面积受限，暂列备选，不计入训练。'),
]
# All six saved manufacturer pages state H210 mm x W145 mm x D85 mm.
# The old 574:914 destination kept the photographed horizontal compression.
FRONT_SIZE=(638,924)  # exact 145:210 aspect; geometric resampling, not new detail
FRONT_CORNERS=[(64,69),(69,947),(643,974),(638,51)]  # TL, BL, BR, TR
PACK_CROPS={role:[0,0,*FRONT_SIZE] for role in ('alyce','antena','kikuchiyo','medhico','porno')}
PACK_CROPS['kirakira']=[0,0,715,1000]
PACK_CAPTIONS={
    'alyce':'long blonde hair, rabbit-ear ribbons and two braided sidelocks, cyan and pink eyes, black gloves held against both cheeks, open-mouth smile, blue and white ruffled dress, upper-body portrait',
    'antena':'lime green bobbed hair, green eyes, cat-ear headphones, using both index fingers to pull the corners of her mouth, elbows spread, teal and purple cropped jacket, shorts, patterned teal glove and wrist device, framing to the hips',
    'kikuchiyo':'long dark hair with purple and pink highlights, red eyes, gold hair ornament, red and black armor, looking forward with a calm expression, a black fox mask beside her head, upper-body portrait',
    'medhico':'teal twin tails with pink highlights, green eyes, white and purple nurse cap, gloved hands raised beside her shoulders, bent arms, open mouth and surprised expression, white and purple nurse outfit, framing to the hips',
    'kirakira':'pink and yellow twin tails, turquoise eye, winking, gloved fingers raised beside her cheek and the other hand at her hip, a purple cropped jacket over a teal top, a purple respirator resting near her neck, purple legwear and belt, framing to the upper thighs',
    'porno':'short silver hair with pink and yellow streaks, pink eye, pale head straps and side earpiece, dark gloves held near her mouth and beside her eye, both bent arms visible, pale armored sleeves and pink body straps, framing to the upper thighs',
}

def perspective_coefficients():
    # Destination rectangle corners -> measured front-plane corners in the
    # common 797 x 1024 manufacturer renders. Never include the box side.
    w,h=FRONT_SIZE
    dest=[(0,0),(0,h),(w,h),(w,0)]
    src=FRONT_CORNERS
    a=[];b=[]
    for (x,y),(u,v) in zip(dest,src):
        a.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]])
        b.extend([u,v])
    return np.linalg.solve(np.array(a,float),np.array(b,float)).tolist()

def main():
    dest=REV/'official/review-supplements';dest.mkdir(parents=True,exist_ok=True)
    rows=[]
    descriptions={
        'alyce':('holdout','casual','long blonde hair, black rabbit-ear ribbons, cyan and pink eyes, black gloves holding the ends of two braids near her cheeks, open-mouth smile, blue and white ruffled dress, bright green, black and pink geometric background'),
        'medhico':('train','casual','teal ponytail with a large yellow bow, glasses, green eyes, a pink vest over a white short-sleeved blouse, teal and yellow necktie, one hand raised near her collar, gentle smile, white background'),
        'kirakira':('train','casual','pink and yellow twin tails, turquoise eyes, lifting heart-shaped goggles with one hand and giving a thumbs-up with the other, smiling, yellow top and purple and teal jacket, pink geometric background'),
        'porno':('train','casual','short silver hair with pastel pink and yellow streaks, pink eyes, black studded choker, pale blue floral-trimmed dress, one hand beside her mouth and the other open at chest height, colorful geometric background with a small decorative Japanese glyph'),
    }
    for r in load(REV/'official/user-supplied-x/manifest.json'):
        split,outfit,description=descriptions[r['character']]
        r['review']={'split':split,'outfit':outfit,'crop':None,'change':'new_official_art',
            'caption':f"dohna_{r['character']}, {outfit} outfit, {description}, colorful anime illustration.",
            'selection_note':'用户提供文件，与已核实鱼介X原帖构图对应；此前下载失败缺口已解决。附件散列独立记录。'}
        rows.append(r)
    for role,key,post,size,group,crop,split,description,note in X_ART:
        path=REV/f'official/artist-x/{key}.jpg'
        with Image.open(path) as im:w,h=im.size;im.verify()
        rows.append({'id':f'{role}-artist-{key}','character':role,'source':str(path),
            'source_name':f'https://pbs.twimg.com/media/{key}?format=jpg&name={size}',
            'source_sha256':sha(path),'width':w,'height':h,'kind':'official',
            'group':f'artist/{role}-{group}','page_url':f'https://x.com/_himehajime/status/{post}/photo/1',
            'artist':'魚介 / おののいもこ','provenance':'Original author post inspected in the browser; downloaded the exact public image URL through Windows certificate-validated HTTPS.',
            'review':{'split':split,'outfit':'casual' if split=='train' else 'alternate_face_only',
                'crop':crop,'change':'new_artist_crop','caption':f'dohna_{role}, {description}, colorful anime illustration.',
                'selection_note':note}})
    coeff=perspective_coefficients();comparisons=REV/'review/edits';comparisons.mkdir(exist_ok=True)
    font=ImageFont.truetype(str(FONT),17)
    for original in load(REV/'official/packaging/fronts/manifest.json'):
        role=original['character'];crop=PACK_CROPS[role]
        parent=Path(original['source']);page=original['page_url'];url=original['source_name']
        if role=='kirakira':
            parent=REV/'official/packaging/kirakira-flat-x.jpg'
            page='https://x.com/_himehajime/status/1351726953754103808/photo/1'
            url='https://pbs.twimg.com/media/EsJMXPwVoAEPbbq?format=jpg&name=medium'
        with Image.open(parent) as im:raw=im.convert('RGB')
        if role=='kirakira':
            corrected=raw;before=raw;operations=['retain_original_flat_frame'];mapping=None
        else:
            if raw.size!=(797,1024):raise ValueError('Manufacturer render changed')
            page_html=REV/f'official/packaging/fronts/{role}-page.html'
            if 'H210mm&times;W145mm&times;D85mm' not in page_html.read_text(encoding='utf-8-sig'):
                raise ValueError(f'Unverified front aspect ratio: {role}')
            corrected=raw.transform(FRONT_SIZE,Image.Transform.PERSPECTIVE,coeff,Image.Resampling.BICUBIC)
            before=raw.copy();ImageDraw.Draw(before).line(FRONT_CORNERS+[FRONT_CORNERS[0]],fill=(0,200,255),width=3)
            operations=['front_plane_perspective_correction','restore_manufacturer_145_to_210_aspect','retain_entire_front'];mapping=coeff
        edited=corrected.crop(crop);path=dest/f'{role}-tamatoys-crop.png';edited.save(path)
        comparison=Image.new('RGB',(850,720),(245,246,248));draw=ImageDraw.Draw(comparison)
        for j,(pic,label) in enumerate([(before,'斜拍原图 · 蓝框为正面四角' if role!='kirakira' else '鱼介平面图 · 原始完整画幅'),(edited,'完整正面 · 145:210 比例' if mapping else '原比例保留 · 无透视处理')]):
            preview=pic.copy();preview.thumbnail((410,655));comparison.paste(preview,(j*425+(425-preview.width)//2,45));draw.text((j*425+10,12),label,font=font,fill='black')
        compare_path=comparisons/f'{role}-packaging-comparison.jpg';comparison.save(compare_path,quality=93)
        rows.append({'id':f'{role}-tamatoys-crop','character':role,'source':str(path),
            'source_name':url,'source_sha256':sha(path),'width':edited.width,'height':edited.height,
            'kind':'official','group':f'packaging/tamatoys/{role}','page_url':page,'artist':'魚介',
            'provenance':'Officially credited distinct Tamatoys commission; character crop from original artist flat packaging image or manufacturer front. Printed background labels remain. No generated replacement detail.',
            'parent_source':str(parent),'parent_source_sha256':sha(parent),'parent_size':list(raw.size),
            'edit_receipt':{'operations':operations,'front_size':list(FRONT_SIZE) if mapping else list(raw.size),'source_front_corners_tl_bl_br_tr':FRONT_CORNERS if mapping else None,'front_physical_mm':[145,210] if mapping else None,'aspect_evidence':str(page_html) if mapping else page,'inverse_perspective_coefficients':mapping,'crop_after_correction':crop,'inpainting':False,'text_policy':'Retain the full front including title and labels to preserve limbs. Remove only the box side and outer whitespace. Never synthesize obscured anatomy.'},
            'comparison_url':compare_path.relative_to(REV/'review').as_posix(),
            'review':{'split':'train','outfit':'battle' if role!='alyce' else 'casual',
                'crop':None,'change':'licensed_packaging_crop',
                'caption':f'dohna_{role}, {PACK_CAPTIONS[role]}, colorful geometric background with a large Japanese title and printed product labels, anime illustration.',
                'selection_note':('鱼介原帖平面图，保留完整原画比例。' if role=='kirakira' else '按厂家盒面145:210比例校正四角透视和横向压缩，保留完整正面。')+'保留原有手臂、躯干与腿部；文字未去除；原画无足部，不算全身图。同包装版本不另算一张。'}})
    save(dest/'manifest.json',rows)
    print('Reviewed supplements:',len(rows))
    print([(r['character'],r['id'],r['review']['split']) for r in rows])

if __name__=='__main__':main()
