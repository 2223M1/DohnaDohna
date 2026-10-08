using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Models.Monsters;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// Monster moves can emit a telegraph/hit effect directly, outside AttackCommand.
// Keep the native effect and timing, only resolve its hidden gateway anchor.
public sealed class SquadNativeVfxTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_native_vfx_target";
    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter), [typeof(Creature), typeof(string)]),
         new(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreature), [typeof(Creature), typeof(string)]),
         new(typeof(NWormyImpactVfx), nameof(NWormyImpactVfx.Create), [typeof(Creature)])];
    public static void Prefix(ref Creature __0)
    {
        if (SquadCombatState.TryGet(__0.Player) is { } squad && squad.Living.Any()) __0 = squad.Front;
    }
}

// LeafSlimeM sets its projectile marker before the first animation await. Change
// only that marker after method entry; its status-card targets remain Players.
public sealed class SquadSlimeSpitTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_slime_spit_visual_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(LeafSlimeM), "StickyShotMove", [typeof(IReadOnlyList<Creature>)])];
    public static void Postfix(LeafSlimeM __instance, IReadOnlyList<Creature> targets)
    {
        if (!targets.Any(c => SquadCombatState.TryGet(c.Player) != null)) return;
        var marker = __instance.Creature.GetCreatureNode()?.GetSpecialNode<Godot.Node2D>("Visuals/SpitTarget");
        if (marker == null) return; // Native headless/no visual-node path.
        var nodes = targets.Select(c => SquadCombatState.TryGet(c.Player) is { } squad && squad.Living.Any() ? squad.Front : c)
            .Select(c => c.GetCreatureNode()).OfType<NCreature>().ToArray();
        if (nodes.Length > 0) marker.GlobalPosition = new Godot.Vector2(nodes.Min(n => n.GlobalPosition.X), marker.GlobalPosition.Y);
    }
}

public sealed class SquadHitCastPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_native_hit_cast";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim),
        [typeof(Creature), typeof(string), typeof(float)])];
    public static bool Prefix(ref Creature creature, string triggerName, ref Task __result)
    {
        if (triggerName is "Hit" or "Cast" or "PowerUp" && SquadCombatState.TryGet(creature.Player) is { } squad)
            creature = squad.Front;
        if (!SquadCombatState.IsMember(creature) || triggerName is not ("Hit" or "Cast" or "PowerUp")
            || creature.GetCreatureNode()?.Visuals is not RoleVisuals visuals) return true;
        // Native Thorns resolves before the outer hit. Killing the final enemy
        // ends combat immediately after that hit, but its visible reaction still
        // belongs to the creature in the room. RoleVisuals stops it on replacement,
        // death or _ExitTree; the combat/action token would cut it off at victory.
        __result = triggerName == "Hit"
            ? creature.IsAlive ? visuals.PlayHurt(CancellationToken.None) : Task.CompletedTask
            : visuals.Play("cast", null, SquadCombatState.Get(creature.PetOwner!).Cancellation.Token);
        return false;
    }
}

public sealed class SquadRoomExitPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_combat_visual_exit";
    public static ModPatchTarget[] GetTargets() => [new(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom), "_ExitTree", Type.EmptyTypes)];
    public static void Prefix(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom __instance) => SquadCombatState.DisposeForRoom(__instance);
}

public sealed class SquadDeathAnimationPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_native_death_visual_boundary";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCreature), nameof(NCreature.StartDeathAnim), [typeof(bool)])];
    public static bool Prefix(NCreature __instance, ref float __result)
    {
        if (!SquadCombatState.IsMember(__instance.Entity)) return true;
#if DOHNADOHNA_STABLE
        __instance.Hitbox.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;
        __instance.Hitbox.FocusMode = Godot.Control.FocusModeEnum.None;
#else
        __instance.DisableInteractionForDeath();
#endif
        __result = 0;
        return false;
    }
}
