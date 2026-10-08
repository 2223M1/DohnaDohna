using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>The shared max-HP gain reaches all four members; its visual growth must too.</summary>
public sealed class SquadBigMushroomPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_big_mushroom_members";
    public static ModPatchTarget[] GetTargets() => [new(typeof(BigMushroom), "Grow", Type.EmptyTypes)];

    public static void Postfix(BigMushroom __instance)
    {
        if (SquadCombatState.TryGet(__instance.Owner) is { } squad) GrowMembers(squad);
    }

    internal static void GrowMembers(SquadCombatState squad)
    {
        // Exact native BigMushroom.Grow values in both supported hosts. Use the
        // host tween, not DefaultScale: Shrink intentionally replaces this scale.
        foreach (var actor in squad.Actors) actor.GetCreatureNode()?.ScaleTo(1.5f, 0);
    }
}
