"""Collect observed Lycee official card references with explicit Gyokai credit.

Card frames are review references, not automatic training samples. The card IDs
were observed in the official artist search; never enumerate guessed card IDs.
"""
import argparse
import hashlib
import re
import urllib.request
from pathlib import Path
from html.parser import HTMLParser
from PIL import Image, ImageDraw, ImageFont
from dataset import ROOT, save, sha, FONT

CARDS={
 'alyce':['LO-4111-A','LO-4111','LO-3976-L','LO-3976'],
 'kikuchiyo':['LO-4110-A','LO-4110','LO-3975-X','LO-3975-S','LO-3975'],
 'antena':['LO-4004-A','LO-4004-K','LO-4004'],
 'medhico':['LO-4014-K','LO-4014'], 'kirakira':['LO-4008-K','LO-4008'],
 'joker':['LO-4009'],'tora':['LO-4007'],'zappa':['LO-4006'],'kuma':['LO-4005'],
 'porno':['LO-3974-S','LO-3974'],
}
SEARCH='https://lycee-tcg.com/card/?word=%E9%AD%9A%E4%BB%8B&limit=200'

class CardPage(HTMLParser):
    def __init__(self):super().__init__();self.text=[];self.images=[]
    def handle_data(self,data):self.text.append(data)
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='img' and a.get('src'):self.images.append(a['src'])

def fetch(url,path):
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists():
        with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'DohnaDohna-source-audit/1.0'}),timeout=30) as response:
            data=response.read(12*1024*1024+1)
        if len(data)>12*1024*1024:raise ValueError('Oversized reference')
        path.write_bytes(data)
    return path.read_bytes()

def collect(root):
    dest=root/'official/lycee';records=[]
    for role,ids in CARDS.items():
        for ident in ids:
            page=f'https://lycee-tcg.com/card/card_detail.pl?cardno={ident}'
            html=dest/'pages'/f'{ident}.html';raw=fetch(page,html)
            parser=CardPage();parser.feed(raw.decode('utf-8'))
            text=' '.join(parser.text)
            if not re.search(r'illust\s*:\s*魚介',text):raise ValueError(f'Missing artist credit: {ident}')
            image_url=f'https://lycee-tcg.com/card/image/{ident}.png'
            record={'id':ident,'character':role,'page_url':page,'page_sha256':sha(html),
                    'artist':'魚介','credit':'illust : 魚介','provenance':'Lycee official licensed card catalogue',
                    'search_url':SEARCH,'url':image_url,'source_policy':'OFFICIAL_OR_VERIFIED_ORIGINAL_ARTIST',
                    'training_decision':'PENDING_VISUAL_REVIEW'}
            # Review this character through safe source art, not a fetish-card export.
            if role=='porno':
                record['training_decision']='METADATA_ONLY_PENDING_NONEXPLICIT_SOURCE'
            else:
                if not any(ident+'.png' in s for s in parser.images):raise ValueError(f'Image not linked: {ident}')
                path=dest/'images'/f'{ident}.png';fetch(image_url,path)
                with Image.open(path) as im:record.update(width=im.width,height=im.height)
                record.update(path=str(path),sha256=sha(path))
            records.append(record);print(ident,record['training_decision'],flush=True)
            save(dest/'manifest.json',records)
    selected=[r for r in records if r.get('path')]
    font=ImageFont.truetype(str(FONT),18)
    for start in range(0,len(selected),10):
        batch=selected[start:start+10];canvas=Image.new('RGB',(1500,970),'white');draw=ImageDraw.Draw(canvas)
        for i,r in enumerate(batch):
            with Image.open(r['path']) as source:im=source.convert('RGB')
            im.thumbnail((280,410));x=(i%5)*300;y=(i//5)*485
            canvas.paste(im,(x+(300-im.width)//2,y));draw.text((x+5,y+417),r['id']+' '+r['character'],font=font,fill='black')
            draw.text((x+5,y+444),f"{r['width']}x{r['height']} / 魚介",font=font,fill='black')
        out=root/'review'/f'lycee-{start//10+1:02}.jpg';out.parent.mkdir(parents=True,exist_ok=True);canvas.save(out,quality=95)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT/'revisions/official-gyokai-v2');args=p.parse_args();collect(args.root)
