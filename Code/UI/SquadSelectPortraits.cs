using DohnaDohna.Content;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace DohnaDohna.Code.UI;

/// <summary>Selection-owned battle-outfit gallery; no input or lobby state.</summary>
public sealed partial class SquadSelectPortraits : Control
{
    private readonly List<Control> _lanes = [];
    private readonly List<TextureRect> _portraits = [];
    private readonly List<Label> _names = [];
    private readonly List<string?> _roles = [null, null, null, null];
    private readonly float[] _entry = new float[4];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var fade = new ShaderMaterial { Shader = GD.Load<Shader>("res://DohnaDohna/shaders/selection_bust.gdshader") };
        for (int i = 0; i < 4; i++)
        {
            var lane = new Control { Name = "Lane" + i, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(lane);
            _lanes.Add(lane);
            var portrait = new TextureRect { Name = "Portrait", MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = TextureFilterEnum.Linear, Material = fade };
            lane.AddChild(portrait);
            _portraits.Add(portrait);
            var label = new Label { Name = "MemberName", MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", 26);
            label.AddThemeConstantOverride("outline_size", 5);
            label.AddThemeColorOverride("font_outline_color", new Color("101523"));
            lane.AddChild(label);
            _names.Add(label);
        }
        Resized += Layout;
        Layout();
    }

    internal void SetRoster(IReadOnlyList<string> roles)
    {
        for (int i = 0; i < 4; i++)
        {
            string? id = i < roles.Count ? roles[i] : null;
            if (_roles[i] == id) continue;
            _roles[i] = id;
            _entry[i] = 0;
            var role = id == null ? null : RoleDefinition.Get(id);
            _portraits[i].Texture = role == null ? null : GD.Load<Texture2D>(role.AssetRoot + "/selection_bust.png");
            _names[i].Text = role?.GetDisplayName(LocManager.Instance.Language) ?? "";
        }
        Layout();
        QueueRedraw();
    }

    private void Layout()
    {
        float width = (Size.X - 3 * 12) / 4;
        for (int i = 0; i < _lanes.Count; i++)
        {
            _lanes[i].Position = new Vector2(i * (width + 12), 0);
            _lanes[i].Size = new Vector2(width, Size.Y);
            _names[i].Position = new Vector2(0, Size.Y - 43);
            _names[i].Size = new Vector2(width, 36);
            PlacePortrait(i);
        }
        QueueRedraw();
    }

    private void PlacePortrait(int i)
    {
        var portrait = _portraits[i];
        if (portrait.Texture == null) return;
        // Height is shared, not width: a wide weapon may leave the panel while
        // the face stays readable. No stretching or low-res combat-frame zoom.
        float height = Size.Y - 48;
        var size = portrait.Texture.GetSize();
        portrait.Size = size * (height / size.Y);
        float focus = _roles[i] == "kirakira" ? .60f : .50f;
        float settle = MathF.Pow(1 - _entry[i], 3);
        portrait.Position = new Vector2(_lanes[i].Size.X * .5f - portrait.Size.X * focus + settle * 24,
            8 + (i % 2) * 12);
        portrait.Modulate = new Color(1, 1, 1, 1 - settle);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        for (int i = 0; i < _lanes.Count; i++)
        {
            if (_entry[i] >= 1) continue;
            _entry[i] = Math.Min(1, _entry[i] + (float)delta / .24f);
            PlacePortrait(i);
        }
    }

    public override void _Draw()
    {
        foreach (var (lane, i) in _lanes.Select((lane, i) => (lane, i)))
        {
            var color = _roles[i] == null ? new Color("73849D") : new Color(RoleDefinition.Get(_roles[i]!).Color);
            var p = lane.Position;
            float w = lane.Size.X, h = lane.Size.Y;
            DrawColoredPolygon([p + new Vector2(22, 16), p + new Vector2(w, 0),
                p + new Vector2(w - 22, h), p + new Vector2(0, h)], new Color(color, _roles[i] == null ? .05f : .15f));
            DrawLine(p + new Vector2(8, h - 1), p + new Vector2(w - 8, h - 1), new Color(color, .8f), 3, true);
            if (_roles[i] == null)
                DrawString(ThemeDB.FallbackFont, p + new Vector2(w * .5f - 25, h * .5f),
                    $"0{i + 1}", HorizontalAlignment.Left, -1, 48, new Color(color, .25f));
        }
    }

    public override void _ExitTree() => Resized -= Layout;
}
