"""Download explicitly observed, visually reviewed public illustrator files.

No account/session access, guessed originals, upscaling or frame removal.
New sources must be checked in the original author's page before adding here.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import io
from pathlib import Path
import urllib.request
from PIL import Image
from dataset import ROOT, load, save, sha

OBSERVED = [
    ('antena','E0ihsxVVcAQVAvB','1389538319529172992',1,'casual-promo'),
    ('medhico','E0ihtgMVEAAcuph','1389538319529172992',2,'casual-promo'),
    ('kikuchiyo','E0V-QidVgAAu8m7','1388654937349918721',1,'casual-sword-promo'),
    ('alyce','Ent5cNoVQAAQDd_','1331793034518753280',1,'birthday-2020'),
    ('kirakira','EnQX1zzUcAIaFAc','1329714387993542656',1,'casual-goggles-promo'),
    ('porno','EnzuhRCUwAAzz1u','1332202174957248512',1,'casual-release-promo'),
    ('medhico','Ep7d5diVEAEsI2F','1341753737254060034',1,'birthday-2020'),
]

def main(root=ROOT/'revisions/official-gyokai-v2'):
    dest=root/'official/artist-x'
    dest.mkdir(parents=True,exist_ok=True)
    def collect(item):
        role,key,post,photo,group=item
        url=f'https://pbs.twimg.com/media/{key}?format=jpg&name=medium'
        path=dest/(key+'.jpg')
        if not path.exists():
            for attempt in range(3):
                try:
                    req=urllib.request.Request(url,headers={'User-Agent':'DohnaDohna-source-review/1.0'})
                    with urllib.request.urlopen(req,timeout=30) as response:
                        if not response.url.startswith('https://pbs.twimg.com/media/'):
                            raise ValueError('Unexpected media redirect')
                        data=response.read(25*1024*1024+1)
                    if len(data)>25*1024*1024:raise ValueError('Oversized image')
                    with Image.open(io.BytesIO(data)) as im:im.verify()
                    path.write_bytes(data)
                    break
                except (OSError,urllib.error.URLError):
                    if attempt==2:raise
        with Image.open(path) as im:w,h=im.size
        return {'id':role+'-artist-'+key,'character':role,'source':str(path),
                'source_name':url,'source_sha256':sha(path),'width':w,'height':h,
                'kind':'official','group':'artist/'+role+'-'+group,
                'page_url':f'https://x.com/_himehajime/status/{post}/photo/{photo}',
                'artist':'魚介 / おののいもこ',
                'provenance':'Verified original illustrator public X artwork; exact file exposed by photo viewer. Public display resolution, not claimed master file.',
                'decision':'pending'}
    rows=[]
    failures=[]
    # Preserve each successful source even if a different CDN request fails.
    # Missing files remain research leads, never training candidates.
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures=[(item,pool.submit(collect,item)) for item in OBSERVED]
        for item,future in futures:
            try:
                rows.append(future.result())
            except (OSError,urllib.error.URLError,ValueError) as error:
                role,key,post,photo,group=item
                failures.append({'character':role,'media_id':key,
                    'page_url':f'https://x.com/_himehajime/status/{post}/photo/{photo}',
                    'status':'LOCATED_DOWNLOAD_FAILED','error':str(error)})
            save(dest/'manifest.json',rows)
    save(dest/'manifest.json',rows)
    save(dest/'acquisition-status.json',{'checked_at':datetime.now(timezone.utc).isoformat(),
         'saved':len(rows),'failed':failures})
    print([(r['character'],r['width'],r['height']) for r in rows])
    if failures:
        print('Download failures:',failures)

if __name__=='__main__':main()
