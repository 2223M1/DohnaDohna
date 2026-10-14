using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace DohnaDohna.Code.UI;

/// <summary>SceneTitle.x, AIN 31820/31826/31827 and CharacterPositionCalculator 31793–31795.</summary>
public partial class SelectBackground : Control
{
    private sealed class Figure(Node2D node, Vector2 origin, int index)
    {
        public Node2D Node = node;
        public Vector2 Origin = origin;
        public Vector2 Parallax;
        public float Speed;
        public int Index = index;
    }
    private readonly List<Figure> _figures = [];
    private readonly List<(Sprite2D Node, float Period)> _circles = [];
    private Node2D _plane = null!;
    private Node2D _figurePlane = null!;
    private NCharacterSelectScreen _screen = null!;
    private Transform2D _figureScreenTransform;
    private bool _secondary;
    private Vector2 _pointer = new(640, 360);
    private double _elapsed, _stepAccumulator;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ClipContents = true;
        _screen = GetParent().GetParent<NCharacterSelectScreen>();
        _plane = new Node2D();
        AddChild(_plane);
        using var title = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://DohnaDohna/images/original/title.json"));
        _plane.AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(title.RootElement.GetProperty("系统／标题／背景").GetString()!), Centered = false });
        using var layout = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://DohnaDohna/images/original/title-layout.json"));
        foreach (var circle in layout.RootElement.GetProperty("circles").EnumerateArray())
        {
            var sprite = new Sprite2D { Texture = GD.Load<Texture2D>(circle.GetProperty("image").GetString()!),
                Position = Vector(circle.GetProperty("position")), Scale = Vector(circle.GetProperty("scale")),
                Modulate = new Color(1, 1, 1, circle.GetProperty("alpha").GetSingle()),
                Material = new CanvasItemMaterial { BlendMode = circle.GetProperty("filter").GetInt32() == 3
                    ? CanvasItemMaterial.BlendModeEnum.Sub : CanvasItemMaterial.BlendModeEnum.Add } };
            _plane.AddChild(sprite);
            _circles.Add((sprite, circle.GetProperty("period").GetSingle()));
        }
        foreach (var part in layout.RootElement.GetProperty("figures").EnumerateArray())
        {
            // Native AnimatedBg is oversized, offset and scaled for background
            // cropping. Figures have their own original-game parallax and must
            // fit the actual screen, not that overscan rectangle.
            if (_figurePlane == null) { _figurePlane = new Node2D { Name = "Figures" }; AddChild(_figurePlane); }
            var anchor = new Node2D { Position = new Vector2(640, 360), Scale = Vector2.Zero };
            anchor.AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(part.GetProperty("image").GetString()!),
                Centered = false, Position = Vector(part.GetProperty("offset")) });
            _figurePlane.AddChild(anchor);
            _figures.Add(new Figure(anchor, Vector(part.GetProperty("position")), _figures.Count));
        }
        Resized += Fit;
        Fit();
    }

    private static Vector2 Vector(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle());
    public void SetSecondary(bool secondary) { _secondary = secondary; if (IsNodeReady()) Fit(); }
    private void Fit()
    {
        // SceneTitle deliberately crops the full-size artwork at its 1280x720
        // viewport. Fitting all opaque pixels shrinks the cast to a thumbnail.
        // Background, circles and figures share the original canvas transform.
        var screenSize = _screen.Size;
        float scale = Math.Max(screenSize.X / 1280f, screenSize.Y / 720f);
        _figureScreenTransform = new Transform2D(0, Vector2.One * scale, 0,
            (screenSize - new Vector2(1280, 720) * scale) * .5f);
        _plane.GlobalTransform = _screen.GetGlobalTransform() * _figureScreenTransform;
        _figurePlane.GlobalTransform = _screen.GetGlobalTransform() * _figureScreenTransform;
        _figurePlane.Visible = !_secondary;
        _plane.Modulate = _secondary ? new Color(.09f, .11f, .20f) : Colors.White;
    }

    public override void _Process(double delta)
    {
        if (_plane == null) return;
        // Cancel only the native background's overscan transform, preserving
        // canvas draw order/modulation so native popups still cover the poster.
        _figurePlane.GlobalTransform = _screen.GetGlobalTransform() * _figureScreenTransform;
        _plane.GlobalTransform = _figurePlane.GlobalTransform;
        _elapsed += delta;
        var pointer = _figurePlane.GetLocalMousePosition();
        if (new Rect2(Vector2.Zero, new Vector2(1280, 720)).HasPoint(pointer)) _pointer = pointer;
        _stepAccumulator += delta;
        while (_stepAccumulator >= 1.0 / 60)
        {
            _stepAccumulator -= 1.0 / 60;
            foreach (var figure in _figures)
            {
                var target = (new Vector2(640, 360) - _pointer) * .009f * (2.5f - figure.Index);
                float distance = figure.Parallax.DistanceTo(target);
                int index = Math.Min(19, (int)(figure.Speed / .1f));
                float stoppingDistance = .1f * (index + 1) * (index + 2) * .5f;
                figure.Speed = Math.Clamp(figure.Speed + (stoppingDistance > distance ? -.1f : .1f), 0, 2);
                if (distance < .1f) { figure.Speed = 0; figure.Parallax = target; }
                else figure.Parallax = figure.Parallax.MoveToward(target, figure.Speed);
            }
        }
        foreach (var figure in _figures)
        {
            int reverseIndex = 5 - figure.Index;
            double time = _elapsed - reverseIndex * .02;
            double duration = .5 - reverseIndex * .02;
            float progress = (float)Math.Clamp(time / duration, 0, 1);
            float ease = progress >= 1 ? 1 : 1 - MathF.Pow(2, -10 * progress);
            figure.Node.Position = new Vector2(640, 360).Lerp(figure.Origin, ease) + figure.Parallax;
            float scale = 1.2f * ease;
            if (time > duration) scale = Mathf.Lerp(1.2f, 1f, MathF.Pow((float)Math.Clamp((time - duration) / .2, 0, 1), 2));
            figure.Node.Scale = Vector2.One * scale;
        }
        foreach (var (node, period) in _circles) node.Rotation = (float)(_elapsed / period) * Mathf.Tau;
    }
    public override void _ExitTree() => Resized -= Fit;
}
