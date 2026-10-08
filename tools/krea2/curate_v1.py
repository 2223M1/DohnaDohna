"""Frozen selections made from the 2026-10-08 visual contact-sheet review.

This produces preparation candidates, not a claim that the LoRAs are trained.
Official website portraits were inspected and are derivative/multi-view images;
they remain provenance references rather than additional independent samples.
"""
from dataset import *

IDENTITY={
 'kuma':('a male character with short gray hair, turquoise and lime hair streaks, and teal eyes',
         'a blue jacket and blue trousers, black and white checkered scarf, gray sneakers',
         'a dark purple hood, goggles, teal tactical vest, purple gloves, dark trousers and knee guards'),
 'alyce':('a female character with long golden blonde hair, blunt bangs, cyan and pink heterochromatic eyes and black rabbit-ear hair ribbons',
          'a blue, white and black ruffled dress, puffed sleeves, black patterned stockings, rose-decorated boots, a pocket watch at the waist',
          'a blue, white and black ruffled dress, puffed sleeves, black patterned stockings, rose-decorated boots, a pocket watch at the waist'),
 'antena':('a female character with lime green bobbed hair, an upward curl, green eyes and cat-ear headphones',
           'a pink and black jacket, silver overalls with shorts, dark socks and pink shoes, a rectangular backpack',
           'a turquoise and purple cropped jacket and shorts, asymmetrical patterned boots, cat-ear headphones'),
 'tora':('a male character with spiky dark hair tipped in orange and yellow, amber eyes and a purple headband',
         'a green short-sleeved jacket, bright patterned shirt, green trousers and a yellow scarf',
         'a turquoise and black combat shirt, yellow trousers, yellow waist sash, dark boots and wrist guards'),
 'kikuchiyo':('a female character with long black hair with purple and pink highlights, red eyes and a gold hair ornament',
              'a mint blue school jacket, white blouse, black skirt with gold pattern, pale tights and a sheathed sword',
              'red and black armor, red leggings, dark pleated skirt, gloves and a katana'),
 'medhico':('a female character with teal hair, green eyes and a yellow hair ribbon',
            'glasses, a pink nursing uniform over a white short-sleeved blouse, teal necktie and pale tights',
            'long teal twin tails, a white and purple nurse cap, white and purple futuristic nurse outfit, purple leggings and white boots'),
 'joker':('a male character with short turquoise hair, yellow hair highlights and blue eyes',
          'a purple and blue zip-up hoodie, dark knee-length shorts and dark sneakers with pink details',
          'a backward pink cap with goggles, black patterned face covering, blue shirt, yellow and purple overshirt, dark shorts and a backpack'),
 'zappa':('a muscular male character with short spiky orange hair, shaved dark sides and pink-tinted sunglasses',
          'a red patterned short-sleeved shirt, dark trousers, white belt and white gloves',
          'a red sleeveless vest, belts, dark green trousers with red panels, heavy chain-wrapped forearms and pale gloves'),
 'kirakira':('a female character with bright pink twin tails, mint hair streaks, turquoise eyes and small black hair bows',
             'a yellow top with a black bow, purple and green jacket, teal pleated skirt and asymmetrical dark stockings',
             'a yellow top with a black bow, purple and green jacket, teal pleated skirt and asymmetrical dark stockings'),
 'porno':('a female character with a short silver bob, pastel pink and yellow hair streaks and pink eyes',
          'a pale blue sleeveless ruffled dress, black choker and pale shoes',
          'a pale blue sleeveless ruffled dress, black choker and pale shoes'),
}

