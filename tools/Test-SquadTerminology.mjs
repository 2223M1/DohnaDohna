// Display terminology only: do not rewrite identity, targeting or authoring baselines.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {readBoard, diffBoard} from './card-board.mjs';
const read = path => JSON.parse(fs.readFileSync(path, 'utf8'));
const loc = (lang, table) => read(`DohnaDohna/localization/${lang}/${table}.json`);
const cards = loc('zhs', 'cards'), characters = loc('zhs', 'characters');
for (const file of fs.readdirSync('DohnaDohna/localization/zhs').filter(f => f.endsWith('.json')))
  for (const value of Object.values(loc('zhs', file.slice(0, -5))))
    assert.doesNotMatch(value, /队友|队员|前排|最右侧同伴|(?:临近|邻近)同伴/, file);
assert.match(cards['DOHNA_SQUAD.dynamic_effect_unknown'], /最前方同伴/);
for (const kind of ['attack', 'skill', 'power']) assert.match(cards[`DOHNA_SQUAD.fallback_${kind}`], /最前方同伴代打/);
assert.match(characters['DOHNA_DOHNA_SQUAD_UI.passive_porno'], /正前方同伴/);
assert.match(characters['DOHNA_DOHNA_SQUAD_UI.passive_kuma'], /后方同伴/);
assert.doesNotMatch(characters['DOHNA_DOHNA_SQUAD_UI.passive_kuma'], /正后方/);
for (const role of ['kuma', 'alyce', 'antena', 'tora', 'kikuchiyo', 'medhico', 'joker', 'zappa', 'kirakira', 'porno']) {
  const text = characters[`DOHNA_DOHNA_SQUAD_UI.passive_${role}`];
  assert.equal(loc('zhs', 'powers')[`DOHNA_DOHNA_POWER_${role.toUpperCase()}_INNATE.description`], text);
  assert.equal(loc('zhs', 'relics')[`DOHNA_DOHNA_RELIC_${role.toUpperCase()}_EMBLEM.description`], text);
}
assert.match(cards['DOHNA_DOHNA_CARD_SQUAD_LOOP.description'], /最右侧/); // Native orb order stays intact.
for (const [lang, term] of [['zhs', '最前方同伴'], ['eng', 'frontmost member'], ['jpn', '先頭の隊員']]) {
  assert.ok(loc(lang, 'cards')['DOHNA_DOHNA_CARD_SQUAD_SWAP.description'].includes(term));
  assert.ok(loc(lang, 'characters')['DOHNA_DOHNA_CHARACTER_DOHNA_SQUAD.description'].includes(term));
}
const board = readBoard(fs.readFileSync('Docs/authoring/DohnaDohna-card-board.html', 'utf8'));
diffBoard(read('Docs/authoring/card-board-baseline.json'), board);
for (const entry of board.entries.filter(e => e.kind !== 'power'))
  for (const field of ['name', 'text', 'upgradedText', 'designText'])
    if (entry[field]) assert.doesNotMatch(entry[field], /队友|队员|前排代打|最右侧(?:队员|同伴)/, entry.id);
console.log(`PASS: three-language formation terms, native orb wording, ${board.entries.length} board identities and original baseline.`);
