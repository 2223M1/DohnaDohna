using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

internal sealed class SquadPreparationStartPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_card_prepare_start";
    public static ModPatchTarget[] GetTargets() =>
    [new(typeof(NMouseCardPlay), nameof(NMouseCardPlay.Start), Type.EmptyTypes),
     new(typeof(NControllerCardPlay), nameof(NControllerCardPlay.Start), Type.EmptyTypes)];

    public static void Prefix(NCardPlay __instance)
    {
        // Prefix matters: controller Start can cancel synchronously.
        var card = __instance.Holder?.CardModel;
        if (card is DohnaDohna.Cards.SquadSwap
            || card?.Type is not (CardType.Attack or CardType.Skill or CardType.Power)
            || SquadCombatState.TryGet(card.Owner) is not { } squad || !squad.Living.Any()) return;
        __instance.AddChild(new SquadPreparationInput { Name = nameof(SquadPreparationInput), Card = card });
    }
}

internal sealed class SquadPreparationFinishPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_card_prepare_finish";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCardPlay), "Cleanup", [typeof(bool)])];
    public static void Prefix(NCardPlay __instance, bool isFinished) =>
        __instance.GetNodeOrNull<SquadPreparationInput>(nameof(SquadPreparationInput))?.Finish(isFinished);
}

internal sealed class SquadPreparationQueueCancelPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_card_prepare_dequeue";
    public static ModPatchTarget[] GetTargets() =>
    [new(typeof(NCardPlayQueue), nameof(NCardPlayQueue.RemoveCardFromQueueForCancellation), [typeof(PlayCardAction)]),
     new(typeof(NCardPlayQueue), nameof(NCardPlayQueue.RemoveCardFromQueueForCancellation), [typeof(NCard), typeof(bool)])];
    public static void Prefix(object __0)
    {
        var card = __0 is PlayCardAction action ? action.NetCombatCard.ToCardModelOrNull() : ((NCard)__0).Model;
        if (card != null) SquadPreparationInput.Cancel(card);
    }
}

// No pointer capture, targeting override, synthetic play, or second input loop.
// Mouse, keyboard shortcuts and controller all retain native NCardPlay behavior.
internal sealed partial class SquadPreparationInput : Node
{
    internal CardModel Card { get; init; } = null!;
    private RoleVisuals? _visuals;
    private bool _finished;
    public override void _Ready() => FollowActor();
    public override void _Process(double delta) { if (!_finished) FollowActor(); }

    private void FollowActor()
    {
        var squad = SquadCombatState.TryGet(Card.Owner);
        var next = squad?.Living.Any() == true
            ? squad.Actions.ActorFor(Card).GetCreatureNode()?.Visuals as RoleVisuals : null;
        if (_visuals == next) return;
        if (GodotObject.IsInstanceValid(_visuals)) _visuals!.EndPreparation(this, false);
        _visuals = next;
        _visuals?.Prepare(this, Card);
    }

    internal void Finish(bool submitted)
    {
        if (_finished) return;
        _finished = true;
        if (GodotObject.IsInstanceValid(_visuals)) _visuals!.EndPreparation(this, submitted);
    }

    internal static void Cancel(CardModel card)
    {
        if (SquadCombatState.TryGet(card.Owner) is not { } squad) return;
        foreach (var actor in squad.Actors)
            (actor.GetCreatureNode()?.Visuals as RoleVisuals)?.CancelPreparation(card);
    }

    public override void _ExitTree() => Finish(false);
}