# First-version coverage is conservative: no bondage costume or erotic framing.
CASUAL_ONLY={'antena','kirakira','porno'}
# Shared body drawings stay together even when the face or costume changes.
POSE_FAMILIES={
 'alyce':{'基本':'neutral','乐':'neutral','哀':'hands_up','怒':'hands_up','驚':'hands_up'},
 'antena':{'哀':'leaning','驚':'leaning'},
 'kikuchiyo':{'基本':'neutral','乐':'neutral','乐Ｂ':'neutral','哀':'turned','怒':'turned'},
 'medhico':{'怒':'raised','驚':'raised'},
 'joker':{'基本':'neutral','驚':'neutral','乐Ｂ':'sleeve','怒':'sleeve'},
 'zappa':{'基本':'neutral','乐Ｂ':'neutral'},
 'kirakira':{'基本':'neutral','驚':'neutral','哀':'leaning','怒':'leaning'},
 'porno':{'基本':'neutral','乐':'neutral','怒':'neutral','乐Ｂ':'close','哀':'close'},
}
UPPER_WEAR={
 'kuma':('a blue jacket and a black and white checkered scarf','a purple hood, goggles, a teal tactical vest and purple gloves'),
 'alyce':('a blue, white and black ruffled dress with puffed sleeves and black gloves',)*2,
 'antena':('a pink and black jacket, silver overalls and a rectangular backpack','a turquoise and purple jacket'),
 'tora':('a green short-sleeved jacket, bright patterned shirt and yellow scarf','a turquoise and black combat shirt and yellow waist sash'),
 'kikuchiyo':('a mint blue school vest and white blouse','red and black armor and gloves'),
 'medhico':('glasses, a pink uniform, white blouse and teal necktie','long teal twin tails, a white and purple nurse cap and a futuristic nurse outfit'),
 'joker':('a purple and blue zip-up hoodie','a backward pink cap with goggles, patterned face covering, blue shirt and yellow overshirt'),
 'zappa':('a red patterned shirt and white gloves','a red sleeveless vest, belts, chain-wrapped forearms and pale gloves'),
 'kirakira':('a yellow top with a black bow and a purple and green jacket',)*2,
 'porno':('a pale blue ruffled dress and black studded choker',)*2,
}
SELECTED_POSES={
 'kuma':{'基本':['基本','乐','哀','怒','驚'],'打斗':['基本','哀','怒','驚']},
 'alyce':{'基本':['基本','乐','哀','驚'],'打斗':['基本','哀','驚']},
 'antena':{'基本':['基本','乐','哀','怒','驚']},
 'tora':{'基本':['基本','乐Ｂ','哀','怒','驚'],'打斗':['基本','哀','怒','驚']},
 'kikuchiyo':{'基本':['基本','乐','哀','怒','驚'],'战Ｂ':['基本','哀','怒'],'打斗':['乐Ｂ','驚']},
 'medhico':{'基本':['基本','乐','哀','怒','驚'],'打斗':['基本','乐','哀','怒','驚']},
 'joker':{'基本':['基本','乐Ｂ','哀','怒','驚'],'打斗':['基本','哀','怒','驚']},
 'zappa':{'基本':['基本','乐Ｂ','哀','怒','驚'],'打斗':['基本','哀','怒','驚']},
 'kirakira':{'基本':['基本','乐','乐Ｂ','哀','怒','驚']},
 'porno':{'基本':['基本','乐','乐Ｂ','哀','怒','驚']},
}

