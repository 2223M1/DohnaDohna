using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.Code.Visuals;

/// <summary>One room-owned writer for squad root positions. Body animation never writes these transforms.</summary>
public sealed partial class SquadFormation : Node
{
    private readonly Dictionary<NCreature, Vector2> _destinations = [];
    private readonly Dictionary<NCreature, Vector2> _starts = [];
    private float _elapsed;
    private bool _moving;
    private Creature? _acting;
    private Creature? _movingActor;
    private float _duration = .4f;
    private NCombatRoom _room = null!;

    public static SquadFormation Ensure(NCombatRoom room)
    {
        if (room.GetNodeOrNull<SquadFormation>(nameof(SquadFormation)) is { } found) return found;
        var node = new SquadFormation { Name = nameof(SquadFormation), _room = room };
        room.AddChild(node);
        return node;
    }

    public void Reflow(SquadCombatState squad, float scaling, bool fullyCenterPlayers, bool animate = false, Creature? acting = null)
    {
        var controller = _room.GetCreatureNode(squad.Owner.Creature);
        if (controller == null) return;
        controller.Hide();
        _room.SetCreatureIsInteractable(squad.Owner.Creature, false);
        var nodes = squad.Living.Select(_room.GetCreatureNode).OfType<NCreature>().ToArray();
        foreach (var actor in squad.Actors.Where(c => c.IsDead))
            _room.SetCreatureIsInteractable(actor, false);
        if (nodes.Length == 0)
        { _moving = false; _acting = null; _starts.Clear(); _destinations.Clear(); return; }

        // Use the same camera-space half-room and front-row baseline as native layout.
        // HUD width participates so adjacent health bars never occupy the same slot.
        // Slots belong to formation, not temporary body size (Shrink/Mushroom).
        // Reflows after buffs, swaps or native layout callbacks must not spread
        // or collapse the row merely because a member's art changed scale.
        var widths = nodes.Select(n => Math.Max(n.Visuals.Bounds.Size.X,
            n.GetNode<Control>("%HealthBar").Size.X)).ToArray();
        float available = 960f / scaling;
        float gap = nodes.Length > 1 ? Math.Clamp((available - 150f - widths.Sum()) / (nodes.Length - 1), 12f, 70f) : 0;
        float total = widths.Sum() + gap * (nodes.Length - 1);
        float right = fullyCenterPlayers ? total * 0.5f : -Math.Max(60f, (available - total) * 0.5f);
        float cursor = right - total;
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            var destination = new Vector2(cursor + widths[i] * 0.5f, 200);
            cursor += widths[i] + gap;
            if (animate) _starts[node] = node.Position;
            _destinations[node] = destination;
            if (!animate && !_moving) node.Position = destination;
            else if (_moving && !animate)
            {
                // Native layout may run during this animation (summon, resize, death).
                // Restore the same interpolated position immediately, not one frame later.
                if (_starts.TryGetValue(node, out var start)) node.Position = start.Lerp(destination, ProgressFor(node));
                else node.Position = destination;
            }
            node.Modulate = Colors.White;
            node.Visuals.Modulate = Colors.White;
            if (node.Visuals is RoleVisuals role) role.FormationOrigin = nodes[^1];
            node.Show();
            _room.SetCreatureIsInteractable(node.Entity, true);
        }
        if (animate)
        {
            _elapsed = 0;
            _moving = true;
            _acting = acting;
            _movingActor = acting;
            _duration = .4f;
            foreach (var node in nodes.Where(n => acting == null && _starts[n].DistanceTo(_destinations[n]) > .01f))
                (node.Visuals as RoleVisuals)?.BeginFormationJump(_duration);
        }
        foreach (var removed in _destinations.Keys.Where(n => !nodes.Contains(n)).ToArray())
        { _destinations.Remove(removed); _starts.Remove(removed); }
    }

    public async Task FinishMovement(CancellationToken token)
    {
        while (_moving) await this.AwaitProcessFrame(token);
    }

    // BeforeCardPlayed commits order/auras immediately. Its visible movement
    // shares the FIRST native animation gate, never a second awaited delay.
    internal void BeginWindup(Creature actor, float seconds)
    {
        if (!_moving || _acting != actor) return;
        _acting = null;
        _elapsed = 0;
        _duration = Math.Max(seconds, .0001f);
        foreach (var node in _starts.Keys.Where(n => n.Entity != actor))
            (node.Visuals as RoleVisuals)?.BeginFormationJump(_duration);
    }

    private float ProgressFor(NCreature node) => (node.Visuals as RoleVisuals)?.FormationJumpProgress(_elapsed / _duration)
        ?? MotionTimeline.Ease("EaseInOutSine", _elapsed / _duration);

    public override void _Process(double delta)
    {
        if (!_moving || CombatManager.Instance.IsPaused) return;
        if (_acting is { IsDead: true } dead) BeginWindup(dead, .4f);
        if (_acting != null) return;
        var mode = SaveManager.Instance.PrefsSave.FastMode;
        _elapsed = mode == FastModeType.Instant ? _duration : Math.Min(_duration, _elapsed + (float)delta);
        foreach (var (node, start) in _starts)
            if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
            {
                node.Position = start.Lerp(_destinations[node], ProgressFor(node));
                if (node.Entity == _movingActor && node.Visuals is RoleVisuals { RoleId: "kuma" } actor)
                    actor.MovementAfterimage(delta);
            }
        if (_elapsed >= _duration) { _moving = false; _starts.Clear(); }
    }

    public override void _ExitTree() { _moving = false; _starts.Clear(); _destinations.Clear(); }
}
