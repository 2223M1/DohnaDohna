using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DohnaDohna.Code.UI;

/// <summary>Room-owned display for the real player's shared powers and native orb manager; not a fifth creature.</summary>
public sealed partial class SquadSharedDisplay : Node2D
{
    private SquadCombatState _squad = null!;
    private NCombatRoom _room = null!;
    private NPowerContainer _powers = null!;
    private Label _caption = null!;
    private NOrbManager _orbs = null!;
    internal Control PowerAnchor => _powers;

    public static void Create(SquadCombatState squad, NCombatRoom room)
    {
        var controller = room.GetCreatureNode(squad.Owner.Creature)
            ?? throw new InvalidOperationException("Squad controller node is missing.");
        var display = new SquadSharedDisplay { Name = "DohnaSharedResources", _squad = squad, _room = room };
        controller.GetParent().AddChild(display);
        display._powers = new NPowerContainer { Name = "SharedPowers", Size = new Vector2(400, 64), MouseFilter = Control.MouseFilterEnum.Ignore };
        display.AddChild(display._powers);
        display._powers.SetCreature(squad.Owner.Creature);
        display._caption = new Label { Text = new LocString("characters", "DOHNA_SQUAD.shared_powers").GetFormattedText(),
            MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(.8f, .86f, .94f) };
        display._caption.AddThemeFontSizeOverride("font_size", 17);
        display.AddChild(display._caption);
        display._orbs = controller.OrbManager ?? throw new InvalidOperationException("Squad player has no native orb manager.");
        display._orbs.Reparent(display, false);
        // Native scene offsets its container 195px towards the Defect's chest.
        // Our manager is already anchored at the saucer, so keep that local origin.
        display._orbs.GetNode<Control>("%Orbs").Position = Vector2.Zero;
        display._orbs.Show();
        display._Process(0);
    }

    public override void _Process(double delta)
    {
        if (_orbs == null || !_squad.Living.Any()) return;
        var members = _squad.Living.Select(c => _room.GetCreatureNode(c)
            ?? throw new InvalidOperationException("Squad member node is missing.")).ToArray();
        var left = members.Min(c => c.Position.X);
        _powers.Position = new Vector2(left - 70, 330);
        _caption.Position = _powers.Position + new Vector2(0, -23);
        _caption.Visible = _squad.Owner.Creature.Powers.Any(p => p.IsVisible);
        var orbActor = _squad.IsAlive("antena") ? _squad.GetActor("antena") : _squad.Front;
        var node = orbActor.GetCreatureNode();
        var anchor = node?.Visuals is RoleVisuals { SaucerAnchor: { } saucer } ? saucer
            : node!.Visuals.GlobalPosition + new Vector2(0, -240);
        _orbs.GlobalPosition = anchor;
        _orbs.Scale = Vector2.One;
    }
}
