using DohnaDohna.Cards;
using DohnaDohna.Content;
using DohnaDohna.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadRefinementPreviewPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_refinement_preview";
    public static ModPatchTarget[] GetTargets() => [new(typeof(TouchOfOrobas), nameof(TouchOfOrobas.SetupForPlayer), [typeof(Player)])];
    internal static IEnumerable<SquadRoleRelic> Eligible(Player player) => player.Relics.OfType<SquadRoleRelic>()
        .Where(r => r.Multiplier == 1 && !r.IsMelted);
    public static bool Prefix(TouchOfOrobas __instance, Player player, ref bool __result)
    {
        if (player.Character is not DohnaSquad) return true;
        __result = Eligible(player).Any();
        // Do not preselect the first relic. The native event can truthfully show
        // its choice prompt; the two saved IDs are assigned only after selection.
        ((StringVar)__instance.DynamicVars["StarterRelic"]).StringValue = new LocString("relics", "DOHNA_SQUAD.refinement_source").GetFormattedText();
        ((StringVar)__instance.DynamicVars["UpgradedRelic"]).StringValue = new LocString("relics", "DOHNA_SQUAD.refinement_result").GetFormattedText();
        return false;
    }
}

public sealed class SquadRefinementChoicePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_refinement_choice";
    public static ModPatchTarget[] GetTargets() => [new(typeof(TouchOfOrobas), nameof(TouchOfOrobas.AfterObtained), Type.EmptyTypes)];
    public static bool Prefix(TouchOfOrobas __instance, ref Task __result)
    {
        if (__instance.Owner.Character is not DohnaSquad) return true;
        __result = Refine(__instance);
        return false;
    }
    private static async Task Refine(TouchOfOrobas touch)
    {
        var owner = touch.Owner;
        var options = SquadRefinementPreviewPatch.Eligible(owner).Select(relic =>
        {
            var option = ((RunState)owner.RunState).CreateCard<SquadRelicUpgradeChoice>(owner);
            option.SetRelic(relic, touch.GetUpgradedStarterRelic(relic));
            return (CardModel)option;
        }).ToArray();
        if (options.Length == 0) throw new InvalidOperationException("Refinement offered without an eligible starter relic.");
        var selected = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), options, owner,
            new CardSelectorPrefs(new LocString("relics", "DOHNA_SQUAD.refinement_prompt"), 1))).Single();
        var original = ((SquadRelicUpgradeChoice)selected).Original!;
        var upgrade = touch.GetUpgradedStarterRelic(original);
        touch.StarterRelic = original.Id;
        touch.UpgradedRelic = upgrade.Id;
        await RelicCmd.Replace(original, upgrade.ToMutable());
    }
}
