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
        Creature[] targets = command.IsSingleTargeted && selection.Play?.Target is { } target ? [target]
            : selection.Actor.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        // Replace generic physical feedback across the catalog. Bespoke hit
        // nodes/elemental effects stay native: WithHitVfxNode is additive.
        if (selection.Substituting || card is SquadAttackCard || command.HitVfx is
                MegaCrit.Sts2.Core.Commands.VfxCmd.slashPath or MegaCrit.Sts2.Core.Commands.VfxCmd.bluntPath
                or MegaCrit.Sts2.Core.Commands.VfxCmd.heavyBluntPath or MegaCrit.Sts2.Core.Commands.VfxCmd.giantHorizontalSlashPath)
            SquadActions.ConfigureNativeImpact(command, squad.RoleOf(selection.Actor), SquadMotionTiming.Cue(card) == "strike");
        var previous = selection.PresentedCommand;
        var previousTargets = selection.AnimationTargets;
        selection.PresentedCommand = command;
        selection.AnimationTargets = targets;
        RoleFinisher? finish = null;
        try
        {
            // Native Execute owns TriggerAnim, OnlyPlayAnimOnce, all hit waits
            // and the actual VFX/damage. The role supplies only its visual gate.
            if (SquadFinisher.CanFinish(command, card, selection.Play, targets))
                finish = RoleFinisher.Begin(selection.Actor, SquadMotionTiming.Cue(card), targets);
            return await command.Execute(context);
        }
        finally
        {
            (selection.Actor.GetCreatureNode()?.Visuals as RoleVisuals)?.CancelPendingEmissions(selection);
            finish?.CommandComplete();
            selection.PresentedCommand = previous;
            selection.AnimationTargets = previousTargets;
            squad.SyncController();
        }
    }
}
