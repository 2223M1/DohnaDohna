using System.Runtime.CompilerServices;
using System.Text.Json;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using DohnaDohna.Code.UI;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadWorldSuccessionPatch : IPatchMethod
{
    private sealed record Pending(string RoleId);
    private static readonly ConditionalWeakTable<Player, Pending> Deaths = new();
    public static string PatchId => "dohnadohna_world_death_succession";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.ShouldDie),
        [typeof(IRunState), typeof(ICombatState), typeof(Creature), typeof(AbstractModel).MakeByRefType()])];
    public static void Postfix(ICombatState? combatState, Creature creature, ref bool __result, ref AbstractModel? preventer)
    {
        // Native Fairy/Lizard eligibility has already run. A living replacement
        // prevents the controller's death, not the individual member's death.
        if (!__result || combatState != null || creature.Player is not { Character: DohnaSquad } owner) return;
        var state = SquadStore.Get(owner);
        var dead = state.Front;
        if (dead == null) return;
        dead.Hp = 0;
        Deaths.Add(owner, new Pending(dead.RoleId));
        if (state.Front is { } next)
        {
            creature.SetMaxHpInternal(next.MaxHp);
            creature.SetCurrentHpInternal(next.Hp);
            __result = false;
            // Canonical feature identity; no replacement heal/buff algorithm.
            preventer = ModelDb.Power<DohnaDohna.Powers.SquadRules>();
        }
        SquadStore.State.Set((RunState)owner.RunState, owner.NetId, state);
    }
    public static async Task Present(Player owner)
    {
        if (!Deaths.TryGetValue(owner, out var pending)) return;
        Deaths.Remove(owner);
        using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(RoleDefinition.Get(pending.RoleId).AssetRoot + "/motions.json"));
        // The native room lifecycle owns the voice tail; disposing here would
        // silence it immediately whenever the optional poster is disabled.
        _ = SquadAudio.PlayInRoom(data.RootElement.GetProperty("deathVoice")[0].GetString()!);
        var poster = PresentationSettings.ShowFemaleDeathCutIn ? data.RootElement.GetProperty("poster").GetString() : null;
        if (poster != null) await DeathCutIn.Show(((Godot.SceneTree)Godot.Engine.GetMainLoop()).Root, poster, CancellationToken.None);
    }
}

public sealed class SquadWorldDeathPresentationPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_world_death_presentation";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.AfterDeath),
        [typeof(IRunState), typeof(ICombatState), typeof(Creature), typeof(bool), typeof(float)])];
    public static void Postfix(ICombatState? combatState, Creature creature, ref Task __result)
    {
        if (combatState == null && creature.Player is { Character: DohnaSquad } owner) __result = Finish(__result, owner);
    }
    private static async Task Finish(Task original, Player owner) { await original; await SquadWorldSuccessionPatch.Present(owner); }
}

public sealed class SquadWorldHpSyncPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_world_hp_sync";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.AfterCurrentHpChanged),
        [typeof(IRunState), typeof(ICombatState), typeof(Creature), typeof(decimal)])];
    public static void Postfix(ICombatState? combatState, Creature creature, ref Task __result)
    {
        if (combatState == null && creature.IsAlive && creature.Player is { Character: DohnaSquad } owner && creature == owner.Creature)
            __result = Finish(__result, owner);
    }
    private static async Task Finish(Task original, Player owner)
    {
        await original;
        var state = SquadStore.Get(owner);
        if (state.Front is not { } front) return;
        front.Hp = owner.Creature.CurrentHp;
        front.MaxHp = owner.Creature.MaxHp;
        SquadStore.State.Set((RunState)owner.RunState, owner.NetId, state);
    }
}
