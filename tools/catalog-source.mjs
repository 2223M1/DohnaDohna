// Explicit, one-time import of the approved 170 prototypes. Ordinary builds do not need private sources.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'Content/card-pool.json'), 'utf8'));
const split = s => s.split(/\s+/).filter(Boolean);
export const cards = split(manifest.common).map(prototype => ({ prototype, role: null, rarity: 'Common', melee: false }));
assert.equal(cards.length, 20);
for (const [role, group] of Object.entries(manifest.roles)) {
  assert.equal(split(group.uncommon).length, 9, role);
  assert.equal(split(group.rare).length, 6, role);
  for (const rarity of ['Uncommon', 'Rare']) for (const prototype of split(group[rarity.toLowerCase()]))
    cards.push({ prototype, role, rarity, melee: split(group.melee).includes(prototype) });
}
assert.equal(cards.length, 170);
assert.equal(new Set(cards.map(c => c.prototype)).size, 170);
export const snake = s => s.replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2').replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();
export const modelName = c => `Squad${c.prototype}`;
export const modelId = c => `DOHNA_DOHNA_CARD_${snake(modelName(c))}`;

if (process.argv.includes('--audit')) {
  for (const card of cards) {
    const source = fs.readFileSync(path.join(root, `Reference/Vanilla/${manifest.hostBaseline}/src/Core/Models/Cards/${card.prototype}.cs`), 'utf8');
    assert(fs.existsSync(path.join(root, `Reference/Vanilla/0.107.1/src/Core/Models/Cards/${card.prototype}.cs`)));
    console.log(JSON.stringify({ ...card,
      ctor: source.match(/: base\([^\n]+/)?.[0],
      methods: [...source.matchAll(/(?:public|protected|private).*?(?:override|Task|void|bool).*?(?:\(|=>|\n\t\{)/g)].map(m => m[0].trim()),
      powers: [...new Set([...source.matchAll(/PowerCmd\.Apply<(\w+)>/g)].map(m => m[1]))],
      special: source.split('\n').filter(l => /Owner\.Creature|Owner\.Player|PlayerCreatures|Allies|OtherPlayer|FromCard|Execute\(|CombatManager|Owner.PlayerCombatState/.test(l)).map(l => l.trim())
    }));
  }
}

if (process.argv.includes('--generate')) {
  const dir = path.join(root, 'Cards/Catalog');
  fs.mkdirSync(dir, { recursive: true });
  for (const c of cards) {
    const input = path.join(root, `Reference/Vanilla/${manifest.hostBaseline}/src/Core/Models/Cards/${c.prototype}.cs`);
    const original = fs.readFileSync(input, 'utf8');
    let src = original.replace(/\r\n/g, '\n').replace(/namespace MegaCrit\.Sts2\.Core\.Models\.Cards;/, `namespace DohnaDohna.Cards.Catalog;`)
      .replace(new RegExp(`\\b${c.prototype}\\b`, 'g'), modelName(c))
      .replace(`public sealed class ${modelName(c)} : CardModel`, `[RegisterCard(typeof(SquadCardPool))]\npublic sealed class ${modelName(c)} : SquadCatalogCard`)
      .replace(new RegExp(`(public ${modelName(c)}\\(\\)[\\s\\S]*?: base\\([^\\n]*CardRarity\\.)\\w+`), `$1${c.rarity}`)
      .replace(/protected override (async )?Task OnPlay\(/g, 'protected override $1Task OnSquadPlay(')
      .replace(/protected override bool IsPlayable/g, 'protected override bool IsOriginalPlayable')
      .replace(/base\.Owner\.Creature/g, 'Actor')
      .replace(/\.Execute\(choiceContext\)/g, '.ExecuteSquad(choiceContext)')
      .replace(/\.FromCard\(this, cardPlay\)/g, '.FromSquadCard(this, cardPlay)')
      .replace(/protected override IEnumerable<IHoverTip> ExtraHoverTips/g, 'protected override IEnumerable<IHoverTip> AdditionalHoverTips')
      .replace(/public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;/g, '')
      .replace(/TargetType.AnyAlly/g, 'SquadTargeting.OtherMember');
    // Decompiled compiler collection helpers are private host implementation details.
    const helper = /new (?:global::)?_003C_003Ez__ReadOnly(Array|List|SingleElementList)<[^>]+>\(/g;
    let match;
    while ((match = helper.exec(src))) {
      const start = match.index, bodyStart = helper.lastIndex;
      let end = bodyStart, depth = 1;
      for (; depth; end++) { if (src[end] === '(') depth++; if (src[end] === ')') depth--; }
      const body = src.slice(bodyStart, end - 1);
      src = src.slice(0, start) + (match[1] === 'SingleElementList' ? `[${body}]` : body) + src.slice(end);
      helper.lastIndex = start;
    }
    if (manifest.memberTarget.includes(c.prototype)) src = src.replace(/TargetType.Self/g, 'SquadTargeting.Member');
    src = src.replace(`: SquadCatalogCard\n{`, `: SquadCatalogCard\n{\n\tpublic override string? FixedRole => ${c.role ? `"${c.role}"` : 'null'};\n\tpublic override bool IsMelee => ${c.melee};\n\tpublic override string Prototype => "${c.prototype}";`);
    assert(src.includes(' : SquadCatalogCard'), c.prototype);
    src = `// Adapted from STS2 ${manifest.hostBaseline} ${c.prototype}; source SHA256 ${crypto.createHash('sha256').update(original).digest('hex')}.\n// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.\nusing DohnaDohna.Code.Squad;\nusing DohnaDohna.Content;\nusing MegaCrit.Sts2.Core.Models;\nusing MegaCrit.Sts2.Core.Models.Cards;\nusing STS2RitsuLib.Interop.AutoRegistration;\n${src}`;
    const dest = path.join(dir, `${modelName(c)}.cs`);
    if (fs.existsSync(dest)) throw new Error(`Refusing to overwrite adapted source: ${dest}`);
    fs.writeFileSync(dest, src);
  }
  console.log(`Generated ${cards.length} independent models. Review required before build.`);
}

if (process.argv.includes('--adapt-patch')) {
  let patch = '*** Begin Patch\n';
  const onlyRole = process.argv.find(a => a.startsWith('--role='))?.slice(7);
  for (const c of cards.filter(c => !onlyRole || (c.role ?? 'common') === onlyRole)) {
    const file = `Cards/Catalog/${modelName(c)}.cs`;
    const old = fs.readFileSync(path.join(root, file), 'utf8');
    let src = old.replace(`public override string Prototype => "${c.prototype}";`,
      `public override string Prototype => "${c.prototype}";\n\tpublic override CardModel PrototypeCard => ModelDb.Card<${c.prototype}>();`)
      .replace(/CanonicalVars/g, 'OriginalVars')
      .replace(/\tpublic override TargetType TargetType => TargetType.AnyEnemy;\n/, '');
    if (['BodySlam', 'DemonicShield', 'ExpectAFight', 'GangUp'].includes(c.prototype))
      src = src.replace(/card.Owner.Creature/g, 'SquadCardModel.ResolveActor(card)');
    if (manifest.memberTarget.includes(c.prototype)) {
      src = src.replace(/CreatureCmd.Heal\(Actor,/g, 'CreatureCmd.Heal(cardPlay.Target!,')
        .replace(/CreatureCmd.GainBlock\(Actor,/g, 'CreatureCmd.GainBlock(cardPlay.Target!,')
        .replace(/(PowerCmd.Apply<\w+>\(choiceContext, )Actor,/g, '$1cardPlay.Target!,');
    }
    const start = src.indexOf('protected override async Task OnSquadPlay(');
    assert(start >= 0, c.prototype);
    const open = src.indexOf('{', start);
    let depth = 1, end = open + 1;
    for (; depth; end++) { if (src[end] === '{') depth++; if (src[end] === '}') depth--; }
    const body = src.slice(open, end).replace(/\bawait\b[\s\S]*?;/g, stmt => `${stmt}\n\t\tif (Actor.IsDead) return;`);
    src = src.slice(0, open) + body + src.slice(end);
    src = src.replace(/(public override async Task (?:OnEnqueuePlayVfx|AfterAutoPrePlayPhaseEnteredEarly|BeforeHandDraw)\([^)]*\)\n\t\{)/g, '$1\n\t\tif (IsFallback) return;');
    if (src === old) continue;
    patch += `*** Update File: ${file}\n@@\n` + old.trimEnd().split('\n').map(l => '-' + l).join('\n') + '\n'
      + src.trimEnd().split('\n').map(l => '+' + l).join('\n') + '\n';
  }
  console.log(patch + '*** End Patch');
}
