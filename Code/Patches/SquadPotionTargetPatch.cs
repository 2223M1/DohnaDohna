using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>Only creature-local potions use member arrows. Hand/energy/orb potions keep their real Player.</summary>
public sealed class SquadPotionTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_potion_target";
    public static ModPatchTarget[] GetTargets() => new[]
    {
        typeof(BlockPotion), typeof(StrengthPotion), typeof(DexterityPotion), typeof(FlexPotion),
        typeof(SpeedPotion), typeof(FyshOil), typeof(BloodPotion), typeof(RegenPotion),
#if !DOHNADOHNA_STABLE
        typeof(Ambergris),
#endif
        typeof(LiquidBronze), typeof(GhostInAJar), typeof(HeartOfIron), typeof(ShipInABottle),
        typeof(Fortifier), typeof(FruitJuice), typeof(GigantificationPotion)
    }.Select(type => new ModPatchTarget(type, "get_TargetType", Type.EmptyTypes)).ToArray();

    public static void Postfix(PotionModel __instance, ref TargetType __result)
    {
        // Canonical models have no owner. Outside combat, native drink/world-health rules apply.
        if (__instance.IsMutable && SquadCombatState.TryGet(__instance.Owner)?.Living.Any() == true)
            __result = SquadTargeting.Member;
    }
}
