using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.UI;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadCardChoicePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_auxiliary_input";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCardPlay), "TryPlayCard", [typeof(Creature)])];
    public static bool Prefix(NCardPlay __instance, Creature? target)
    {
        var card = __instance.Holder.CardModel;
        if (card is not SquadAttackCard || target == null || SquadCombatState.TryGet(card.Owner) is not { } squad) return true;
        var existing = __instance.GetNodeOrNull<SquadAuxiliaryTarget>(nameof(SquadAuxiliaryTarget));
        if (existing != null) return existing.Submitting;
        if (!squad.Living.Any() || squad.Actions.AuxiliaryCandidates(card, squad.Actions.ActorFor(card)).Length == 0) return true;
        var selection = new SquadAuxiliaryTarget { Name = nameof(SquadAuxiliaryTarget) };
        __instance.AddChild(selection);
        TaskHelper.RunSafely(selection.Select(__instance, target, squad));
        return false;
    }
}

public sealed class SquadPlaySeriesPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_play_series";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardModel), nameof(CardModel.OnPlayWrapper),
        [typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(ResourceInfo), typeof(bool)])];
    public static void Prefix(CardModel __instance, bool isAutoPlay, out SquadPlaySelection? __state)
    {
        __state = SquadPlaySelection.Current;
        if (SquadCombatState.TryGet(__instance.Owner) is not { } squad || !squad.Living.Any()) return;
        var prepared = isAutoPlay ? null : SquadPlaySelection.Take(__instance);
        if (prepared != null) { SquadPlaySelection.Current = prepared; return; }
        var actor = squad.Actions.ActorFor(__instance);
        var candidates = actor.IsAlive ? squad.Actions.AuxiliaryCandidates(__instance, actor) : [];
        if (!isAutoPlay && candidates.Length > 0)
            throw new InvalidOperationException("Manual dual-target card reached execution without its input selection.");
        var auxiliary = candidates.Length == 0 ? null : __instance.Owner.RunState.Rng.CombatTargets.NextItem(candidates);
        SquadPlaySelection.Current = new(__instance, actor, auxiliary);
    }
    // The host async method captured this scope for its whole series. Restore the caller immediately,
    // so nested auto-plays own their own context and cannot overwrite a suspended outer card.
    public static void Postfix(SquadPlaySelection? __state) => SquadPlaySelection.Current = __state;
}

public sealed class SquadQueuedPlayCancelPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_queued_choice_cancel";
    public static ModPatchTarget[] GetTargets() => [new(typeof(PlayCardAction), "CancelAction", Type.EmptyTypes)];
    public static void Postfix(PlayCardAction __instance)
    {
        if (__instance.NetCombatCard.ToCardModelOrNull() is { } card) SquadPlaySelection.Take(card);
    }
}
