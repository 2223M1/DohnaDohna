"""Boundary tests for data preparation, conversion and evaluation contracts."""
import contextlib
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from PIL import Image
import torch
from safetensors.torch import save_file, load_file
import dataset as d
import convert_lora as c
import evaluate as e


class DataTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
    def tearDown(self):self.temp.cleanup()
    def fixture(self):
        candidates=[];reviews=[]
        for i,color in enumerate(('red','blue','green')):
            path=self.root/f'source-{i}.png';Image.new('RGBA',(40,80),color).save(path)
            ident=f'kuma-{i}'
            candidates.append({'id':ident,'character':'kuma','source':str(path),'source_name':path.name,
                               'source_sha256':d.sha(path),'kind':'portrait','group':str(i),'width':40,'height':80})
            reviews.append({'id':ident,'split':'holdout' if i==2 else 'train','group':str(i),
                            'reviewed_nonexplicit':True,'caption':'dohna_kuma, standing.'})
        d.save(self.root/'candidates.json',candidates);d.save(self.root/'curation.json',reviews)
        return candidates,reviews
    def build(self):
        with patch.object(d,'ROSTER',{'kuma':'阿熊'}),contextlib.redirect_stdout(io.StringIO()):d.build(self.root)
    def test_alpha_removes_hidden_colors_and_preserves_edges(self):
        im=Image.new('RGBA',(4,4),(255,0,255,0));im.putpixel((1,1),(0,0,255,128))
        path=self.root/'alpha.png';im.save(path);result=d.opaque_image(path)
        self.assertEqual(result.size,(1,1));self.assertEqual(result.getpixel((0,0)),(120,120,248))
        im.putpixel((0,0),(0,255,0,0));other=self.root/'other.png';im.save(other)
        self.assertEqual(d.visible_hash(path),d.visible_hash(other))
    def test_native_detail_and_opaque_scene_background_are_preserved(self):
        scene=Image.new('RGB',(123,267),(10,20,30));path=self.root/'scene.png';scene.save(path)
        result=d.prepared_image(path)
        self.assertEqual(result.size,scene.size);self.assertEqual(result.tobytes(),scene.tobytes())
        portrait=Image.new('RGBA',(700,1900),(0,0,0,0))
        portrait.paste((20,30,40,255),(50,10,650,1890));path=self.root/'portrait.png';portrait.save(path)
        result=d.prepared_image(path,background=(245,239,229))
        self.assertEqual(result.size,(648,1928));self.assertEqual(result.getpixel((0,0)),(245,239,229))
    def test_zip_excludes_holdout_and_detects_tampering(self):
        self.fixture();self.build();d.verify(self.root)
        summary=d.load(self.root/'dataset-summary.json')[0]
        with zipfile.ZipFile(summary['package']) as z:
            self.assertEqual(set(z.namelist()),{'kuma-0.png','kuma-0.txt','kuma-1.png','kuma-1.txt'})
        (self.root/'datasets/kuma/train/kuma-0.txt').write_text('wrong caption')
        with self.assertRaises(ValueError):d.verify(self.root)
    def test_group_leakage_and_visible_duplicates_fail(self):
        _,reviews=self.fixture();reviews[-1]['group']='0';d.save(self.root/'curation.json',reviews)
        with self.assertRaisesRegex(ValueError,'group leakage'):self.build()
        candidates,reviews=self.fixture();candidates[-1].update({k:candidates[0][k] for k in ('source','source_sha256')})
        d.save(self.root/'candidates.json',candidates)
        with self.assertRaisesRegex(ValueError,'Same source crop'):self.build()
        reviews[-1]['split']='train';d.save(self.root/'curation.json',reviews)
        with self.assertRaisesRegex(ValueError,'Duplicate visible'):self.build()
    def test_submitted_config_is_frozen(self):
        self.fixture();self.build();path=self.root/'configs/kuma.json';config=d.load(path)
        config['submission']['status']='RUNNING';d.save(path,config);before=path.read_bytes()
        with self.assertRaisesRegex(ValueError,'frozen'):self.build()
        self.assertEqual(path.read_bytes(),before)


class ConversionTests(unittest.TestCase):
    def test_mapping_coverage_and_update_scale(self):
        fixed=['img_in','time_embed.linear_1','time_embed.linear_2','txt_in.linear_1','txt_in.linear_2',
               'time_mod_proj','final_layer.linear','text_fusion.projector']
        blocks=[f'transformer_blocks.{i}' for i in range(28)]
        blocks += [f'text_fusion.{group}.{i}' for group in ('layerwise_blocks','refiner_blocks') for i in range(2)]
        names=fixed+[b+'.'+leaf for b in blocks for leaf in ('attn.to_q','attn.to_k','attn.to_v','attn.to_gate','attn.to_out.0','ff.gate','ff.up','ff.down')]
        self.assertEqual({c.map_module(n) for n in names},c.expected_modules())
        source={};base={}
        for n in names:
            source[f'transformer.{n}.lora_A.weight']=torch.arange(6,dtype=torch.float32).reshape(2,3).clone()
            source[f'transformer.{n}.lora_B.weight']=torch.arange(8,dtype=torch.float32).reshape(4,2).clone()
            base[c.map_module(n)+'.weight']=torch.zeros(4,3)
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);src=root/'source.safetensors';model=root/'base.safetensors';out=root/'native.safetensors';receipt=root/'receipt.json'
            save_file(source,str(src));save_file(base,str(model))
            with contextlib.redirect_stdout(io.StringIO()):c.convert(src,model,out,receipt,rank=2,alpha=4)
            converted=load_file(str(out));r=d.load(receipt)
            self.assertEqual(r['base_sha256'],d.sha(model));self.assertEqual(r['mapped_modules'],264)
            prefix='lora_unet_first'
            native=converted[prefix+'.lora_up.weight']@converted[prefix+'.lora_down.weight']*converted[prefix+'.alpha']/2
            original=source['transformer.img_in.lora_B.weight']@source['transformer.img_in.lora_A.weight']*2
            self.assertTrue(torch.equal(native,original))
    def test_unknown_missing_shapes_and_nonfinite_rejected(self):
        with self.assertRaises(ValueError):c.map_module('unknown.projection')
        with self.assertRaises(ValueError):c.validate_pair('x',{'A':torch.zeros(2,3)},[4,3],2)
        with self.assertRaises(ValueError):c.validate_pair('x',{'A':torch.zeros(3,2),'B':torch.zeros(4,2)},[4,3],2)
        with self.assertRaises(ValueError):c.validate_pair('x',{'A':torch.full((2,3),float('nan')),'B':torch.zeros(4,2)},[4,3],2)


class EvaluationTests(unittest.TestCase):
    def test_matrix_controls_and_trigger_ablation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);e.prepare(root);preset=d.load(root/'evaluation/kuma/preset.json')
            with self.assertRaises(ValueError):e.jobs(preset,'screen')
            preset['checkpoints']=[{'label':label,'step':step} for label,step in zip(('early','middle','late'),(100,300,500))]
            self.assertEqual(len(e.jobs(preset,'screen')),12)
            preset['selection']={'label':'middle','weight':0.75};jobs=e.jobs(preset,'final')
            formal=[j for j in jobs if j['control'] is None]
            self.assertEqual(len(formal),18);self.assertEqual(len(jobs),24)
            self.assertEqual({j['seed'] for j in formal},{42,271828,314159})
            for job in jobs:
                if job['control']=='no_trigger':self.assertNotIn('dohna_kuma',job['prompt'])
                if job['control']=='no_lora':self.assertIsNone(job['checkpoint'])


if __name__=='__main__':unittest.main()
