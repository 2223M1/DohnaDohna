"""Strict ModelScope PEFT -> Musubi Krea2 conversion, adapted from MS2000.

Mapping reference: user-owned krea2-lora-training-audit,
evidence/remote/training-v2/convert_modelscope_krea2_lora.py.
No historical scripts are imported or executed.
"""
import argparse
import json
from pathlib import Path
import re

import torch
from safetensors import safe_open
from safetensors.torch import load_file, save_file

from dataset import sha, save


ATTENTION = {'to_q':'wq','to_k':'wk','to_v':'wv','to_gate':'gate','to_out.0':'wo'}


def expected_modules():
    fixed={'first','tmlp.0','tmlp.2','txtmlp.1','txtmlp.3','tproj.1','last.linear','txtfusion.projector'}
    blocks=[f'blocks.{i}' for i in range(28)]
    blocks += [f'txtfusion.{group}.{i}' for group in ('layerwise_blocks','refiner_blocks') for i in range(2)]
    return fixed | {f'{b}.{leaf}' for b in blocks for leaf in
                    ('attn.wq','attn.wk','attn.wv','attn.gate','attn.wo','mlp.gate','mlp.up','mlp.down')}


def map_module(name):
    fixed={'img_in':'first','time_embed.linear_1':'tmlp.0','time_embed.linear_2':'tmlp.2',
           'txt_in.linear_1':'txtmlp.1','txt_in.linear_2':'txtmlp.3','time_mod_proj':'tproj.1',
           'final_layer.linear':'last.linear','text_fusion.projector':'txtfusion.projector'}
    if name in fixed:return fixed[name]
    match=re.fullmatch(r'transformer_blocks\.(\d+)\.(attn|ff)\.(.+)',name)
    if match:
        index,section,leaf=match.groups()
        if section=='attn' and leaf in ATTENTION:return f'blocks.{index}.attn.{ATTENTION[leaf]}'
        if section=='ff' and leaf in ('gate','up','down'):return f'blocks.{index}.mlp.{leaf}'
    match=re.fullmatch(r'text_fusion\.(layerwise_blocks|refiner_blocks)\.(\d+)\.(attn|ff)\.(.+)',name)
    if match:
        group,index,section,leaf=match.groups()
        if section=='attn' and leaf in ATTENTION:return f'txtfusion.{group}.{index}.attn.{ATTENTION[leaf]}'
        if section=='ff' and leaf in ('gate','up','down'):return f'txtfusion.{group}.{index}.mlp.{leaf}'
    raise ValueError(f'Unknown Krea2 module: {name}')


def validate_pair(module, pair, shape, rank):
    if set(pair)!={'A','B'}:raise ValueError(f'Incomplete LoRA pair: {module}')
    if len(shape)!=2:raise ValueError(f'Expected Linear layer: {module}')
    if tuple(pair['A'].shape)!=(rank,shape[1]) or tuple(pair['B'].shape)!=(shape[0],rank):
        raise ValueError(f'Incompatible LoRA shape: {module}')
    if not all(torch.isfinite(t).all().item() for t in pair.values()):
        raise ValueError(f'Non-finite LoRA weights: {module}')


def convert(source, base, output, receipt, *, alpha, rank=32):
    for p in (output,receipt):
        if p.exists():raise FileExistsError(f'Refusing overwrite: {p}')
    if alpha<=0 or rank<=0:raise ValueError('Rank and alpha must be positive')
    src=load_file(str(source),device='cpu')
    with safe_open(str(source),framework='pt') as h:metadata=h.metadata() or {}
    for key in ('lora_alpha','ss_network_alpha'):
        if key in metadata and float(metadata[key])!=alpha:raise ValueError('Source alpha contradicts supplied run configuration')
    if 'lora_rank' in metadata and int(metadata['lora_rank'])!=rank:raise ValueError('Source rank mismatch')
    pairs={};mapping={}
    for key,value in src.items():
        m=re.fullmatch(r'transformer\.(.+)\.lora_([AB])\.weight',key)
        if not m:raise ValueError(f'Unknown tensor key: {key}')
        name,side=m.groups();module=map_module(name)
        pair=pairs.setdefault(module,{})
        if side in pair:raise ValueError(f'Name collision: {module}')
        pair[side]=value;mapping[key]='lora_unet_'+module.replace('.','_')+('.lora_down.weight' if side=='A' else '.lora_up.weight')
    if len(src)!=528 or len(pairs)!=264:
        raise ValueError(f'Expected full 264-layer Krea2 adapter, got {len(src)} tensors / {len(pairs)} layers')
    if set(pairs)!=expected_modules():
        raise ValueError(f'Module coverage mismatch; missing={sorted(expected_modules()-set(pairs))}; unexpected={sorted(set(pairs)-expected_modules())}')
    state={}
    with safe_open(str(base),framework='pt') as h:
        keys=set(h.keys())
        for module,pair in pairs.items():
            key=module+'.weight'
            if key not in keys:raise ValueError(f'Mapped module absent from base: {key}')
            validate_pair(module,pair,h.get_slice(key).get_shape(),rank)
            prefix='lora_unet_'+module.replace('.','_')
            state[prefix+'.lora_down.weight']=pair['A']
            state[prefix+'.lora_up.weight']=pair['B']
            state[prefix+'.alpha']=torch.tensor(float(alpha),dtype=torch.float32)
    source_hash=sha(source);base_hash=sha(base)
    metadata.update({'conversion_tool':'DohnaDohna/tools/krea2/convert_lora.py',
                     'source_sha256':source_hash,'validated_base_sha256':base_hash,
                     'ss_network_dim':str(rank),'ss_network_alpha':str(alpha),
                     'format':'musubi-native','conversion_only':'true'})
    output.parent.mkdir(parents=True,exist_ok=True)
    temp=output.with_suffix('.safetensors.partial')
    if temp.exists():raise FileExistsError(temp)
    save_file(state,str(temp),metadata=metadata)
    restored=load_file(str(temp),device='cpu')
    for key,tensor in src.items():
        if not torch.equal(tensor,restored[mapping[key]]):raise ValueError(f'Conversion changed weights: {key}')
    for module in pairs:
        if restored['lora_unet_'+module.replace('.','_')+'.alpha'].item()!=alpha:
            raise ValueError(f'Conversion changed scale: {module}')
    temp.rename(output)
    result={'source':str(source),'source_sha256':source_hash,'base':str(base),'base_sha256':base_hash,
            'output':str(output),'output_sha256':sha(output),'rank':rank,'alpha':alpha,
            'source_tensors':len(src),'mapped_modules':len(pairs),'output_tensors':len(restored),
            'tensor_value_verification':'BITWISE_EQUAL','scaling':'alpha / rank',
            'shape_verification':'PASS','quality_status':'NOT_EVALUATED','module_map':mapping}
    save(receipt,result)
    print(json.dumps({k:v for k,v in result.items() if k!='module_map'},indent=2))


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True)
    p.add_argument('--base',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--receipt',type=Path,required=True);p.add_argument('--alpha',type=float,required=True)
    p.add_argument('--rank',type=int,default=32)
    a=p.parse_args();convert(a.source,a.base,a.output,a.receipt,alpha=a.alpha,rank=a.rank)
