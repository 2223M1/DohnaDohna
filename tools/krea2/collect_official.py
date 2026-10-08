"""Collect the nine publicly listed squad character portraits from AliceSoft."""
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import urllib.parse
import urllib.request

from PIL import Image
from dataset import ROOT, save

BASE='https://www.alicesoft.com'
PAGE='/dohnadohna/chara/nayuta/nayuta01.html'
PAGES={'nayuta01':'kuma','nayuta02':'zappa','nayuta03':'porno','nayuta04':'kirakira',
       'nayuta05':'tora','flattt03':'alyce','shinonomeha01':'kikuchiyo','question01':'medhico','question02':'antena'}


class Assets(HTMLParser):
    def __init__(self):
        super().__init__();self.links=[];self.images=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='a' and a.get('href'):self.links.append(a['href'])
        if tag=='img' and a.get('src'):self.images.append(a)


def request(url,path):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists():
        req=urllib.request.Request(url,headers={'User-Agent':'DohnaDohna-asset-research/1.0'})
        with urllib.request.urlopen(req,timeout=30) as r:
            if urllib.parse.urlparse(r.url).hostname not in ('www.alicesoft.com','alicesoft.com'):
                raise ValueError('Unexpected download host')
            data=r.read(25*1024*1024+1)
            if len(data)>25*1024*1024:raise ValueError('Oversized asset')
        path.write_bytes(data)
    return path.read_bytes()


def collect():
    dest=ROOT/'official';index=request(BASE+PAGE,dest/'pages/nayuta01.html')
    parser=Assets();parser.feed(index.decode('utf-8'));urls={Path(x).stem:urllib.parse.urljoin(BASE,x) for x in parser.links}
    records=[]
    for key,role in PAGES.items():
        url=urls[key]
        raw=request(url,dest/'pages'/f'{key}.html');p=Assets();p.feed(raw.decode('utf-8'))
        img=next(x for x in p.images if Path(x['src']).name==f'img_{key}.png')
        asset=urllib.parse.urljoin(BASE,img['src']);path=dest/'images'/f'{role}-{key}.png'
        data=request(asset,path)
        with Image.open(path) as im:w,h=im.size
        records.append({'character':role,'page_url':url,'url':asset,'alt':img.get('alt'),
                        'path':str(path),'sha256':hashlib.sha256(data).hexdigest(),'width':w,'height':h,
                        'group':f'portrait-default-{role}','provenance':'AliceSoft official character page'})
        print(role,w,h,flush=True)
    save(dest/'manifest.json',records)


if __name__=='__main__':collect()
