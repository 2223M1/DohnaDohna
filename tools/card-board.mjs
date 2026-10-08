// Offline authoring only. Returned HTML is parsed as inert JSON, never executed.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const baselinePath = 'Docs/authoring/card-board-baseline.json';
const htmlPath = 'Docs/authoring/DohnaDohna-card-board.html';
const fields = new Set(['name','cost','upgradedCost','role','melee','text','upgradedText','notes','mark','order','hp','color']);
const sha = value => crypto.createHash('sha256').update(value).digest('hex');
const encode = value => JSON.stringify(value).replaceAll('<', '\\u003c');
const plain = text => (text ?? '').replace(/\[\/?(?:gold|green|red|blue|purple|orange|aqua)\]/g, '')
  .replace(/\[img\][^[]*energy_icon\.png\[\/img\]/g, '⚡');
const json = file => JSON.parse(fs.readFileSync(path.resolve(root, file), 'utf8'));

export function readBoard(html) {
  assert(Buffer.byteLength(html) < 8_000_000, 'Board exceeds 8 MB');
  const payloads = [...html.matchAll(/<script\s+id="dohna-board-data"\s+type="application\/json"\s*>([\s\S]*?)<\/script\s*>/gi)];
  assert.equal(payloads.length, 1, 'Exactly one inert DohnaDohna data block is required');
  const data = JSON.parse(payloads[0][1]);
  assert.equal(data.schema, 1);
  assert.equal(data.project, 'DohnaDohna');
  assert(Array.isArray(data.entries) && data.entries.length < 2000);
  assert.equal(new Set(data.entries.map(e => e.id)).size, data.entries.length, 'Duplicate stable ID');
  for (const e of data.entries) {
    assert(typeof e.id === 'string' && /^[\w.-]{1,150}$/.test(e.id), 'Invalid stable ID');
    assert(['card','role','relic','effect','power'].includes(e.kind), 'Unknown entry kind');
    for (const [key, value] of Object.entries(e)) {
      assert(['id','kind','source','rarity','type',...fields].includes(key), 'Unknown field: ' + key);
      assert(['string','number','boolean'].includes(typeof value) || value === null, 'Scalar fields only');
      if (typeof value === 'string') assert(value.length < 20000, 'Field too large');
    }
  }
  return data;
}

export function diffBoard(baseline, returned) {
  assert.equal(returned.baselineHash, baseline.baselineHash, 'Different baseline: use the baseline delivered with this board');
  assert.equal(sha(JSON.stringify(baseline.entries)), baseline.baselineHash, 'Repository baseline was modified');
  const old = new Map(baseline.entries.map(e => [e.id, e]));
  const next = new Map(returned.entries.map(e => [e.id, e]));
  const added = returned.entries.filter(e => !old.has(e.id));
  const removed = baseline.entries.filter(e => !next.has(e.id));
  const changed = [];
  for (const e of returned.entries) {
    const before = old.get(e.id);
    if (!before) continue;
    const changes = {};
    for (const key of new Set([...Object.keys(before), ...Object.keys(e)])) {
      if (JSON.stringify(before[key]) === JSON.stringify(e[key])) continue;
      assert(fields.has(key), 'Readonly identity/source changed: ' + e.id + '/' + key);
      changes[key] = { before: before[key] ?? null, after: e[key] ?? null };
    }
    if (Object.keys(changes).length) changed.push({ id: e.id, name: e.name, changes });
  }
  return { project: 'DohnaDohna', baselineHash: baseline.baselineHash, added, removed, changed };
}

function patchFile(file, content) {
  if (!fs.existsSync(path.join(root, file))) return `*** Add File: ${file}\n` + content.trimEnd().split('\n').map(l=>'+'+l).join('\n') + '\n';
  const old = fs.readFileSync(path.join(root, file), 'utf8').trimEnd();
  if (old === content.trimEnd()) return '';
  return `*** Update File: ${file}\n@@\n` + old.split(/\r?\n/).map(l=>'-'+l).join('\n') + '\n' + content.trimEnd().split('\n').map(l=>'+'+l).join('\n') + '\n';
}

function generate(runtimeFile, vanillaRoot) {
  const runtime = json(runtimeFile);
  assert.equal(runtime.filter(c => c.prototype).length, 170, 'Expected verified runtime catalog');
  const roleSource = fs.readFileSync(path.join(root, 'Content/RoleDefinition.cs'), 'utf8');
  const roles = [...roleSource.matchAll(/new\("(\w+)", "([^"]+)", "[^"]+", "[^"]+", "(#[0-9A-F]+)"/g)]
    .map(([,id,name,color])=>({id,name,color,hp:Number(roleSource.match(new RegExp(`"${id}" => (\\d+)`))[1])}));
  assert.equal(roles.length, 10);
  const cardsLoc = json('DohnaDohna/localization/zhs/cards.json');
  const relicsLoc = json('DohnaDohna/localization/zhs/relics.json');
  let order = 0;
  const entries = runtime.map(c=>({id:c.id,kind:'card',name:c.title,cost:c.cost,upgradedCost:c.upgradedCost,
    role:c.role ?? '',melee:c.melee,text:plain(c.baseText),upgradedText:plain(c.upgradedText),notes:'',mark:'',order:order++,
    rarity:c.rarity,type:c.type,source:c.prototype ? `STS2 0.111.0 · ${c.prototype}` : 'DohnaDohna'}));
  for (const r of roles) {
    entries.push({id:'ROLE.'+r.id,kind:'role',name:r.name,role:r.id,hp:r.hp,color:r.color,notes:'',mark:'',order:order++,source:'RoleDefinition / 原作基础HP ÷ 10'});
    for (const suffix of ['', '_PLUS']) {
      const id = `DOHNA_DOHNA_RELIC_${r.id.toUpperCase()}_EMBLEM${suffix}`;
      entries.push({id:'RELIC.'+id,kind:'relic',name:relicsLoc[id+'.title'],role:r.id,text:relicsLoc[id+'.description'],
        upgradedText:'',notes:'',mark:'',order:order++,source:suffix ? '奥罗巴斯之触 · 收益翻倍' : '角色初始遗物',rarity:'Starter'});
    }
    entries.push({id:'EFFECT.'+r.id,kind:'effect',name:r.name+' · 协同攻击附效',role:r.id,
      text:plain(cardsLoc[`DOHNA_SQUAD.effect_${r.id}`]),upgradedText:plain(cardsLoc[`DOHNA_SQUAD.effect_${r.id}_upgraded`]),
      notes:'古老牙齿蜕变后，附效数值翻倍；无视格挡不变。',mark:'',order:order++,source:'SquadActions'});
  }
  const powerLoc = JSON.parse(fs.readFileSync(path.join(vanillaRoot, 'zhs/powers.json'), 'utf8'));
  const powerNames = new Set();
  for (const file of fs.readdirSync(path.join(root, 'Cards/Catalog'))) {
    const code = fs.readFileSync(path.join(root,'Cards/Catalog',file),'utf8');
    for (const match of code.matchAll(/PowerCmd\.Apply<(\w+)>/g)) if (!match[1].startsWith('Squad')) powerNames.add(match[1]);
  }
  for (const name of [...powerNames].sort()) {
    const id = name.replace(/([a-z0-9])([A-Z])/g,'$1_$2').toUpperCase();
    if (!powerLoc[id+'.title']) continue;
    entries.push({id:'POWER.'+id,kind:'power',name:powerLoc[id+'.title'],role:'',text:plain(powerLoc[id+'.description'] ?? powerLoc[id+'.smartDescription']),
      upgradedText:'',notes:'原生能力模板；花括号在游戏中由实际层数填入。',mark:'',order:order++,source:'STS2 0.111.0 · '+name});
  }
  const baseline = {schema:1,project:'DohnaDohna',label:'170张卡池 · 真实初始遗物 · 2026-10-09',host:'0.111.0',
    runtimeSha256:sha(fs.readFileSync(path.resolve(root,runtimeFile))),baselineHash:sha(JSON.stringify(entries)),entries};
  const template = fs.readFileSync(path.join(root,'tools/card-board.template.html'),'utf8');
  if (process.argv.includes('--write')) {
    fs.mkdirSync(path.join(root,'Docs/authoring'),{recursive:true});
    fs.writeFileSync(path.join(root,baselinePath),JSON.stringify(baseline,null,2)+'\n');
    fs.writeFileSync(path.join(root,htmlPath),template.replace('__DOHNA_DATA__',encode(baseline)));
    console.log(`Generated offline board: ${entries.length} entries; baseline ${baseline.baselineHash}`);
    return;
  }
  console.log('*** Begin Patch\n' + patchFile(baselinePath,JSON.stringify(baseline,null,2))
    + patchFile(htmlPath,template.replace('__DOHNA_DATA__',encode(baseline))) + '*** End Patch');
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  if (process.argv[2] === 'generate') generate(process.argv[3], process.argv[4]);
  else if (process.argv[2] === 'diff') {
    const returned = readBoard(fs.readFileSync(process.argv[3], 'utf8'));
    console.log(JSON.stringify(diffBoard(json(process.argv[4] ?? baselinePath),returned),null,2));
  } else throw new Error('Usage: node tools/card-board.mjs generate <runtime-cards.json> <vanilla-localization-root> | diff <returned.html> [baseline.json]');
}
