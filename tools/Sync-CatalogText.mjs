// Explicit content import, not a build step. Emits apply_patch, preserving unrelated authored keys.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { cards, modelId, snake } from './catalog-source.mjs';
const option = name => process.argv.find(a => a.startsWith(`--${name}=`))?.slice(name.length + 3);
const lang = option('lang');
const index = ['zhs', 'eng', 'jpn'].indexOf(lang);
assert(index >= 0, '--lang=zhs|eng|jpn');
const vanilla = option('vanilla');
assert(vanilla, '--vanilla=<0.111.0 localization root>');
const translated = JSON.parse(fs.readFileSync(path.join(vanilla, lang, 'cards.json'), 'utf8'));
const read = table => JSON.parse(fs.readFileSync(`DohnaDohna/localization/${lang}/${table}.json`, 'utf8'));
const output = { cards: read('cards'), characters: read('characters'), powers: read('powers'), relics: read('relics') };
for (const card of cards) {
  const prefix = snake(card.prototype) + '.';
  const entries = Object.entries(translated).filter(([k]) => k.startsWith(prefix));
  assert(entries.some(([k]) => k === prefix + 'title'), card.prototype);
  for (const [key, text] of entries) output.cards[modelId(card) + key.slice(prefix.length - 1)] = text
    .replaceAll('所有玩家', '所有队员').replaceAll('其他玩家', '其他队员').replaceAll('另一名玩家', '另一名队员')
    .replaceAll('ALL players', 'ALL members').replaceAll('another player', 'another member').replaceAll('other players', 'other members')
    .replaceAll('すべてのプレイヤー', 'すべての隊員').replaceAll('他のプレイヤー', '他の隊員');
}
const effects = {
  kuma: [['其他队友获得1点[gold]活力[/gold]。', 'Other members gain 1 [gold]Vigor[/gold].', '他の隊員に[gold]活力[/gold]1を与える。'], [1, 2]],
  alyce: [['无视[gold]格挡[/gold]。', 'Ignore [gold]Block[/gold].', '[gold]ブロック[/gold]を無視する。']],
  antena: [['生成1个[gold]闪电充能球[/gold]。', '[gold]Channel[/gold] 1 [gold]Lightning[/gold].', '[gold]ライトニング[/gold]を1個[gold]生成[/gold]する。']],
  tora: [['本回合获得1点[gold]力量[/gold]。', 'Gain 1 [gold]Strength[/gold] this turn.', 'このターン[gold]筋力[/gold]1を得る。'], [1, 2]],
  kikuchiyo: [['若击杀目标，抽1张牌。', 'If this kills the target, draw 1 card.', '対象を倒した場合、カードを1枚引く。'], [1, 2]],
  medhico: [['给予一名存活队员4点[gold]格挡[/gold]。', 'Give a living member 4 [gold]Block[/gold].', '生存している隊員1人に[gold]ブロック[/gold]4を与える。'], [4, 6]],
  joker: [['获得3点[gold]活力[/gold]。', 'Gain 3 [gold]Vigor[/gold].', '[gold]活力[/gold]3を得る。'], [3, 5]],
  zappa: [['获得4点[gold]格挡[/gold]。', 'Gain 4 [gold]Block[/gold].', '[gold]ブロック[/gold]4を得る。'], [4, 6]],
  kirakira: [['给予2层[gold]中毒[/gold]。', 'Apply 2 [gold]Poison[/gold].', '[gold]毒[/gold]2を与える。'], [2, 3]],
  porno: [['给予1层[gold]虚弱[/gold]。', 'Apply 1 [gold]Weak[/gold].', '[gold]脱力[/gold]1を与える。'], [1, 2]]
};
const passives = {
  kuma: ['阿熊及其后方队友拥有1点额外力量。', 'Kuma and members behind him have 1 additional Strength.', 'クマとその後方の隊員は追加の筋力1を持つ。'],
  porno: ['珀尔诺及其正前方队友拥有2点额外荆棘。', 'Porno and the member directly in front of her have 2 additional Thorns.', 'ポルノとそのすぐ前の隊員は追加のトゲ2を持つ。'],
  tora: ['每回合，每种正面效果使虎太郎在本回合获得1点力量，每种限一次。', 'Each turn, each kind of positive effect grants Torataro 1 Strength for that turn, once per kind.', '毎ターン、正の効果の種類ごとに虎太郎はこのターン筋力1を得る。各種類1回まで。'],
  medhico: ['每场战斗，首次与梅蒂可交换顺位的队友回复3点生命。', 'Each combat, the first member to exchange positions with Medhico heals 3 HP.', '戦闘ごとに、メディコと最初に順番を入れ替えた隊員のHPを3回復する。'],
  kirakira: ['每场战斗开始时，选择一张手牌，使其在本场战斗获得保留。', 'At the start of each combat, choose a card in your hand. It gains Retain this combat.', '戦闘開始時、手札を1枚選ぶ。この戦闘中、そのカードは保留を得る。'],
  antena: ['每回合首次执行攻击牌时，每命中一名敌人，使其他队员在本回合获得1点敏捷。', 'The first Attack Antena performs each turn grants other members 1 Dexterity this turn for each enemy hit.', '毎ターン、アンテナが最初に実行するアタックで敵に命中するたび、他の隊員はこのターン敏捷1を得る。各対象1回。'],
  kikuchiyo: ['每回合首次执行攻击牌后，抽1张牌。', 'After Kikuchiyo performs her first Attack each turn, draw 1 card.', '毎ターン、菊千代が最初のアタックを実行した後、カードを1枚引く。'],
  alyce: ['攻击本回合已被其他队友攻击过的敌人时，每段伤害增加4。', 'ALyCE deals 4 additional damage per hit to enemies attacked by another member this turn.', 'このターンに他の隊員が攻撃した敵に対し、ALyCEの攻撃は1ヒットごとに追加の4ダメージを与える。'],
  zappa: ['在你的回合结束时，若扎帕位于最前方，获得4点格挡。', 'At the end of your turn, if Zappa is in front, he gains 4 Block.', 'ターン終了時、ザッパが先頭にいる場合、ブロック4を得る。'],
  joker: ['每次顺位改变时，获得2点活力。', 'Whenever Joker changes position, he gains 2 Vigor.', 'ジョーカーの順番が変わるたび、活力2を得る。']
};
const names = {
  kuma: ['阿熊','Kuma','クマ'], alyce: ['爱丽丝','ALyCE','ALyCE'], antena: ['安缇娜','Antena','アンテナ'],
  tora: ['虎太郎','Torataro','虎太郎'], kikuchiyo: ['菊千代','Kikuchiyo','菊千代'], medhico: ['梅蒂可','Medhico','メディコ'],
  joker: ['小丑','Joker','ジョーカー'], zappa: ['扎帕','Zappa','ザッパ'], kirakira: ['绮菈绮菈','Kirakira','キラキラ'], porno: ['珀尔诺','Porno','ポルノ']
};
for (const [role, [texts, upgrade]] of Object.entries(effects)) {
  const text = texts[index], upgraded = upgrade ? text.replace(String(upgrade[0]), String(upgrade[1])).replace('2 card.', '2 cards.') : text;
  output.cards[`DOHNA_SQUAD.effect_${role}`] = names[role][index] + ': ' + text;
  output.cards[`DOHNA_SQUAD.effect_${role}_upgraded`] = names[role][index] + ': ' + upgraded;
  // Ancient doubles effect quantities, not the number of selectable recipients.
  const doubleEffect = s => role === 'alyce' ? s : role === 'medhico' && index === 2
    ? s.replace(/(ブロック\[\/gold\])(\d+)/, (_, tag, value) => tag + Number(value) * 2)
    : s.replace(/\d+/, n => String(Number(n) * 2)).replace('2 card.', '2 cards.');
  output.cards[`DOHNA_SQUAD.effect_${role}_ancient`] = names[role][index] + ': ' + doubleEffect(text);
  output.cards[`DOHNA_SQUAD.effect_${role}_upgraded_ancient`] = names[role][index] + ': ' + doubleEffect(upgraded);
  const legacy = `DOHNA_DOHNA_CARD_${role.toUpperCase()}_SIGNATURE`;
  const damage = output.cards['DOHNA_DOHNA_CARD_SQUAD_STRIKE.description'];
  output.cards[legacy + '.description'] = damage + '\n' + text;
  output.cards[legacy + '.upgradeDescription'] = damage + '\n' + upgraded;
  const passive = passives[role][index];
  output.characters[`DOHNA_DOHNA_SQUAD_UI.passive_${role}`] = passive;
  const key = `DOHNA_DOHNA_POWER_${role.toUpperCase()}_INNATE`;
  output.powers[key + '.title'] = names[role][index] + [' · 固有',' · Innate','・固有'][index];
  output.powers[key + '.description'] = passive;
  output.powers[key + '.smartDescription'] = passive;
  const relicKey = `DOHNA_DOHNA_RELIC_${role.toUpperCase()}_EMBLEM`;
  output.relics[relicKey + '.title'] = names[role][index] + ['的专长', "'s Emblem", 'の紋章'][index];
  output.relics[relicKey + '.description'] = passive;
  output.relics[relicKey + '_PLUS.title'] = names[role][index] + ['的专长 · 强化', "'s Refined Emblem", 'の紋章・強化'][index];
  let enhanced = passive;
  if (role === 'kirakira') enhanced = [
    '每场战斗开始时，选择2张手牌，使其在本场战斗获得保留。',
    'At the start of each combat, choose 2 cards in your hand. They gain Retain this combat.',
    '戦闘開始時、手札を2枚選ぶ。この戦闘中、そのカードは保留を得る。'][index];
  else if (role === 'alyce' && index === 2) enhanced = passive.replace('追加の4', '追加の8');
  else enhanced = passive.replace(/\d+/, n => String(Number(n) * 2)).replace('2 card.', '2 cards.');
  output.relics[relicKey + '_PLUS.description'] = enhanced;
}
output.powers['DOHNA_DOHNA_POWER_KIRAKIRA_INNATE.selection'] = ['选择手牌，使其获得保留。','Choose cards to gain Retain.','保留を得る手札を選ぶ。'][index];
output.cards['DOHNA_DOHNA_CARD_SQUAD_TRANSCENDENT_SPECIAL.title'] = ['超越协同','Transcendent Team Attack','超越連携'][index];
output.cards['DOHNA_DOHNA_CARD_SQUAD_TRANSCENDENT_SPECIAL.description'] = output.cards['DOHNA_DOHNA_CARD_SQUAD_SPECIAL.description'];
output.cards['DOHNA_DOHNA_CARD_SQUAD_RELIC_UPGRADE_CHOICE.title'] = ['强化初始遗物','Refine Starter Relic','初期レリックを強化'][index];
output.cards['DOHNA_DOHNA_CARD_SQUAD_RELIC_UPGRADE_CHOICE.description'] = '{Effect}';
output.relics['DOHNA_SQUAD.refinement_source'] = ['自选一件初始遗物','a starter relic of your choice','選んだ初期レリック'][index];
output.relics['DOHNA_SQUAD.refinement_result'] = ['对应强化遗物','its refined form','対応する強化版'][index];
output.relics['DOHNA_SQUAD.refinement_prompt'] = ['选择一件初始遗物进行强化。','Choose a starter relic to refine.','強化する初期レリックを1つ選ぶ。'][index];
output.cards['DOHNA_SQUAD.fallback_attack'] = ['[gold]前排代打[/gold]。\n造成{FallbackDamage:diff()}点伤害。\n抽1张牌。','[gold]Front member substitutes[/gold].\nDeal {FallbackDamage:diff()} damage.\nDraw 1 card.','[gold]先頭の隊員が代行[/gold]。\n{FallbackDamage:diff()}ダメージを与える。\nカードを1枚引く。'][index];
output.cards['DOHNA_SQUAD.fallback_skill'] = ['[gold]前排代打[/gold]。\n获得{FallbackBlock:diff()}点[gold]格挡[/gold]。\n抽1张牌。','[gold]Front member substitutes[/gold].\nGain {FallbackBlock:diff()} [gold]Block[/gold].\nDraw 1 card.','[gold]先頭の隊員が代行[/gold]。\n[gold]ブロック[/gold]{FallbackBlock:diff()}を得る。\nカードを1枚引く。'][index];
output.cards['DOHNA_SQUAD.fallback_power'] = ['[gold]前排代打[/gold]。\n抽{FallbackCards:diff()}张牌。','[gold]Front member substitutes[/gold].\nDraw {FallbackCards:diff()} cards.','[gold]先頭の隊員が代行[/gold]。\nカードを{FallbackCards:diff()}枚引く。'][index];
output.cards['DOHNA_DOHNA_CARD_SQUAD_REVIVE_CHOICE.description'] = ['以0血复活，在后排接受本次休息治疗。\n基础休息治疗：{Heal}。生命上限：{MaxHp}。','Revive at 0 HP at the rear, then receive this rest\'s healing.\nRest healing: {Heal}. Max HP: {MaxHp}.','HP0で後衛に復活し、今回の休憩による回復を受ける。\n休憩回復量：{Heal}。最大HP：{MaxHp}。'][index];
const memberTargetText = {
  BUFFER: ['使一名存活队员接下来受到的{BufferPower:diff()}次生命损伤无效。', 'Prevent the next {BufferPower:diff()} instances of HP loss for a living member.', '生存している隊員1人の次の{BufferPower:diff()}回のHP減少を無効化する。'],
  NOT_YET: ['回复一名存活队员{Heal:diff()}点生命。','Heal a living member for {Heal:diff()} HP.','生存している隊員1人のHPを{Heal:diff()}回復する。'],
  FLAME_BARRIER: ['给予一名存活队员{Block:diff()}点[gold]格挡[/gold]。\n该队员在这个回合每受到一次攻击，对攻击者造成{DamageBack:diff()}点伤害。','Give a living member {Block:diff()} [gold]Block[/gold].\nWhenever they are attacked this turn, deal {DamageBack:diff()} damage back.','生存している隊員1人に{Block:diff()}[gold]ブロック[/gold]を与える。\nこのターン、その隊員が攻撃されるたび、攻撃者に{DamageBack:diff()}ダメージを与える。'],
  ABRASIVE: ['给予一名存活队员{DexterityPower:diff()}点[gold]敏捷[/gold]和{ThornsPower:diff()}点[gold]荆棘[/gold]。','Give a living member {DexterityPower:diff()} [gold]Dexterity[/gold] and {ThornsPower:diff()} [gold]Thorns[/gold].','生存している隊員1人に[gold]敏捷[/gold]{DexterityPower:diff()}と[gold]トゲ[/gold]{ThornsPower:diff()}を与える。'],
  ENERGY_SURGE: ['获得{Energy:energyIcons()}。','Gain {Energy:energyIcons()}.','{Energy:energyIcons()}を得る。']
};
for (const [id, text] of Object.entries(memberTargetText)) output.cards[`DOHNA_DOHNA_CARD_SQUAD_${id}.description`] = text[index];
output.characters['DOHNA_SQUAD.shared_powers'] = ['小队共用能力','Shared squad powers','小隊共通の能力'][index];
output.characters['DOHNA_SQUAD.source_title'] = ['小队共用 · 来源','Shared · Source','小隊共通・付与元'][index];
output.characters['DOHNA_SQUAD.source_description'] = ['来源：{Role}。此能力归小队共用。','Source: {Role}. This power belongs to the squad.','付与元：{Role}。この能力は小隊で共有する。'][index];
output.relics['DOHNA_DOHNA_RELIC_SQUAD_STANDARD.description'] = ['旧版小队标记。保留旧存档身份，不再提供战斗效果。','Legacy squad marker. Retained for old saves; no longer grants combat effects.','旧版の小隊マーカー。セーブ互換性のために残されているが、戦闘効果はない。'][index];
let patch = '*** Begin Patch\n';
for (const [table, values] of Object.entries(output)) {
  const file = `DohnaDohna/localization/${lang}/${table}.json`;
  const before = fs.readFileSync(file, 'utf8').trimEnd().split(/\r?\n/);
  const after = JSON.stringify(values, null, 2).split('\n');
  patch += `*** Update File: ${file}\n@@\n` + before.map(l => '-' + l).join('\n') + '\n' + after.map(l => '+' + l).join('\n') + '\n';
}
console.log(patch + '*** End Patch');
