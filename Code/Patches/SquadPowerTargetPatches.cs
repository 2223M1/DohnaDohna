using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// Resolve only these verified native personal effects. Unknown powers stay on
// their explicit owner; IsPlayer, Player.Creature and Creature's query APIs are
// never impersonated. A caller needing later access keeps the returned model or
// its Owner, rather than querying an unrelated/shared creature after a swap.
public sealed class SquadPowerTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_power_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(PowerCmd), nameof(PowerCmd.Apply),
        [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])];
    public static void Prefix(PowerModel power, ref Creature target) => target = Recipient(power, target);

    internal static Creature Recipient(PowerModel power, Creature target)
    {
#if !DOHNADOHNA_STABLE
        // Its healing target is personal, but the extra turn has a Player owner.
        if (power is AmbergrisPower && SquadCombatState.IsMember(target)) return target.PetOwner!.Creature;
#endif
        if (SquadCombatState.TryGet(target.Player) is not { } squad || !squad.Living.Any()) return target;
        if (power is FocusPower && squad.IsAlive("antena")) return squad.GetActor("antena");
        return power is StrengthPower or DexterityPower or VigorPower or ArtifactPower or BufferPower
            or RegenPower or ThornsPower or WeakPower or VulnerablePower or FrailPower or PoisonPower or ShrinkPower
            or TemporaryStrengthPower or TemporaryDexterityPower ? squad.Front : target;
    }
}

public sealed class SquadPowerStackTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_power_stack_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(PowerCmd), nameof(PowerCmd.FindExistingInstanceForStacking),
        [typeof(PowerModel), typeof(Creature), typeof(Creature)])];
    // Both Apply overloads must find the SAME native instance. Redirecting only
    // non-generic Apply made Apply<T> return a newly-created, never-applied model
    // when the resolved member already had a stack.
    public static void Prefix(PowerModel basePower, ref Creature target) => target = SquadPowerTargetPatch.Recipient(basePower, target);
}
