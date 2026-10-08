using System.Text.Json;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Patches;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using DohnaDohna.Powers;
using DohnaDohna.Relics;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    // Host-supported test selector. This is command verification, not an input/UI claim.
    private sealed class CatalogSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult<IEnumerable<CardModel>>(options.Take(minSelect).ToArray());
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new InvalidOperationException("Catalog command fixture must not claim rewards.");
    }

    private static void CatalogCheck(bool condition, string message)
    {
        if (!condition) throw new Exception("Catalog: " + message);
        GD.Print("DOHNA_CATALOG_CHECK " + message);
    }

    private static async Task SetCatalogRoster(Player owner, RunState run, SquadRunState state)
    {
        SquadStore.State.Set(run, owner.NetId, state);
        owner.Creature.SetMaxHpInternal(state.Front!.MaxHp);
        owner.Creature.SetCurrentHpInternal(state.Front.Hp);
        // A debug roster replacement is not a production run transition. Match
        // its starter inventory explicitly through native commands in the fixture.
        foreach (var relic in owner.Relics.OfType<SquadRoleRelic>().ToArray()) await RelicCmd.Remove(relic);
        foreach (var member in state.Members) await RelicCmd.Obtain(SquadRoleRelic.Basic(member.RoleId).ToMutable(), owner);
    }

    private async Task VerifyCatalog(Player owner, RunState run)
    {
        var cards = ModelDb.Character<DohnaSquad>().CardPool.AllCards.OfType<SquadCatalogCard>().ToArray();
        CatalogCheck(cards.Length == 170 && cards.Select(c => c.Prototype).Distinct().Count() == 170, "170 unique prototypes");
        CatalogCheck(cards.Count(c => c.FixedRole == null && c.Rarity == CardRarity.Common) == 20, "20 common public cards");
        foreach (var role in RoleDefinition.All)
        {
            CatalogCheck(cards.Count(c => c.FixedRole == role.Id && c.Rarity == CardRarity.Uncommon) == 9
                && cards.Count(c => c.FixedRole == role.Id && c.Rarity == CardRarity.Rare) == 6, role.Id + " 9 uncommon + 6 rare");
        }
        var saved = SquadStore.Get(owner).Copy();
        CatalogCheck(owner.Relics.OfType<SquadRoleRelic>().Select(r => r.Role).Order()
            .SequenceEqual(saved.Members.Select(m => m.RoleId).Order()), "new run grants exactly the four selected native starter relics");
        var metadata = new List<object>();
        foreach (var canonical in ModelDb.Character<DohnaSquad>().CardPool.AllCards
            .Where(c => c is SquadCatalogCard || c.Rarity is CardRarity.Basic or CardRarity.Ancient).Append(ModelDb.Card<SquadReviveChoice>()))
        {
            var role = (canonical as SquadCardModel)?.FixedRole;
            var roster = role == null ? saved.Members.Select(m => m.RoleId) : new[] { role }.Concat(RoleDefinition.All.Select(r => r.Id).Where(id => id != role).Take(3));
            SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(roster));
            var card = run.CreateCard(canonical, owner);
            var baseVars = card.DynamicVars.ToDictionary(p => p.Key, p => p.Value.BaseValue);
            var baseText = card.GetDescriptionForPile(PileType.Deck);
            var baseKeywords = card.Keywords.Select(k => k.ToString()).ToArray();
            var baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
            if (card.IsUpgradable) card.UpgradeInternal();
            metadata.Add(new { id = canonical.Id.ToString(), title = canonical.TitleLocString.GetFormattedText(),
                cost = baseCost, upgradedCost = card.EnergyCost.GetWithModifiers(CostModifiers.None), type = card.Type.ToString(), rarity = card.Rarity.ToString(),
                role, melee = (card as SquadCardModel)?.IsMelee ?? false, prototype = (card as SquadCatalogCard)?.Prototype,
                portrait = card.PortraitPath, baseVars, upgradedVars = card.DynamicVars.ToDictionary(p => p.Key, p => p.Value.BaseValue),
                baseText, upgradedText = card.GetDescriptionForPile(PileType.Deck), baseKeywords,
                upgradedKeywords = card.Keywords.Select(k => k.ToString()).ToArray() });
        }
        SquadStore.State.Set(run, owner.NetId, saved);
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "runtime-cards.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        CatalogCheck(metadata.Count == 176, "runtime export 170 + 4 basic models + transcendent + revival");

        using var selector = CardSelectCmd.UseSelector(new CatalogSelector());
        var context = new BlockingPlayerChoiceContext();
        foreach (var roster in new[] {
            new[] { "kuma", "medhico", "joker", "tora" },
            new[] { "kirakira", "kikuchiyo", "alyce", "antena" },
            new[] { "porno", "zappa", "kuma", "tora" } })
        {
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            var fixture = SquadRunState.Create(roster);
            await SetCatalogRoster(owner, run, fixture);
            CatalogCheck(SquadPool.ForPlayer(owner, cards).Count() == 80, string.Join(',', roster) + " 80-card pool");
            await EnterBattle(owner);
            var squad = SquadCombatState.Get(owner);
            var combat = owner.Creature.CombatState!;
            CatalogCheck(squad.Living.Count() == 4 && combat.Players.Count == 1, "one player / four native members");
            foreach (var member in squad.Living)
            {
                CatalogCheck(member.MaxHp == RoleDefinition.Get(squad.RoleOf(member)).StartingHp, squad.RoleOf(member) + " new-run HP");
                CatalogCheck(member.Powers.OfType<SquadInnatePower>().Count() == 1, squad.RoleOf(member) + " innate power");
            }
            foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 10000);
            var target = combat.Enemies.First();
            if (roster.Contains("kirakira"))
                CatalogCheck(PileType.Hand.GetPile(owner).Cards.Any(c => c.Keywords.Contains(CardKeyword.Retain)), "opening native retain selection");
            if (roster.Contains("antena"))
            {
                CatalogCheck(owner.PlayerCombatState!.OrbQueue.Capacity == 3, "native three orb slots");
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadBallLightning>(owner), target);
                CatalogCheck(owner.PlayerCombatState.OrbQueue.Orbs.Single() is LightningOrb, "Ball Lightning native channel");
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadDefragment>(owner), null);
                CatalogCheck(squad.GetActor("antena").GetPowerAmount<FocusPower>() == 1 && owner.Creature.GetPowerAmount<FocusPower>() == 0,
                    "Focus stays on Antena");
                await Screenshot("catalog-orbs.png");
                var front = squad.Front;
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadPommelStrike>(owner), target);
                CatalogCheck(squad.FrontRole == "kikuchiyo" && squad.Front != front, "melee inserts real owner before attack");
                await CreatureCmd.Kill(squad.GetActor("antena"));
                CatalogCheck(owner.PlayerCombatState.OrbQueue.Orbs.Count == 0 && owner.PlayerCombatState.OrbQueue.Capacity == 3,
                    "Antena death removes orbs without removing capacity");
                var fallback = combat.CreateCard<SquadBallLightning>(owner);
                CatalogCheck(fallback.IsFallback && fallback.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "dead-role substitution cost");
                await CardCmd.AutoPlay(context, fallback, target);
                CatalogCheck(owner.PlayerCombatState.OrbQueue.Orbs.Count == 0 && fallback.Pile?.Type != PileType.Exhaust,
                    "dead card remains playable / does not execute channel");
                await OrbCmd.Channel<FrostOrb>(context, owner);
                CatalogCheck(owner.PlayerCombatState.OrbQueue.Orbs.Count == 1, "external orbs still work without living Antena");
            }
            else
            {
                await squad.Swap(squad.GetActor("kuma"));
                CatalogCheck(squad.Living.All(c => c.GetPowerAmount<StrengthPower>() >= 1), "Kuma front aura covers allies");
                await squad.Swap(squad.GetActor("tora"));
                CatalogCheck(squad.GetActor("kuma").GetPowerAmount<StrengthPower>() == 1, "aura reorder does not accumulate");
                if (roster.Contains("medhico"))
                {
                    var tora = squad.GetActor("tora");
                    await CreatureCmd.SetCurrentHp(tora, tora.MaxHp - 8);
                    await squad.Swap(squad.GetActor("medhico"));
                    CatalogCheck(tora.CurrentHp == tora.MaxHp - 5, "Medhico exchange heals 3");
                    await squad.Swap(tora);
                    CatalogCheck(tora.CurrentHp == tora.MaxHp - 5, "Medhico only once per battle");
                    CatalogCheck(squad.GetActor("joker").GetPowerAmount<VigorPower>() == 0, "unmoved Joker gets no vigor");
                    await squad.MoveToFront(squad.GetActor("joker"));
                    CatalogCheck(squad.GetActor("joker").GetPowerAmount<VigorPower>() == 2, "Joker once per effective reorder");
                }
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadShrugItOff>(owner), null);
                CatalogCheck(squad.Front.Block >= 8, "public block belongs to front");
            }
            var order = squad.Order.ToArray();
            await Screenshot("catalog-" + roster[0] + ".png");
            await FinishCombat(owner);
            CatalogCheck(SquadStore.Get(owner).Members.Select(m => m.RoleId).SequenceEqual(order), "formation persisted after combat");
        }
    }

    private async Task VerifyRoleRelics(Player owner, RunState run)
    {
        CatalogCheck(owner.Relics.OfType<SquadRoleRelic>().Count() == 4, "exactly four starting native relics");
        var touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
        foreach (var role in RoleDefinition.All)
        {
            var basic = SquadRoleRelic.Basic(role.Id);
            var upgrade = touch.GetUpgradedStarterRelic(basic);
            CatalogCheck(basic.Rarity == RelicRarity.Starter && upgrade is SquadRoleRelic { Multiplier: 2 } plus
                && plus.Role == role.Id && plus.Rarity == RelicRarity.Starter, role.Id + " native refinement mapping / starter rarity");
            CatalogCheck(!basic.IsAllowedInShops && !upgrade.IsAllowedInShops, role.Id + " no shop drop");
            CatalogCheck(!basic.DynamicDescription.GetFormattedText().Contains("DOHNA_")
                && !upgrade.DynamicDescription.GetFormattedText().Contains("DOHNA_"), role.Id + " localized basic/refined description");
        }
        await SetCatalogRoster(owner, run, SquadRunState.Create(["medhico", "joker", "tora", "kuma"]));
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var kuma = squad.GetActor("kuma");
        var med = squad.GetActor("medhico");
        CatalogCheck(med.GetPowerAmount<StrengthPower>() == 1, "starter aura +1");
        owner.GetRelic<KumaEmblem>()!.IsWax = true;
        await RelicCmd.Melt(owner.GetRelic<KumaEmblem>()!);
        CatalogCheck(med.GetPowerAmount<StrengthPower>() == 0 && !kuma.HasPower<KumaInnate>(), "melting removes owned aura and executor");
        await RelicCmd.Remove(owner.GetRelic<KumaEmblem>()!);
        await RelicCmd.Obtain<KumaEmblem>(owner);
        CatalogCheck(med.GetPowerAmount<StrengthPower>() == 1, "native regain installs aura once");
        touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
        CatalogCheck(touch.SetupForPlayer(owner), "Touch preview offers native selection");
        var obtaining = RelicCmd.Obtain(touch, owner);
        MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen? selection = null;
        for (int frame = 0; frame < 300 && selection == null; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            selection = GetTree().Root.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen>().SingleOrDefault();
        }
        CatalogCheck(selection != null, "native refinement grid opens without command selector override");
        var holders = selection!.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NGridCardHolder>()
            .Where(h => h.CardModel is SquadRelicUpgradeChoice).ToArray();
        CatalogCheck(holders.Length == 4, "native refinement grid displays all four starter relics");
        var chosen = holders.Single(h => ((SquadRelicUpgradeChoice)h.CardModel).Original!.Role == "medhico");
        await ClickNativeChoice(chosen, "refinement");
        await obtaining.WaitAsync(TimeSpan.FromSeconds(15));
        CatalogCheck(owner.GetRelic<MedhicoEmblemPlus>() != null && owner.GetRelic<MedhicoEmblem>() == null
            && owner.Relics.OfType<SquadRoleRelic>().Count() == 4, "Touch selects exactly one of four / native replace");
        await CreatureCmd.SetCurrentHp(kuma, kuma.MaxHp - 8);
        await squad.Swap(med);
        CatalogCheck(kuma.CurrentHp == kuma.MaxHp - 2, "refined Medhico heals six");
        await RelicCmd.Replace(owner.GetRelic<KumaEmblem>()!, ModelDb.Relic<KumaEmblemPlus>().ToMutable());
        await squad.Swap(kuma);
        CatalogCheck(med.GetPowerAmount<StrengthPower>() == 2, "refined aura replaces one with two without stacking");
        await RelicCmd.Remove(owner.GetRelic<KumaEmblemPlus>()!);
        CatalogCheck(med.GetPowerAmount<StrengthPower>() == 0, "removing refinement withdraws exactly its contribution");
        await Screenshot("native-role-relics.png");
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        var special = owner.Deck.Cards.OfType<SquadSpecial>().Single();
        CardCmd.Upgrade(special);
        CatalogCheck(special.DynamicVars.Damage.BaseValue == 10, "ordinary Special upgrade remains ten");
        var tooth = (ArchaicTooth)ModelDb.Relic<ArchaicTooth>().ToMutable();
        CatalogCheck(tooth.SetupForPlayer(owner), "Archaic Tooth recognizes starter special");
        await RelicCmd.Obtain(tooth, owner);
        var transformed = owner.Deck.Cards.OfType<SquadTranscendentSpecial>().Single();
        CatalogCheck(transformed.IsUpgraded && transformed.DynamicVars.Damage.BaseValue == 20
            && transformed.EffectMultiplier == 2 && !owner.Deck.Cards.OfType<SquadSpecial>().Any(),
            "native transcendence preserves upgrade and doubles damage/effects");
        CatalogCheck(owner.Deck.Cards.OfType<SquadStrike>().All(c => c.DynamicVars.Damage.BaseValue == 6)
            && owner.Deck.Cards.OfType<SquadDefend>().All(c => c.DynamicVars.Block.BaseValue == 5), "native tooth leaves strikes/defends unchanged");
        await Screenshot("archaic-tooth.png");
    }

    private async Task VerifyAllPrototypes(Player owner, RunState run)
    {
        var cards = ModelDb.Character<DohnaSquad>().CardPool.AllCards.OfType<SquadCatalogCard>().ToArray();
        using var selector = CardSelectCmd.UseSelector(new CatalogSelector());
        var context = new BlockingPlayerChoiceContext();
        int played = 0;
        foreach (var group in cards.GroupBy(c => c.FixedRole))
        {
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            var roster = new[] { group.Key ?? "kuma" }.Concat(new[] { "zappa", "medhico", "joker", "tora" }
                .Where(id => id != group.Key)).Take(4).ToArray();
            var fixture = SquadRunState.Create(roster);
            // Endurance fixture, not changed production HP: keep all 15/20 prototypes
            // in one combat so native shared/per-member powers also interact.
            foreach (var member in fixture.Members) member.Hp = member.MaxHp = 2000;
            await SetCatalogRoster(owner, run, fixture);
            await EnterBattle(owner);
            var squad = SquadCombatState.Get(owner);
            var combat = owner.Creature.CombatState!;
            foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 1000000);
            foreach (var canonical in group.OrderBy(c => c.Type == CardType.Power ? 1 : 0))
            {
                for (int upgrade = 0; upgrade <= 1; upgrade++)
                {
                    await PlayerCmd.SetEnergy(3, owner);
                    await PlayerCmd.SetStars(3, owner);
                    if (PileType.Hand.GetPile(owner).Cards.Count > 5)
                        await CardCmd.Discard(context, PileType.Hand.GetPile(owner).Cards.Skip(5).ToArray());
                    for (int n = PileType.Hand.GetPile(owner).Cards.Count; n < 3; n++)
                        await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<SquadStrike>(owner), PileType.Hand, owner);
                    var card = (SquadCatalogCard)combat.CreateCard(canonical, owner);
                    if (upgrade == 1) card.UpgradeInternal();
                    if (card is SquadGrandFinale)
                        await CardPileCmd.Add(PileType.Draw.GetPile(owner).Cards.ToArray(), PileType.Discard);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
                    Creature? target = card.TargetType == TargetType.AnyEnemy ? combat.HittableEnemies.First()
                        : card.TargetType == SquadTargeting.Member ? squad.Front
                        : card.TargetType == SquadTargeting.OtherMember ? squad.Living.First(c => c != card.Actor) : null;
                    GD.Print($"DOHNA_CATALOG_PLAY_BEGIN {card.Prototype} upgrade={upgrade}");
                    await CardCmd.AutoPlay(context, card, target).WaitAsync(TimeSpan.FromSeconds(18));
                    CatalogCheck(!squad.Living.Any(c => c.IsDead) && card.Pile?.Type != PileType.Play,
                        $"native play {card.Prototype} upgrade={upgrade}");
                    played++;
                }
            }
            await Screenshot("catalog-all-" + (group.Key ?? "common") + ".png");
            await FinishCombat(owner);
        }
        CatalogCheck(played == 340, "all 170 prototypes played base and upgraded");
    }
}
