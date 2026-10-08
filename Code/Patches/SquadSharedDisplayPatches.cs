using DohnaDohna.Code.Squad;
using DohnaDohna.Code.UI;
using DohnaDohna.Content;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// The native power controls and tooltip renderer stay intact. The hidden player
// has no useful hitbox anchor, so only its tooltip endpoint uses the visible bar.
public sealed class SquadSharedHoverPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_shared_power_hover";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCreature), nameof(NCreature.ShowHoverTips), [typeof(IEnumerable<IHoverTip>)])];
    internal static Control? Anchor(NCreature node) => SquadCombatState.TryGet(node.Entity.Player) == null ? null
        : node.GetParent().GetNodeOrNull<SquadSharedDisplay>("DohnaSharedResources")?.PowerAnchor;
    public static bool Prefix(NCreature __instance, IEnumerable<IHoverTip> hoverTips)
    {
        if (Anchor(__instance) is not { } anchor) return true;
        if (NCombatRoom.Instance?.Ui.Hand.InCardPlay != false) return false;
        NHoverTipSet.Remove(anchor);
        NHoverTipSet.CreateAndShow(anchor, hoverTips, HoverTip.GetHoverTipAlignment(anchor, .5f));
        return false;
    }
}

public sealed class SquadSharedUnhoverPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_shared_power_unhover";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCreature), nameof(NCreature.HideHoverTips), Type.EmptyTypes)];
    public static void Postfix(NCreature __instance)
    {
        if (SquadSharedHoverPatch.Anchor(__instance) is { } anchor) NHoverTipSet.Remove(anchor);
    }
}

public sealed class SquadSharedSourcePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_shared_power_source";
    public static ModPatchTarget[] GetTargets() => [new(typeof(PowerModel), "get_HoverTips", Type.EmptyTypes)];
    public static void Postfix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!__instance.IsMutable || !__instance.IsVisible || SquadCombatState.TryGet(__instance.Owner.Player) == null
            || __instance.Applier is not { } source || !SquadCombatState.IsMember(source)) return;
        var name = RoleDefinition.Get(((DohnaDohna.Monsters.SquadMember)source.Monster!).RoleId).GetDisplayName(LocManager.Instance.Language);
        var description = new LocString("characters", "DOHNA_SQUAD.source_description");
        description.Add("Role", name);
        __result = __result.Append(new HoverTip(new LocString("characters", "DOHNA_SQUAD.source_title"), description));
    }
}
