"""Check the private local STS2 source reference; intentionally separate from CI."""
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
manifest_path=root/'Reference/Vanilla/source-manifest.json'
if not manifest_path.is_file():
    raise SystemExit('Private vanilla reference is not installed on this machine; see Docs/VANILLA-SOURCE.md.')
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
failures=[]
for item in manifest['files']:
    p=root/item['destination']
    if not p.resolve().is_relative_to((root/'Reference/Vanilla').resolve()):
        raise SystemExit('Source reference path escaped its private directory.')
    if not p.is_file() or p.stat().st_size!=item['bytes'] or hashlib.sha256(p.read_bytes()).hexdigest()!=item['sha256']:
        failures.append(item['destination'])
if failures:raise SystemExit('Source reference mismatch:\n'+'\n'.join(failures))
print(f"PASS: {len(manifest['files'])} original game source/reference files, all bytes verified.")
