import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = p => fs.readFileSync(path.join(root,p),'utf8');
const manifest = JSON.parse(read('DohnaDohna.json'));
assert.equal(manifest.id,'DohnaDohna');
assert.equal(manifest.dependencies[0].id,'STS2-RitsuLib');
assert.equal(manifest.has_pck,true,'Squad gameplay requires the resource pack.');
assert.ok(fs.existsSync(path.join(root,'tools/verify-pck.py')));
const project = read('DohnaDohna.csproj');
assert.match(project,/<EnableDefaultCompileItems>false<\/EnableDefaultCompileItems>/);
assert.doesNotMatch(project,/<Compile Include="[^"\n]*(?:Reference|Tests|tools|\*\*\/\*\.cs"$)/m);
assert.ok(fs.existsSync(path.join(root,'Reference/.gdignore')));
assert.match(read('export_presets.cfg'),/Reference\/\*\*/);
for(const lang of ['zhs','eng','jpn']) for(const name of ['cards','powers','settings_ui']) JSON.parse(read(`DohnaDohna/localization/${lang}/${name}.json`));
const publishing=JSON.parse(read('eng/publish.example.json'));
for(const key of ['repository','workshopItemId','telemetryUrl','pagesProject']) assert.equal(publishing[key],'');
for(const f of ['Scripts/Entry.cs','DohnaDohna.csproj','DohnaDohna.json','eng/DohnaDohna.Hosts.props','tools/Build.ps1','tools/New-Package.ps1']) {
  assert.doesNotMatch(read(f),/3776911445|telemetry\.feixingwawa\.cn|2223M1\/NinjaSlayer|NinjaSlayerIds/);
}
console.log('PASS: identity, resource contract, reference/compiler/export boundaries, localization JSON and empty publication bindings.');
