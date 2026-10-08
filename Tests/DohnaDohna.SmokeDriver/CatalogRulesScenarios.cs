using System.Text.Json;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Powers;
using DohnaDohna.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using Godot;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyCatalogRules(Player owner, RunState run)
    {
        using var selector = CardSelectCmd.UseSelector(new CatalogSelector());
        var context = new BlockingPlayerChoiceContext();
        var fixture = SquadRunState.Create(["tora", "antena", "alyce", "kikuchiyo"]);
        foreach (var member in fixture.Members) member.Hp = member.MaxHp = 200;
        await SetCatalogRoster(owner, run, fixture);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 10000);
        var target = combat.Enemies.First();
        var antena = squad.GetActor("antena");
        var tora = squad.GetActor("tora");
        foreach (var enemy in combat.Enemies) await CreatureCmd.GainBlock(enemy, 100, ValueProp.Unpowered, null);
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadSweepingBeam>(owner), null);
        CatalogCheck(squad.Living.Where(c => c != antena).All(c => c.GetPowerAmount<DexterityPower>() == combat.Enemies.Count),
            "Antena first attack counts blocked targets once each");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadSweepingBeam>(owner), null);
        CatalogCheck(squad.Living.Where(c => c != antena).All(c => c.GetPowerAmount<DexterityPower>() == combat.Enemies.Count),
            "Antena second attack grants no additional dexterity");
        int rewards = tora.GetPowerAmount<SquadTemporaryStrength>();
        await PowerCmd.Apply<ArtifactPower>(context, tora, 1, tora, null);
        await PowerCmd.Apply<ArtifactPower>(context, tora, 2, tora, null);
        await PowerCmd.Remove<ArtifactPower>(tora);
        await PowerCmd.Apply<ArtifactPower>(context, tora, 1, tora, null);
        CatalogCheck(tora.GetPowerAmount<SquadTemporaryStrength>() == rewards + 1, "Tora rewards a positive type once, no recursion/reapply farm");
        foreach (var enemy in combat.Enemies) await CreatureCmd.LoseBlock(context, enemy, enemy.Block, null);
        int hp = target.CurrentHp;
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadBeamCell>(owner), target);
        CatalogCheck(hp - target.CurrentHp == 7, "Alyce native 3 plus supported-target 4 in the same hit");
        await PowerCmd.Remove<VulnerablePower>(target);
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadCreativeAi>(owner), null);
        CatalogCheck(owner.Creature.HasPower<CreativeAiPower>() && !antena.HasPower<CreativeAiPower>(), "generated-card power belongs to shared player");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadLoop>(owner), null);
        CatalogCheck(antena.HasPower<LoopPower>() && !owner.Creature.HasPower<LoopPower>(), "orb automatic trigger belongs to Antena");
        await OrbCmd.Channel<FrostOrb>(context, owner);
        await squad.Swap(squad.GetActor("alyce"));
        int block = squad.Front.Block;
        await owner.PlayerCombatState!.OrbQueue.Orbs.Single().Passive(context, null);
        CatalogCheck(squad.Front.Block == block + 2 && antena.Block == 0, "native frost protects current front");
        await CreatureCmd.Kill(antena);
        CatalogCheck(owner.Creature.HasPower<CreativeAiPower>() && owner.PlayerCombatState.OrbQueue.Orbs.Count == 0,
            "source death keeps shared power but removes old orbs");

        var fallback = combat.CreateCard<SquadTempest>(owner);
        fallback.UpgradeInternal();
        CatalogCheck(fallback.IsFallback && !fallback.EnergyCost.CostsX && fallback.EnergyCost.GetWithModifiers(CostModifiers.All) == 1,
            "dead X card uses fixed one-cost substitution");
        fallback.EnergyCost.SetUntilPlayed(0);
        // STS2 Tempest has no printed Exhaust. Exercise preservation of an
        // externally granted keyword as well as its external cost modifier.
        CardCmd.ApplyKeyword(fallback, CardKeyword.Exhaust);
        var copy = (SquadTempest)combat.CloneCard(fallback);
        GD.Print($"DOHNA_COST_COPY before={fallback.EnergyCost.GetWithModifiers(CostModifiers.All)} after={copy.EnergyCost.GetWithModifiers(CostModifiers.All)} x={copy.EnergyCost.CostsX} keywords={string.Join(',', copy.Keywords)} locals={copy.EnergyCost.HasLocalModifiers}");
        CatalogCheck(copy.EnergyCost.GetWithModifiers(CostModifiers.All) == 0 && copy.EnergyCost.GetAmountToSpend() == 0
            && copy.EnergyCost.GetResolved() == 0 && copy.Keywords.Contains(CardKeyword.Exhaust),
            "substitution clone preserves external cost and exhaust keyword");
        await CardCmd.AutoPlay(context, copy, null);
        CatalogCheck(squad.Front.Block >= block + 10 && owner.PlayerCombatState.OrbQueue.Orbs.Count == 0
            && copy.Pile?.Type == PileType.Exhaust, "upgraded substitute skill blocks / native active exhaust / no original channel");
        var absent = combat.CreateCard<SquadBackstab>(owner);
        CatalogCheck(absent.IsFallback && absent.EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "absent role substitutes without joining roster");
        hp = target.CurrentHp;
        await CardCmd.AutoPlay(context, absent, target);
        CatalogCheck(hp - target.CurrentHp == 10 && absent.Pile?.Type == PileType.Exhaust,
            "front Alyce substitutes with its own supported damage bonus / preserves exhaust");
        var powerFallback = combat.CreateCard<SquadCapacitor>(owner);
        int slots = owner.PlayerCombatState.OrbQueue.Capacity;
        await CardCmd.AutoPlay(context, powerFallback, null);
        CatalogCheck(owner.PlayerCombatState.OrbQueue.Capacity == slots && powerFallback.Pile == null,
            "substitute power draws and leaves native piles without expanding slots");
        var options = CardCreationOptions.ForRoom(owner, RoomType.Monster).GetPossibleCards(owner).ToArray();
        CatalogCheck(options.OfType<SquadCatalogCard>().Count() == 80
            && options.All(c => c is not SquadCardModel { FixedRole: { } role } || fixture.Members.Any(m => m.RoleId == role)),
            "native candidate pool contains selected 80 catalog cards");
        var reward = CardFactory.CreateForReward(owner, 3, CardCreationOptions.ForRoom(owner, RoomType.Monster)).ToArray();
        CatalogCheck(reward.Length == 3 && reward.All(r => r.Card is SquadCatalogCard),
            "native reward generation excludes basic / ancient / retired models");
        foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
            CatalogCheck(CardFactory.CreateForMerchant(owner, options, type).Card != null, "native shop produces " + type);
        var transform = CardFactory.CreateRandomCardForTransform(combat.CreateCard<SquadStrike>(owner), true, run.Rng.CombatCardGeneration);
        CatalogCheck(transform is not SquadCardModel { FixedRole: { } transformedRole } || fixture.Members.Any(m => m.RoleId == transformedRole),
            "native transform obeys selected roster");
        await CardCmd.Discard(context, PileType.Hand.GetPile(owner).Cards.ToArray());
        await Hook.BeforeHandDraw(combat, owner, context);
        CatalogCheck(PileType.Hand.GetPile(owner).Cards.Count == 1 && PileType.Hand.GetPile(owner).Cards[0].Type == CardType.Power,
            "shared Creative AI still generates after its source dies");
        await OrbCmd.Channel<FrostOrb>(context, owner);
        int deathBlock = squad.Front.Block;
        await Hook.AfterPlayerTurnStart(combat, context, owner);
        CatalogCheck(squad.Front.Block == deathBlock, "dead Antena personal Loop no longer triggers external orbs");
        await Screenshot("catalog-substitution.png");
        await FinishCombat(owner);

        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
        await SetCatalogRoster(owner, run, SquadRunState.Create(["medhico", "kirakira", "joker", "zappa"]));
        await RelicCmd.Replace(owner.GetRelic<KirakiraEmblem>()!, ModelDb.Relic<KirakiraEmblemPlus>().ToMutable());
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        combat = owner.Creature.CombatState!;
        CatalogCheck(PileType.Hand.GetPile(owner).Cards.Count(c => c.Keywords.Contains(CardKeyword.Retain)) == 2,
            "refined Kirakira selects two combat copies");
        var zappa = squad.GetActor("zappa");
        var joker = squad.GetActor("joker");
        await PowerCmd.Apply<DexterityPower>(context, zappa, 10, zappa, null);
        await Hook.AfterSideTurnEnd(combat, CombatSide.Player, squad.Living.ToArray());
        CatalogCheck(zappa.Block == 4, "Zappa end-side native hook grants four without Dexterity");
        await CreatureCmd.SetCurrentHp(zappa, zappa.MaxHp - 10);
        await squad.MoveToFront(squad.GetActor("medhico"));
        CatalogCheck(zappa.CurrentHp == zappa.MaxHp - 7, "Medhico insertion heals the former front");
        CatalogCheck(joker.GetPowerAmount<VigorPower>() == 2, "Joker shifted once by insertion");
        await CreatureCmd.Kill(squad.GetActor("kirakira"));
        CatalogCheck(joker.GetPowerAmount<VigorPower>() == 2, "death compaction grants no Joker vigor");
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
        await VerifyLastEnemyRewards(owner, run);
        await VerifyRefinedPassiveValues(owner, run);
        owner = await VerifyCatalogReload(owner, run);
        run = (RunState)owner.RunState;
        // Reconstruct only the actual previous version's marker in the isolated
        // fixture. This does not touch or claim to inspect a user's daily save.
        foreach (var relic in owner.Relics.OfType<SquadRoleRelic>().ToArray()) await RelicCmd.Remove(relic);
        await RelicCmd.Obtain<SquadStandard>(owner);
        owner = await VerifyCatalogReload(owner, run, migrateLegacy:true);
        await RelicCmd.Remove(owner.Relics.OfType<SquadRoleRelic>().First());
        await VerifyCatalogReload(owner, (RunState)owner.RunState);
    }

    private async Task VerifyRefinedPassiveValues(Player owner, RunState run)
    {
        var context = new BlockingPlayerChoiceContext();
        foreach (var ids in new[] { new[] { "tora", "alyce", "antena", "kikuchiyo" }, new[] { "porno", "zappa", "joker", "kuma" } })
        {
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
            await SetCatalogRoster(owner, run, SquadRunState.Create(ids));
            var touch = ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas>();
            foreach (var relic in owner.Relics.OfType<SquadRoleRelic>().ToArray())
                await RelicCmd.Replace(relic, touch.GetUpgradedStarterRelic(relic).ToMutable());
            await EnterBattle(owner);
            var squad = SquadCombatState.Get(owner);
            var combat = owner.Creature.CombatState!;
            foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 10000);
            if (ids[0] == "tora")
            {
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadSweepingBeam>(owner), null);
                CatalogCheck(squad.GetActor("alyce").GetPowerAmount<DexterityPower>() == 2 * combat.Enemies.Count,
                    "refined Antena grants two dexterity per target");
                CatalogCheck(squad.GetActor("tora").GetPowerAmount<SquadTemporaryStrength>() == 2,
                    "refined Tora grants two strength per positive type");
                var target = combat.Enemies.First();
                int hp = target.CurrentHp, hand = PileType.Hand.GetPile(owner).Cards.Count;
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), target);
                CatalogCheck(PileType.Hand.GetPile(owner).Cards.Count == hand + 2, "refined Kikuchiyo draws two on first attack");
                hp = target.CurrentHp;
                await CardCmd.AutoPlay(context, combat.CreateCard<SquadBeamCell>(owner), target);
                CatalogCheck(hp - target.CurrentHp == 11, "refined Alyce adds eight in the same damage hit");
            }
            else
            {
                CatalogCheck(squad.Living.All(c => c.GetPowerAmount<StrengthPower>() == 2), "refined Kuma covers allies with two strength");
                CatalogCheck(squad.GetActor("porno").GetPowerAmount<ThornsPower>() == 4
                    && squad.GetActor("zappa").GetPowerAmount<ThornsPower>() == 4, "refined Porno grants four thorns");
                await squad.MoveToFront(squad.GetActor("zappa"));
                CatalogCheck(squad.GetActor("joker").GetPowerAmount<VigorPower>() == 4, "refined Joker grants four vigor once per reorder");
                await Hook.AfterSideTurnEnd(combat, CombatSide.Player, squad.Living.ToArray());
                CatalogCheck(squad.Front.Block == 8, "refined Zappa grants eight block");
            }
            await FinishCombat(owner);
        }
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
    }

    private async Task<Player> VerifyCatalogReload(Player owner, RunState run, bool migrateLegacy = false)
    {
        var expected = JsonSerializer.Serialize(SquadStore.Get(owner));
        var ids = migrateLegacy ? owner.Relics.Where(r => r is not SquadStandard).Select(r => r.Id)
            .Concat(SquadStore.Get(owner).Members.Select(m => SquadRoleRelic.Basic(m.RoleId).Id)).ToArray()
            : owner.Relics.Select(r => r.Id).ToArray();
        await SaveManager.Instance.SaveRun(null);
        await SaveManager.Instance.SaveRun(null);
        var disk = SaveManager.Instance.LoadRunSave();
        CatalogCheck(disk.Status == MegaCrit.Sts2.Core.Saves.ReadSaveStatus.Success && disk.SaveData != null, "native disk save");
        await NGame.Instance!.ReturnToMainMenu();
        var saved = disk.SaveData!;
        var loaded = RunState.FromSerializable(saved);
        await RunManager.Instance.SetUpSavedSingleplayer(loaded, saved);
        await NGame.Instance.LoadRun(loaded, saved.PreFinishedRoom);
        var loadedOwner = loaded.Players.Single();
        CatalogCheck(JsonSerializer.Serialize(SquadStore.Get(loadedOwner)) == expected
            && loadedOwner.Relics.Select(r => r.Id).SequenceEqual(ids), migrateLegacy
                ? "native reload migrates exact legacy starter marker once"
                : "native reload preserves formation, HP, refined IDs and no starter replenishment");
        // The caller's original run has been unloaded; no more operations may use its players.
        return loadedOwner;
    }

    private async Task VerifyLastEnemyRewards(Player owner, RunState run)
    {
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
        foreach (var prototype in new CardModel[] { ModelDb.Card<SquadFeed>(), ModelDb.Card<SquadHandOfGreed>(), ModelDb.Card<SquadSunder>(), ModelDb.Card<SquadTheHunt>() })
        {
            await SetCatalogRoster(owner, run, SquadRunState.Create(["kuma", "alyce", "antena", "kikuchiyo"]));
            await EnterBattle(owner);
            var squad = SquadCombatState.Get(owner);
            var combat = owner.Creature.CombatState!;
            if (prototype is SquadSunder)
            {
                var first = combat.Enemies.First();
                await CreatureCmd.SetCurrentHp(first, 1);
                int before = owner.PlayerCombatState!.Energy;
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), combat.CreateCard(prototype, owner), first);
                CatalogCheck(owner.PlayerCombatState.Energy == before + 3, "Sunder nonfinal kill awards native energy");
            }
            foreach (var other in combat.Enemies.Skip(1).ToArray()) await CreatureCmd.Kill(other, true);
            var target = combat.Enemies.First();
            await CreatureCmd.SetCurrentHp(target, 1);
            int maxHp = squad.Front.MaxHp, gold = owner.Gold, energy = owner.PlayerCombatState!.Energy;
            var card = combat.CreateCard(prototype, owner);
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, target);
            CatalogCheck(target.IsDead, "native lethal " + card.Id.Entry);
            if (card is SquadFeed) CatalogCheck(squad.GetActor("kikuchiyo").MaxHp == maxHp + 3, "Feed final kill awards personal max HP");
            if (card is SquadHandOfGreed) CatalogCheck(owner.Gold == gold + 20, "Hand of Greed final kill awards gold");
            // Native PlayerCmd.GainEnergy intentionally does nothing at IsEnding.
            // Persistent fatal rewards still execute; do not bypass that native rule.
            if (card is SquadSunder) CatalogCheck(owner.PlayerCombatState!.Energy == energy, "Sunder final kill follows native ending energy no-op");
            if (card is SquadTheHunt) CatalogCheck(((CombatRoom)run.CurrentRoom!).ExtraRewards.TryGetValue(owner, out var rewards)
                && rewards.OfType<MegaCrit.Sts2.Core.Rewards.CardReward>().Count() == 1, "The Hunt final kill adds one native card reward");
            await CombatManager.Instance.CheckWinCondition();
            if (card is SquadFeed) CatalogCheck(SquadStore.Get(owner).Members.Single(m => m.RoleId == "kikuchiyo").MaxHp == maxHp + 3,
                "Feed final-kill HP included in native post-combat snapshot");
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
        }
    }
}
