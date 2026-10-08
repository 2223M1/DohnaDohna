using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DohnaDohna.Code.Visuals;

/// <summary>One room-owned writer for squad root positions. Body animation never writes these transforms.</summary>
public sealed partial class SquadFormation : Node
{
    private readonly Dictionary<NCreature, Vector2> _destinations = [];
    private readonly Dictionary<NCreature, Vector2> _starts = [];
    private float _elapsed;
    private bool _moving;
    private NCombatRoom _room = null!;

    public static SquadFormation Ensure(NCombatRoom room)
    {
        if (room.GetNodeOrNull<SquadFormation>(nameof(SquadFormation)) is { } found) return found;
        var node = new SquadFormation { Name = nameof(SquadFormation), _room = room };
        room.AddChild(node);
        return node;
    }

    public void Reflow(SquadCombatState squad, float scaling, bool fullyCenterPlayers, bool animate = false)
    {
        var controller = _room.GetCreatureNode(squad.Owner.Creature);
        if (controller == null) return;
        controller.Hide();
        _room.SetCreatureIsInteractable(squad.Owner.Creature, false);
        var nodes = squad.Living.Select(_room.GetCreatureNode).OfType<NCreature>().ToArray();
        foreach (var actor in squad.Actors.Where(c => c.IsDead))
            _room.SetCreatureIsInteractable(actor, false);
        if (nodes.Length == 0) return;

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
                if (_starts.TryGetValue(node, out var start)) node.Position = start.Lerp(destination, _elapsed / .2f);
                else node.Position = destination;
            }
            node.Modulate = Colors.White;
            node.Visuals.Modulate = Colors.White;
            if (node.Visuals is RoleVisuals role) role.FormationOrigin = nodes[^1];
            node.Show();
            _room.SetCreatureIsInteractable(node.Entity, true);
        }
        if (animate) { _elapsed = 0; _moving = true; }
        foreach (var removed in _destinations.Keys.Where(n => !nodes.Contains(n)).ToArray())
        { _destinations.Remove(removed); _starts.Remove(removed); }
    }

    public async Task FinishMovement(CancellationToken token)
    {
        while (_moving) await this.AwaitProcessFrame(token);
    }

    public override void _Process(double delta)
    {
        if (!_moving || CombatManager.Instance.IsPaused) return;
        _elapsed = Math.Min(.2f, _elapsed + (float)delta);
        foreach (var (node, start) in _starts)
            if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
                node.Position = start.Lerp(_destinations[node], _elapsed / .2f);
        if (_elapsed >= .2f) { _moving = false; _starts.Clear(); }
    }

    public override void _ExitTree() { _moving = false; _starts.Clear(); _destinations.Clear(); }
}
