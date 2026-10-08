using Godot;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DohnaDohna.Code.UI;

/// <summary>Native character-button artwork and NButton input, with multi-select instead of character registration.</summary>
public sealed partial class SquadRoleButton : NButton
{
    private TextureRect _icon = null!;
    private Control _outline = null!;
    private ShaderMaterial _hsv = null!;
    private Tween? _hover;
    private bool _selected;
    private bool _dragGesture;
    private Control _art = null!;
    public bool IsSelected => _selected;
    public RoleDefinition? Role { get; private set; }
    public int Slot { get; set; } = -1;
    public Action<SquadRoleButton>? Activated { get; set; }
    public Action<RoleDefinition>? Previewed { get; set; }
    public Action<string, int>? Reordered { get; set; }

    public static SquadRoleButton Create(RoleDefinition? role)
    {
        var source = GD.Load<PackedScene>("res://scenes/screens/char_select/char_select_button.tscn").Instantiate<Control>();
        var button = new SquadRoleButton { Name = role?.Id ?? "EmptySlot", CustomMinimumSize = source.CustomMinimumSize,
            Size = source.CustomMinimumSize, PivotOffset = source.PivotOffset, FocusMode = FocusModeEnum.All, Role = role };
        var owned = source.GetChildrenRecursive<Node>().Where(n => n.Owner == source).ToArray();
        foreach (var node in owned) node.Owner = null;
        foreach (var child in source.GetChildren()) { source.RemoveChild(child); button.AddChild(child); }
        foreach (var node in owned) node.Owner = button;
        foreach (var child in button.GetChildrenRecursive<Control>()) child.MouseFilter = MouseFilterEnum.Ignore;
        source.Free();
        return button;
    }

    public override void _Ready()
    {
        _ignoreDragThreshold = 8;
        _art = GetNode<Control>("MarginContainer");
        _art.PivotOffset = Size * .5f;
        _icon = GetNode<TextureRect>("%Icon");
        _outline = GetNode<Control>("%OutlineLocal");
        _hsv = (ShaderMaterial)_icon.Material.Duplicate();
        _icon.Material = _hsv;
        var background = new ColorRect { Name = "RoleColor", Color = Role == null ? new Color("354258") : new Color(Role.Color),
            MouseFilter = MouseFilterEnum.Ignore };
        _icon.GetParent().AddChild(background);
        _icon.GetParent().MoveChild(background, 0);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetRole(Role);
        ConnectSignals();
    }

    public void SetRole(RoleDefinition? role)
    {
        Role = role;
        if (!IsNodeReady() && _icon == null) return;
        var texture = role == null ? null : GD.Load<Texture2D>(role.AssetRoot + "/portrait.png");
        _icon.Texture = texture == null ? null : new AtlasTexture { Atlas = texture, Region = texture.GetImage().GetUsedRect() };
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        GetNode<ColorRect>("MarginContainer/Mask/RoleColor").Color = role == null ? new Color("354258") : new Color(role.Color);
        Refresh();
    }

    public void SetSelected(bool selected) { _selected = selected; if (_icon != null) Refresh(); }
    private void Refresh()
    {
        _outline.Visible = _selected;
        _hsv.SetShaderParameter("s", _selected || IsFocused ? 1f : .65f);
        _hsv.SetShaderParameter("v", _selected || IsFocused ? 1.1f : .8f);
    }
    protected override void OnFocus()
    {
        base.OnFocus();
        _hover?.Kill();
        _art.Scale = Vector2.One * 1.1f;
        Refresh();
        if (Role != null) Previewed?.Invoke(Role);
    }
    protected override void OnUnfocus()
    {
        _hover?.Kill();
        _hover = CreateTween();
        _hover.TweenProperty(_art, "scale", Vector2.One, .5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        if (_icon != null) Refresh();
    }
    protected override void OnPress() { _dragGesture = false; base.OnPress(); }
    protected override void OnRelease()
    {
        if (!_dragGesture && !GetViewport().GuiIsDragging()) Activated?.Invoke(this);
    }
    public override void _Process(double delta)
    {
        if (_selected) _outline.Modulate = new Color(1, 1, 1,
            Mathf.Lerp(.35f, 1f, (Mathf.Cos(Time.GetTicksMsec() * .001f * 1.6f * Mathf.Pi) + 1) * .5f));
    }
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!IsEnabled || Slot < 0 || Role == null) return default;
        _dragGesture = true;
        var preview = new TextureRect { Texture = _icon.Texture, CustomMinimumSize = new Vector2(70, 104),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        SetDragPreview(preview);
        return Role.Id;
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => IsEnabled && Slot >= 0 && data.VariantType == Variant.Type.String
        && RoleDefinition.All.Any(r => r.Id == data.AsString());
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _dragGesture = true;
        Reordered?.Invoke(data.AsString(), Slot);
    }
    public override void _ExitTree() { _hover?.Kill(); base._ExitTree(); }
}
