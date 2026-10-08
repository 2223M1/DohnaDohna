using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.Powers;

/// <summary>Feature-owned hooks, not a removable relic or an alternative buff engine.</summary>
public sealed class SquadRules : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    private SquadCombatState Squad => SquadCombatState.Get(Owner.Player!);
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player == Owner.Player ? Squad.Actions.TurnStart(context) : Task.CompletedTask;
    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool wasRemovalPrevented, float deathAnimLength) =>
        SquadCombatState.IsMember(creature) && creature.PetOwner == Owner.Player
            ? Squad.ConfirmDeath(creature, wasRemovalPrevented) : Task.CompletedTask;
    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (SquadCombatState.IsMember(creature) && creature.PetOwner == Owner.Player && creature.IsAlive)
            Squad.SyncController();
        return Task.CompletedTask;
    }
    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        !(SquadCombatState.IsMember(creature) && creature.PetOwner == Owner.Player);
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || !Squad.Living.Any()) return;
        var selection = SquadPlaySelection.CurrentFor(play.Card)
            ?? throw new InvalidOperationException("Squad card entered play without its native play-series scope.");
        selection.Actor = Squad.Actions.ActorFor(play.Card);
        selection.Play = play;
        selection.Substituting = play.Card is SquadCardModel { IsFallback: true };
        selection.AttackPresented = false;
        Squad.RecordPlay(play, selection.Actor);
        if (!selection.Substituting && play.Card is SquadCardModel { IsMelee: true })
            await Squad.MoveToFront(selection.Actor);
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        Squad.Capture();
        Squad.Dispose();
        return Task.CompletedTask;
    }
}
