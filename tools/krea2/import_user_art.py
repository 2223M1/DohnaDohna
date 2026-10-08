"""Preserve the four supplied copies of previously verified X drawings."""
from pathlib import Path
import shutil
from datetime import datetime, timezone
from PIL import Image
from dataset import ROOT, load, save, sha

FILES = [
    ('porno','5446aa9b-9801-4ee9-91aa-3b7034344c1e','EnzuhRCUwAAzz1u','1332202174957248512','casual-release-promo'),
    ('medhico','0e9d2f20-0637-452a-a03b-cbb7d253b534','Ep7d5diVEAEsI2F','1341753737254060034','birthday-2020'),
    ('kirakira','bb92be11-c50c-4739-a597-40754b867784','EnQX1zzUcAIaFAc','1329714387993542656','casual-goggles-promo'),
    ('alyce','9d908652-628f-41ce-b29a-3f4f91688370','Ent5cNoVQAAQDd_','1331793034518753280','birthday-2020'),
]

def main():
    root=ROOT/'revisions/official-gyokai-v2'
    archive=root/'archive/pre-user-supplied-x-20261009'
    if not archive.exists():
        archive.mkdir(parents=True)
        for name in ('review-manifest.json','curation.json','status.json'):
            shutil.copy2(root/name,archive/name)
    dest=root/'official/user-supplied-x';dest.mkdir(parents=True,exist_ok=True)
    rows=[]
    for role,clipboard,media,post,group in FILES:
        original=Path('C:/Users/theon/AppData/Local/Temp')/f'codex-clipboard-{clipboard}.png'
        path=dest/f'{role}-{media}.png'
        if not path.exists():shutil.copy2(original,path)
        if sha(original)!=sha(path):raise ValueError(f'Imported source differs: {role}')
        with Image.open(path) as im:im.verify()
        with Image.open(path) as im:w,h=im.size;fmt=im.format
        rows.append({'id':f'{role}-artist-{media}','character':role,'source':str(path),
            'source_name':f'User-supplied copy of X media {media}', 'source_sha256':sha(path),
            'width':w,'height':h,'format':fmt,'kind':'official','group':f'artist/{role}-{group}',
            'page_url':f'https://x.com/_himehajime/status/{post}/photo/1',
            'observed_media_url':f'https://pbs.twimg.com/media/{media}?format=jpg&name=medium',
            'artist':'魚介 / おののいもこ','provenance':'User supplied image visually matches the previously inspected original artist X post. Local bytes are the attachment, not a claimed byte-identical CDN download.',
            'imported_at':datetime.now(timezone.utc).isoformat(),'decision':'pending',
            'source_policy':'OFFICIAL_OR_VERIFIED_ORIGINAL_ARTIST'})
    save(dest/'manifest.json',rows)
    print([(r['character'],r['width'],r['height']) for r in rows])

if __name__=='__main__':main()
