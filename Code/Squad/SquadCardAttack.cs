using DohnaDohna.Cards;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace DohnaDohna.Code.Squad;

internal static class SquadCardAttack
{
    internal static AttackCommand FromSquadCard(this AttackCommand command, CardModel card, CardPlay play) =>
#if DOHNADOHNA_STABLE
        command.FromCard(card);
#else
        command.FromCard(card, play);
#endif

    internal static async Task<AttackCommand> ExecuteSquad(this AttackCommand command, PlayerChoiceContext context)
    {
        var card = command.ModelSource as CardModel ?? throw new InvalidOperationException("Squad card attack has no card.");
        if (SquadCombatState.TryGet(card.Owner) is not { } squad) return await command.Execute(context);
        var selection = SquadPlaySelection.CurrentFor(card) ?? throw new InvalidOperationException("Missing squad play selection.");
        if (selection.Actor.IsDead) return command;
        command.WithNoAttackerAnim();
        // Keep the native command's hit count, target RNG and contact feedback. Only its body's presentation changes.
        if (selection.AttackPresented) return await command.Execute(context);
        selection.AttackPresented = true;
        Creature[] targets = command.IsSingleTargeted && selection.Play?.Target is { } target ? [target]
            : selection.Actor.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        // Prototypes already carry native feedback (including custom hit nodes).
        // Only substitution attacks need a default contact; never append a second VFX.
        if (selection.Substituting)
            SquadActions.ConfigureNativeImpact(command, squad.RoleOf(selection.Actor), true);
        await RoleVisuals.PlayAttack(selection.Actor, card is SquadCardModel { IsMelee: true } ? "special" : "strike",
            async () => { if (selection.Actor.IsAlive) await command.Execute(context); },
            squad.Cancellation.Token, targets, SquadFinisher.CanFinish(command, card, selection.Play, targets));
        squad.SyncController();
        return command;
    }
}
