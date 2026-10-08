"""Verify the immutable reference snapshot against migration-manifest.json."""
import hashlib
import json
from pathlib import Path
root = Path(__file__).resolve().parents[1]
manifest = json.loads((root/'Docs/migration-manifest.json').read_text(encoding='utf-8'))
failures = []
for entry in manifest['files']:
    path = root/entry['destination']
    if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
        failures.append(entry['destination'])
if failures:
    raise SystemExit('Reference mismatch:\n'+'\n'.join(failures))
print(f"PASS: {len(manifest['files'])} reference files match migration hashes (see manifest for explicitly transformed files).")
