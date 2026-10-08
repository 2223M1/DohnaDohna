"""Record actual local model hashes and the public platform preset, no credentials."""
from datetime import datetime, timezone
from pathlib import Path
import json
import platform
import subprocess
import sys
import urllib.request
from dataset import ROOT, save, sha
from evaluate import MUSUBI, MODELS


def main():
    files={'raw':MODELS/'raw.safetensors','turbo':MODELS/'turbo.safetensors',
           'text_encoder':MODELS/'qwen3vl_4b_bf16.safetensors',
           'vae':Path('D:/AI/ComfyUI-aki/models/vae/qwen_image_vae.safetensors')}
    records={}
    for key,path in files.items():
        records[key]={'path':str(path),'bytes':path.stat().st_size,'sha256':sha(path)}
        print('hashed',key,records[key]['sha256'],flush=True)
    result={'recorded_at':datetime.now(timezone.utc).isoformat(),'python':sys.version,
            'platform':platform.platform(),'models':records,
            'musubi_commit':subprocess.check_output(['git','-C',str(MUSUBI),'rev-parse','HEAD'],text=True).strip(),
            'musubi_changes':subprocess.check_output(['git','-C',str(MUSUBI),'status','--porcelain'],text=True).splitlines(),
            'gpu':subprocess.check_output(['nvidia-smi','--query-gpu=name,memory.total,driver_version','--format=csv,noheader'],text=True).strip()}
    save(ROOT/'validation/environment.json',result)
    url='https://www.modelscope.cn/api/v1/muse/train/queryPreset?modelType=IMAGE'
    try:
        with urllib.request.urlopen(url,timeout=25) as response:data=json.load(response)
        rows=data['Data']['data']
        preset=[r for r in rows if r.get('styleType')=='KREA_2_TURBO']
        if not preset:raise ValueError('Krea2 preset not returned')
        save(ROOT/'validation/modelscope-public-preset.json',{'source':url,'retrieved_at':datetime.now(timezone.utc).isoformat(),
             'presets':preset,'account_balance':None,'current_quote':None,'accepted_run_parameters':None})
    except (OSError,ValueError,KeyError) as exc:
        save(ROOT/'validation/modelscope-preset-fetch-failure.json',{'source':url,'error':str(exc)})
        raise


if __name__=='__main__':main()
