using Godot;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;

namespace DohnaDohna.Code.UI;

/// <summary>Owns the required 1500ms death pause and the original line effect's visual tail.</summary>
public sealed partial class DeathCutIn : CanvasLayer
{
    private readonly TaskCompletionSource _required = new();
    private Sprite2D _poster = null!;
    private MotionLines _lines = null!;
    private CancellationTokenRegistration _cancellation;
    private double _elapsed;

    public static async Task Show(Node parent, string texture, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var view = new DeathCutIn { Layer = 90 };
        var size = parent.GetViewport().GetVisibleRect().Size;
        float scale = Math.Min(size.X / 1280, size.Y / 720);
        var plane = new Node2D { Scale = Vector2.One * scale,
            Position = (size - new Vector2(1280, 720) * scale) * .5f };
        var image = GD.Load<Texture2D>(texture) ?? throw new InvalidDataException(texture);
        view._poster = new Sprite2D { Texture = image, Centered = false,
            Offset = new Vector2(-image.GetWidth() * .5f, 0), Position = new Vector2(1280, 0), Modulate = new Color(1, 1, 1, 0) };
        plane.AddChild(view._poster);
        view._lines = MotionLines.Create(180, 1, 75);
        plane.AddChild(view._lines); // Original Z160000 lines above Z150000 poster.
        view.AddChild(plane);
        parent.AddChild(view);
        view._cancellation = token.Register(() => Callable.From(view.QueueFree).CallDeferred());
        await view._required.Task.WaitAsync(token);
    }

    public override void _Process(double delta)
    {
        if (CombatManager.Instance.IsPaused) return;
        _elapsed += delta;
        _lines.Advance(delta);
        // AIN33163 + AIN9224: MT (2, one-based), 100ms/1000ms/100ms.
        float x, alpha;
        if (_elapsed < .1) { float r = (float)(_elapsed / .1); x = Mathf.Lerp(1280, 650, MotionTimeline.Ease("EaseOutQuad", r)); alpha = r; }
        else if (_elapsed < 1.1) { x = Mathf.Lerp(650, 630, (float)(_elapsed - .1)); alpha = 1; }
        else { float r = (float)Math.Clamp((_elapsed - 1.1) / .1, 0, 1); x = Mathf.Lerp(630, 0, MotionTimeline.Ease("EaseInQuad", r)); alpha = 1 - r; }
        _poster.Position = new Vector2(x, 0);
        _poster.Modulate = new Color(1, 1, 1, alpha);
        if (_elapsed >= 1.5) _required.TrySetResult();
        if (_elapsed >= 1.5 && _lines.Finished) QueueFree();
    }

    public override void _ExitTree()
    {
        _cancellation.Dispose();
        _required.TrySetCanceled();
    }
}