POSTURES={
 'kuma':{'基本':'standing with one hand at his scarf','乐':'smiling with eyes closed and one hand at his scarf',
         '哀':'leaning forward with one arm held across his torso, uneasy expression','怒':'standing with arms crossed and an irritated expression',
         '驚':'leaning forward with both hands raised, surprised expression'},
 'alyce':{'基本':'standing with a calm expression','乐':'standing with a closed-eye smile',
          '哀':'raising both hands near her face with a distressed expression','怒':'clenching both fists near her face, angry cartoon expression',
          '驚':'holding both hands up, round eyes and surprised expression'},
 'antena':{'基本':'standing with a confident smile and one hand at her hip','乐':'standing with folded arms and closed eyes',
           '哀':'leaning forward with hands lowered and a distressed expression','怒':'clenching both fists with a comically angry expression',
           '驚':'leaning forward with wide eyes and a surprised expression'},
 'tora':{'基本':'standing with one hand behind his head','乐Ｂ':'grinning and raising a clenched fist',
         '哀':'slouching forward with one hand lowered and a worried expression','怒':'raising a clenched fist with a determined expression',
         '驚':'raising open hands in surprise'},
 'kikuchiyo':{'基本':'standing calmly with her sword','乐':'standing with one hand near her mouth and a closed-eye smile',
              '乐Ｂ':'lifting a fox mask above her face with one hand while holding her sword',
              '哀':'turning her upper body sideways, looking back over her shoulder','怒':'turning sideways with one hand on the sword',
              '驚':'holding her sword across her body with wide eyes and a surprised expression'},
 'medhico':{'基本':'standing with one hand near her chin','乐':'smiling with closed eyes and clasped hands near her face',
            '哀':'standing with hands near her chest and a worried expression','怒':'raising her hands with an upset expression',
            '驚':'raising her hands with a startled expression'},
 'joker':{'基本':'standing with both hands raised at chest height','乐Ｂ':'smiling and rolling up one sleeve',
          '哀':'holding his head in both hands with a distressed expression','怒':'rolling up one sleeve with a determined expression',
          '驚':'standing stiffly with a shocked expression'},
 'zappa':{'基本':'standing with hands at his waist and a confident grin','乐Ｂ':'standing with hands at his waist, head turned sideways',
          '哀':'leaning forward with a gloved hand near his mouth','怒':'pressing his gloved fists together with an irritated expression',
          '驚':'raising both gloved hands and shouting in surprise'},
 'kirakira':{'基本':'standing and holding the sides of her jacket','乐':'smiling with closed eyes and one hand raised',
             '乐Ｂ':'standing with a hand near the back of her neck and a closed-eye grin',
             '哀':'leaning forward, looking concerned','怒':'leaning forward and raising a hand near her chest, angry expression',
             '驚':'holding her jacket and looking startled'},
 'porno':{'基本':'standing with one finger near her mouth','乐':'standing and pointing to the side',
          '乐Ｂ':'raising a finger near her cheek, thoughtful expression','哀':'standing with folded arms and an unhappy expression',
          '怒':'holding her hands up near her chin with an annoyed expression',
          '驚':'raising both open hands, startled expression'},
}

# Individual animation frames actually inspected, with sequence-level holdout.
BATTLE={
 'kuma': [('kuma-020a315970','train','jumping with bent knees and aiming a pistol sideways'),
          ('kuma-d999ef3de2','train','lunging sideways with a pistol extended behind his body'),
          ('kuma-17e43f9037','holdout','leaping sideways with arms outstretched and pistols raised')],
 'alyce':[('alyce-1891b59866','train','jumping with a rifle raised, hair flying upward'),
          ('alyce-ef5f6d4257','train','seated beside a small tea table, smiling with eyes closed'),
          ('alyce-2abe9b48f8','holdout','leaning forward and extending both gloved hands')],
 'tora':[('tora-53dda9db1d','train','jumping with bent knees and holding a long staff'),
         ('tora-cda1c9d3af','train','turning sideways with a segmented staff in both hands'),
         ('tora-e8a7da7133','holdout','holding a long staff overhead in both hands')],
 'kikuchiyo':[('kikuchiyo-c9ef906fed','train','jumping with bent knees, gripping a sheathed katana'),
              ('kikuchiyo-b076f656dd','train','crouching low and extending a katana sideways'),
              ('kikuchiyo-c27dfc923a','holdout','swinging a katana with a red curved slash effect')],
 'medhico':[('medhico-3878ee1ab3','train','jumping with both hands raised beside her head'),
            ('medhico-777ce5add1','train','lifting a pink medical case over her head'),
            ('medhico-952b09969d','holdout','leaping sideways with both arms spread')],
 'joker':[('joker-d7564a7d7b','train','jumping and holding a baseball bat overhead'),
          ('joker-94154a746b','train','seen from behind while swinging a baseball bat'),
          ('joker-0ff5f62cb2','holdout','performing a high kick with one leg extended')],
 'zappa':[('zappa-29e12598fe','train','jumping with one knee raised and both fists forward'),
          ('zappa-1d3b91092c','train','leaning into a punch with chain-wrapped forearms'),
          ('zappa-5fc704d212','holdout','turning his back and raising one arm overhead')],
}


