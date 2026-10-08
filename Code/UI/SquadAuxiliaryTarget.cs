using Godot;
using HarmonyLib;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DohnaDohna.Code.UI;

/// <summary>Input-only second arrow. Nothing is enqueued or spent until both targets are confirmed.</summary>
public sealed partial class SquadAuxiliaryTarget : Node
{
    private readonly CancellationTokenSource _lifetime = new();
    public bool Submitting { get; private set; }

    public async Task Select(NCardPlay input, Creature enemy, SquadCombatState squad)
    {
        var card = input.Holder.CardModel ?? throw new InvalidOperationException("Card input lost its model.");
        var actor = squad.Actions.ActorFor(card);
        try
        {
            // The first native target manager continuation must finish resetting its state.
            await this.AwaitProcessFrame(_lifetime.Token);
            var candidates = squad.Actions.AuxiliaryCandidates(card, actor);
            if (actor.IsDead || enemy.IsDead || candidates.Length == 0) { input.CancelPlayCard(); return; }
            var manager = NTargetManager.Instance ?? throw new InvalidOperationException("Combat target manager is missing.");
#if DOHNADOHNA_STABLE
            bool controller = NControllerManager.Instance?.IsUsingController == true;
#else
            bool controller = NControllerManager.Instance?.IsUsingDirectionalNavigation == true;
#endif
            manager.StartTargeting(SquadTargeting.Member, input.Holder.CardNode!,
                controller ? TargetMode.Controller : TargetMode.ClickMouseToTarget,
                () => _lifetime.IsCancellationRequested || actor.IsDead || enemy.IsDead,
                node => node is NCreature creature && candidates.Contains(creature.Entity));
            if (controller)
            {
                var room = NCombatRoom.Instance ?? throw new InvalidOperationException("Combat room is missing.");
                var nodes = candidates.Select(room.GetCreatureNode).OfType<NCreature>().ToArray();
                room.RestrictControllerNavigation(nodes.Select(n => n.Hitbox));
                nodes[0].Hitbox.TryGrabFocus();
            }
            var selected = await manager.SelectionFinished();
            if (_lifetime.IsCancellationRequested) return;
            if (selected is not NCreature target || !target.Entity.IsAlive || !card.CanPlayTargeting(enemy))
            { input.CancelPlayCard(); return; }
            SquadPlaySelection.Queue(new(card, actor, target.Entity));
            Submitting = true;
            AccessTools.Method(typeof(NCardPlay), "TryPlayCard").Invoke(input, [enemy]);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        finally
        {
            NCombatRoom.Instance?.EnableControllerNavigation();
            if (!Submitting) SquadPlaySelection.Take(card);
        }
    }

    public override void _ExitTree() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
