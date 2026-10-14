using System.Text.Json;
using DohnaDohna.Code.Squad;
using DohnaDohna.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyPowerOwnership(Player owner, RunState run)
    {
        var fixture = SquadRunState.Create(["kuma", "alyce", "antena", "zappa"]);
        await SetCatalogRoster(owner, run, fixture);
        await RelicCmd.Obtain(ModelDb.Relic<Vajra>().ToMutable(), owner);
        await RelicCmd.Obtain(ModelDb.Relic<OddlySmoothStone>().ToMutable(), owner);
        await RelicCmd.Obtain(ModelDb.Relic<Anchor>().ToMutable(), owner);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var context = new ThrowingPlayerChoiceContext();
        CatalogCheck(squad.Front.GetPowerAmount<StrengthPower>() == 1 && squad.Front.GetPowerAmount<DexterityPower>() == 1,
            "AfterRoomEntered relics reach a real member before combat starts");
        CatalogCheck(squad.Front.Block == 10 && owner.Creature.Block == 0, "BeforeCombatStart Anchor reaches front once");
        CatalogCheck(owner.Creature.Powers.All(p => p is not StrengthPower and not DexterityPower), "no stranded personal startup powers");
        CatalogCheck(owner.Creature.CombatState!.Players.Count == 1 && squad.Actors.All(c => !c.IsPlayer && c.IsPet && c.PetOwner == owner),
            "native one-player/four-owned-allies identity retained");

        var first = await PowerCmd.Apply<BufferPower>(context, owner.Creature, 1, owner.Creature, null);
        var second = await PowerCmd.Apply<BufferPower>(context, owner.Creature, 2, owner.Creature, null);
        CatalogCheck(ReferenceEquals(first, second) && first!.Owner == squad.Front && first.Amount == 3,
            "generic Apply returns the actual stacked instance");
        var actualOwner = first!.Owner;
        await squad.Swap(squad.GetActor("alyce"));
        CatalogCheck(actualOwner.GetPower<BufferPower>() == first && !squad.Front.HasPower<BufferPower>(), "swapping does not move personal powers");
        await PowerCmd.Remove(first);
        CatalogCheck(!actualOwner.HasPower<BufferPower>(), "native Remove(returned instance) removes the original recipient after swap");
        var third = await PowerCmd.Apply<BufferPower>(context, owner.Creature, 2, owner.Creature, null);
        CatalogCheck(third!.Owner == squad.Front, "next application resolves the new front");
        await PowerCmd.Remove<BufferPower>(third.Owner);
        CatalogCheck(!third.Owner.HasPower<BufferPower>(), "native generic query/remove agree on explicit member");
        var focus1 = await PowerCmd.Apply<FocusPower>(context, owner.Creature, 1, owner.Creature, null);
        var focus2 = await PowerCmd.Apply<FocusPower>(context, owner.Creature, 1, owner.Creature, null);
        CatalogCheck(focus1 == focus2 && focus1!.Owner == squad.GetActor("antena") && focus1.Amount == 2,
            "orb focus stacking returns Antena's real power");
        var shared = await PowerCmd.Apply<EnergyNextTurnPower>(context, owner.Creature, 2, owner.Creature, null);
        CatalogCheck(shared!.Owner == owner.Creature && squad.Living.All(c => !c.HasPower<EnergyNextTurnPower>()), "shared resource power exists once on real Player");
        var rear = squad.GetActor("kuma");
        var explicitPower = await PowerCmd.Apply<BufferPower>(context, rear, 2, rear, null);
        CatalogCheck(explicitPower!.Owner == rear, "explicit rear recipient is never redirected");
        await PowerCmd.Remove(explicitPower);

        var command = new GodModeConsoleCmd();
        await command.Process(owner, []).task!;
        CheckGodMode(squad);
        await Screenshot("godmode-four-native-bars.png");
        foreach (var actor in squad.Living.ToArray())
        {
            int hp = actor.CurrentHp;
            await CreatureCmd.Damage(context, actor, 3, ValueProp.Unpowered | ValueProp.Unblockable, owner.Creature.CombatState!.Enemies.First());
            CatalogCheck(actor.CurrentHp == hp && actor.GetPowerAmount<BufferPower>() == 999999998, squad.RoleOf(actor) + " native Buffer protects its own HP");
        }
        await squad.Swap(squad.GetActor("antena"));
        await command.Process(owner, []).task!;
        CatalogCheck(squad.Actors.All(c => !c.HasPower<BufferPower>() && !c.HasPower<RegenPower>() && !c.HasPower<StrengthPower>()),
            "godmode off removes all native cheat powers after swap");
        await squad.Swap(squad.GetActor("kuma"));
        CatalogCheck(squad.Living.All(c => c.GetPowerAmount<StrengthPower>() == 1),
            "reordering re-establishes only the live aura after native full-stack removal");
        await command.Process(owner, []).task!;
        await command.Process(owner, []).task!;
        await squad.Swap(squad.GetActor("zappa"));
        var auraRecipients = squad.Living.TakeWhile(c => c != squad.GetActor("kuma")).Append(squad.GetActor("kuma")).ToHashSet();
        CatalogCheck(squad.Living.All(c => c.GetPowerAmount<StrengthPower>() == (auraRecipients.Contains(c) ? 1 : 0)),
            "removed aura stacks never create negative Strength on a later reorder");
        await command.Process(owner, []).task!;
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        CheckGodMode(squad);
        await command.Process(owner, []).task!;
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        CatalogCheck(squad.Living.All(c => !c.HasPower<BufferPower>() && !c.HasPower<RegenPower>()), "disabled godmode does not return next combat");

        // A contract fixture for third-party raw-HP interception, NOT a claim of
        // running Hextech/Loadout. The third-party DLL is not installed here.
        var card = owner.Creature.CombatState!.CreateCard<SquadStrike>(owner);
        var attack = new AttackCommand(100).FromCard(card, null);
        var targets = owner.Creature.CombatState.Enemies.ToArray();
        attack.TargetingAllOpponents(owner.Creature.CombatState);
        foreach (var target in targets) await CreatureCmd.SetCurrentHp(target, 1);
        var guard = new Harmony("DohnaDohna.SmokeDriver.HpInterception");
        try
        {
            guard.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal)),
                prefix: new HarmonyMethod(typeof(LoadProbe), nameof(PreserveEnemyHp)));
            CatalogCheck(!SquadFinisher.CanFinish(attack, card, null, targets), "unmodeled raw HP interception suppresses prediction only");
            var result = await CreatureCmd.Damage(context, targets[0], 100, ValueProp.Unpowered, squad.Front);
            CatalogCheck(targets[0].CurrentHp == 1 && result.All(r => !r.WasTargetKilled), "interceptor's actual surviving result remains authoritative");
        }
        finally { guard.UnpatchAll(guard.Id); }
        var proof = new { mode = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode.ToString(),
            state = PowerOwnershipSnapshot(owner), externalHpFixture = "synthetic contract, not Hextech/Loadout runtime",
            assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is "AutoAnthony" or "NinjaSlayer" or "HextechRunes").Select(a => a.FullName).ToArray() };
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "power-ownership.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        await FinishCombat(owner);
        await VerifyVanillaPowerOwner();
    }

    private async Task VerifyVanillaPowerOwner()
    {
        await NGame.Instance!.ReturnToMainMenu();
        var run = await NGame.Instance.StartNewSingleplayerRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(), true,
            [ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Overgrowth>(), ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Hive>(), ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Glory>()],
            [], "DOHNA-NATIVE-OWNER-CONTROL", MegaCrit.Sts2.Core.Runs.GameMode.Standard);
        var player = run.Players.Single();
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster,
            model: ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.SlimesWeak>().ToMutable(), showTransition: false);
        for (int i = 0; i < 900 && player.PlayerCombatState?.Phase != PlayerTurnPhase.Play; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CatalogCheck(player.PlayerCombatState?.Phase == PlayerTurnPhase.Play && SquadCombatState.TryGet(player) == null,
            "ordinary Ironclad starts with no squad state");
        var context = new ThrowingPlayerChoiceContext();
        var first = await PowerCmd.Apply<BufferPower>(context, player.Creature, 1, player.Creature, null);
        var second = await PowerCmd.Apply<BufferPower>(context, player.Creature, 1, player.Creature, null);
        CatalogCheck(first == second && first!.Owner == player.Creature && first.Amount == 2,
            "ordinary player apply/stack/query ownership unchanged");
        var pet = await PlayerCmd.AddPet<MegaCrit.Sts2.Core.Models.Monsters.Osty>(player);
        var petPower = await PowerCmd.Apply<BufferPower>(context, pet, 1, player.Creature, null);
        CatalogCheck(petPower!.Owner == pet && pet.GetPower<BufferPower>() == petPower && first!.Amount == 2,
            "ordinary pet power remains its own native instance");
        var command = new GodModeConsoleCmd();
        await command.Process(player, []).task!;
        CatalogCheck(player.Creature.GetPowerAmount<BufferPower>() == 999999999 && petPower.Amount == 1,
            "ordinary-player godmode does not expand onto pets");
        await command.Process(player, []).task!;
        CatalogCheck(!player.Creature.HasPower<BufferPower>() && pet.GetPower<BufferPower>() == petPower,
            "ordinary-player godmode removal leaves pets unchanged");
        foreach (var speaker in new[] { player.Creature, pet })
        {
            var expected = MegaCrit.Sts2.Core.Nodes.Vfx.NThoughtBubbleVfx.GetCreatureSpeechPosition(speaker);
            var thought = MegaCrit.Sts2.Core.Nodes.Vfx.NThoughtBubbleVfx.Create("ordinary speaker", speaker, 1)!;
            speaker.GetVfxContainer()!.AddChild(thought);
            var speech = MegaCrit.Sts2.Core.Nodes.Vfx.NSpeechBubbleVfx.Create("ordinary speaker", speaker, 1)!;
            speaker.GetVfxContainer()!.AddChild(speech);
            CatalogCheck(thought.GlobalPosition.DistanceTo(expected) < .01f && speech.GlobalPosition.DistanceTo(expected) < .01f,
                "ordinary player/pet dialogue anchors unchanged: " + speaker.ModelId);
            await PreparationUntil(() => !GodotObject.IsInstanceValid(thought) && !GodotObject.IsInstanceValid(speech), "ordinary dialogue lifetime");
        }
        await FinishCombat(player);
    }

    private static bool PreserveEnemyHp(Creature __instance, ValueProp props, ref DamageResult __result)
    {
        if (!__instance.IsEnemy) return true;
        __result = new DamageResult(__instance, props);
        return false;
    }

    private static void CheckGodMode(SquadCombatState squad)
    {
        foreach (var actor in squad.Living)
        {
            CatalogCheck(actor.GetPowerAmount<StrengthPower>() == 999999999 && actor.GetPowerAmount<BufferPower>() == 999999999
                && actor.GetPowerAmount<RegenPower>() == 999999999, squad.RoleOf(actor) + " godmode native powers");
            var icons = actor.GetCreatureNode()!.GetChildrenRecursive<NPower>().Where(n => n.IsVisibleInTree()).Select(n => n.Model).ToArray();
            CatalogCheck(icons.Contains(actor.GetPower<StrengthPower>()!) && icons.Contains(actor.GetPower<BufferPower>()!)
                && icons.Contains(actor.GetPower<RegenPower>()!), squad.RoleOf(actor) + " native power icons visible");
        }
    }

    private static object PowerOwnershipSnapshot(Player owner)
    {
        var squad = SquadCombatState.Get(owner);
        return new
        {
            front = squad.FrontRole,
            creatures = squad.Actors.Prepend(owner.Creature).Select(c => new
            {
                role = c == owner.Creature ? "shared-player" : squad.RoleOf(c),
                c.IsPlayer, c.IsPet, c.CurrentHp, c.Block,
                powers = c.Powers.Select(p => new { id = p.Id.ToString(), p.Amount, p.IsVisible }).ToArray(),
                icons = c.GetCreatureNode()!.GetChildrenRecursive<NPower>().Select(n => new
                {
                    id = n.Model.Id.ToString(), visible = n.IsVisibleInTree(),
                    position = n.GlobalPosition.ToString()
                }).ToArray()
            }).ToArray()
        };
    }

    private async Task VerifyPowerBaseline(Player owner)
    {
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var context = new ThrowingPlayerChoiceContext();
        var command = new GodModeConsoleCmd();
        var samples = new List<object>();
        samples.Add(new { stage = "before", state = PowerOwnershipSnapshot(owner) });
        var enabled = command.Process(owner, []);
        if (!enabled.success || enabled.task == null) throw new Exception("Native godmode did not activate.");
        await enabled.task;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        samples.Add(new { stage = "enabled", state = PowerOwnershipSnapshot(owner) });
        await Screenshot("power-baseline-enabled.png");
        var disabled = command.Process(owner, []);
        await disabled.task!;
        samples.Add(new { stage = "disabled", state = PowerOwnershipSnapshot(owner) });
        foreach (var member in squad.Living)
            foreach (var power in member.Powers.Where(p => p is StrengthPower or BufferPower or RegenPower).ToArray())
                await PowerCmd.Remove(power);
        var first = await PowerCmd.Apply<BufferPower>(context, owner.Creature, 1, owner.Creature, null);
        var second = await PowerCmd.Apply<BufferPower>(context, owner.Creature, 1, owner.Creature, null);
        samples.Add(new { stage = "stack-return", same = ReferenceEquals(first, second),
            firstAmount = first?.Amount, secondAmount = second?.Amount,
            secondOwned = second?.Owner == squad.Front, state = PowerOwnershipSnapshot(owner) });
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "power-baseline.json"),
            JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("DOHNA_POWER_BASELINE_RECORDED");
        await FinishCombat(owner);
    }
}