def main(root=ROOT):
    candidates=load(root/'candidates.json');byid={r['id']:r for r in candidates};curation=[]
    def add(r,split,description,group=None,crop=None,outfit='casual'):
        role=r['character'];core,casual,battle=IDENTITY[role]
        wear=UPPER_WEAR[role][outfit!='casual']
        # Do not label tiny sprite eyes or covered faces with imagined detail.
        if r['kind']=='battle' or 'closed' in description or 'mask covering' in description:
            core=re.sub(r',? (?:and )?(?:cyan and pink heterochromatic|teal|blue|green|amber|red|pink) eyes','',core)
        caption=f'dohna_{role}, {outfit} outfit, {core}, wearing {wear}, {description}'
        caption+=', colorful anime character illustration, bold dark outlines and vivid accent colors.'
        curation.append({'id':r['id'],'split':split,'group':group or r['group'],
                         'caption':caption,'reviewed_nonexplicit':True,'reviewer':'Codex visual review 2026-10-08',
                         'crop':crop,'outfit':outfit})
    for role,outfits in SELECTED_POSES.items():
        for variant,expressions in outfits.items():
            for expression in expressions:
                r=next(x for x in candidates if x['character']==role and x['kind']=='portrait'
                       and x['source_name'].rsplit('.',1)[0]==f'立绘／{ROSTER[role]}／{variant}／{expression}')
                pose=expression
                if expression=='乐':pose='neutral' if role in ('kuma','alyce','kikuchiyo') else expression
                if expression=='基本':pose='neutral'
                if role in ('tora','joker') and expression=='乐Ｂ':pose='怒'
                pose=POSE_FAMILIES.get(role,{}).get(expression,pose)
                held_pose={'antena':'leaning','medhico':'raised'}.get(role,'驚')
                split='holdout' if pose==held_pose else 'train'
                if role in ('alyce','joker','kirakira'):split='train'
                group=f'portrait/{role}/{pose}'
                # Different official clothing drawings share a pose group across splits.
                description=POSTURES[role][expression]+', character portrait, plain light gray background'
                if role=='alyce':description+=', holding a purple parasol' if variant=='基本' and expression in ('基本','乐') else ', carrying a long black rifle' if variant=='打斗' else ''
                if role=='kikuchiyo' and variant=='战Ｂ':description+=', black fox mask covering the face'
                if role=='joker' and variant=='打斗':description+=', holding a yellow baseball bat'
                if role=='medhico' and variant=='打斗' and expression in ('怒','驚'):description+=', carrying a pink medical case'
                add(r,split,description,group,outfit='casual' if variant=='基本' else 'battle')
    for role,choices in BATTLE.items():
        for ident,split,description in choices:
            add(byid[ident],split,description+', full-body game sprite on a light gray background',outfit='battle')
    add(byid['alyce-8aa18f646f'],'train','holding a long rifle diagonally, alert expression, dramatic tilted view with red and purple lighting',outfit='battle')
    add(byid['antena-4fffa5cb9b'],'train','looking toward the viewer with a subdued expression, a suited man behind her with his hand on her shoulder, close-up indoor view with warm yellow lighting')
    add(byid['kikuchiyo-c19dd9c827'],'holdout','looking through a rain-covered window with one hand held against the glass, blue and green lighting')
    add(byid['kirakira-68ddf37115'],'holdout','looking upward and winking as another person pats her head, high-angle view in a warm indoor scene')
    # Safe meal CG contributes a genuinely new view for the casual-only character.
    extra=list(csv.DictReader((ORIGINAL/'assets/manifest.csv').open(encoding='utf-8-sig')))
    selected=[('kuma','事件／主要／道路別.qnt',[660,80,1220,720],'train','seated inside a vehicle and looking sideways, scarf around his neck, a foreground arm and steering wheel, warm light through the windows','battle'),
              ('porno','事件／主要／饭菜.qnt',[650,200,1120,720],'train','sitting at a dining table with half-closed eyes, eating from a spoon, food on a plate in the foreground, parts of other people behind her, warm indoor lighting','casual')]
    for role,name,crop,split,description,outfit in selected:
        row=next(r for r in extra if r['original_name']==name and '\\current\\' in r['output'])
        r={'id':role+'-extra-'+hashlib.sha256(name.encode()).hexdigest()[:10], 'character':role,
           'source':str(ORIGINAL/row['output']),'source_name':name,'source_sha256':row['sha256'],
           'source_package':row['source'],'source_index':int(row['index']), 'kind':'scene',
           'group':name.rsplit('.',1)[0].replace('／','/'),'width':int(row['width']),'height':int(row['height']), 'decision':'pending'}
        candidates=[x for x in candidates if x['id']!=r['id']]+[r]
        add(r,split,description,crop=crop,outfit=outfit)
    # Close-up CGs show only a subset of the character reference checklist.
    scene_wear={
        'alyce-8aa18f646f':'a ruffled dress with puffed sleeves and black gloves',
        'antena-4fffa5cb9b':'cat-ear headphones, a pink and black jacket and silver overall straps',
        'kikuchiyo-c19dd9c827':'a mint school vest and white blouse',
        'kirakira-68ddf37115':'a yellow top with a black bow and teal pleated skirt',
        'kuma-extra-567ade5d1d':'a raised dark collar, scarf and teal tactical vest',
        'porno-extra-7b8e21c405':'a pale ruffled dress and black studded choker',
    }
    for r in curation:
        if r['id'] in scene_wear:
            role=r['id'].split('-')[0]
            old=UPPER_WEAR[role][r['outfit']!='casual']
            r['caption']=r['caption'].replace('wearing '+old,'wearing '+scene_wear[r['id']])
    # Vary only the neutral alpha-composite color, without duplicating samples.
    # Keep original opaque scene backgrounds and use the same color for each
    # pose family so expression variants cannot become independent evidence.
    neutral=[('light gray',(240,240,240)),('muted blue gray',(219,228,236)),('warm off-white',(245,239,229))]
    for role in ROSTER:
        groups=sorted({r['group'] for r in curation if r['id'].startswith(role+'-')})
        for r in curation:
            if not r['id'].startswith(role+'-'):
                continue
            if 'light gray background' in r['caption']:
                name,color=neutral[groups.index(r['group'])%len(neutral)]
                r['background_rgb']=list(color)
                r['caption']=r['caption'].replace('light gray background',name+' background')
            if role=='kuma' and r['group'].endswith('/怒'):
                r['caption']=r['caption'].replace('irritated expression','irritated expression, small angular cartoon marks beside his head')
    save(root/'candidates.json',candidates)
    save(root/'curation.json',curation)
    save(root/'character-features.json',{role:{'display_name':ROSTER[role], 'identity':list(IDENTITY[role]),
                'casual_only':role in CASUAL_ONLY,'official_reference':'official/manifest.json',
                'coverage_limitations':['mostly front and three-quarter views','few independent full-resolution action drawings',
                                        'web portraits repeat in-game drawings'],
                'visual_reference':f'review/{role}-portrait-01.jpg'} for role in ROSTER})
    selected_ids={r['id'] for r in curation}
    save(root/'not-selected.json',[{'id':r['id'],'reason':('official derivative or multi-view reference; not an independent sample' if r['kind']=='official'
               else 'not selected: redundant pose, low detail, effect-only, alternate outfit, chibi, multi-character or outside conservative first-version coverage')}
               for r in candidates if r['id'] not in selected_ids])
    print(json.dumps(dict(collections.Counter(byid.get(r['id'],{'character':r['id'].split('-')[0]})['character'] for r in curation)),ensure_ascii=False))


if __name__=='__main__':main()
