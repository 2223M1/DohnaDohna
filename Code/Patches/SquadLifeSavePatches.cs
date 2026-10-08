using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>Change only native eligibility; the host retains preventer ordering,
/// consumption, heal amount and AfterPreventingDeath on the actual member.</summary>
public sealed class SquadFairyEligibilityPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_native_fairy_eligibility";
    public static ModPatchTarget[] GetTargets() => [new(typeof(FairyInABottle), nameof(FairyInABottle.ShouldDie), [typeof(Creature)])];
    public static void Prefix(FairyInABottle __instance, ref Creature creature)
    {
        if (SquadCombatState.IsMember(creature) && creature.PetOwner == __instance.Owner)
            creature = __instance.Owner.Creature;
    }
}

public sealed class SquadLizardEligibilityPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_native_lizard_eligibility";
    public static ModPatchTarget[] GetTargets() => [new(typeof(LizardTail), nameof(LizardTail.ShouldDieLate), [typeof(Creature)])];
    public static void Prefix(LizardTail __instance, ref Creature creature)
    {
        if (SquadCombatState.IsMember(creature) && creature.PetOwner == __instance.Owner)
            creature = __instance.Owner.Creature;
    }
}
