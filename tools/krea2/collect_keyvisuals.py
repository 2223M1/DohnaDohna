"""Public AliceSoft key visuals actually observed on the product homepage."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from PIL import Image
from collect_official import request
from dataset import ROOT, save, sha

def main(root=ROOT/'revisions/official-gyokai-v2'):
    observed=[('alyce','img_kv1.png'),('medhico','img_kv2.png'),('kirakira','img_kv3.png'),
              ('antena','img_kv4.png'),('kikuchiyo','img_kv5.png')]
    def get(item):
        role,name=item;url='https://www.alicesoft.com/dohnadohna/common/img/home/'+name
        path=root/'official/keyvisuals'/name;request(url,path)
        with Image.open(path) as im:w,h=im.size
        return {'id':role+'-official-keyvisual','character':role,'source':str(path),'source_name':url,
                'source_sha256':sha(path),'kind':'official','group':'keyvisual/'+role,'width':w,'height':h,
                'page_url':'https://www.alicesoft.com/dohnadohna/','artist':'魚介',
                'provenance':'AliceSoft product homepage character key visual; creator credited in staff list',
                'decision':'pending','source_policy':'OFFICIAL_OR_VERIFIED_ORIGINAL_ARTIST'}
    with ThreadPoolExecutor(max_workers=3) as pool:records=list(pool.map(get,observed))
    save(root/'official/keyvisuals/manifest.json',records)
    print([(r['character'],r['width'],r['height']) for r in records])

if __name__=='__main__':main()
