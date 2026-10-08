using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DohnaDohna.Code.Visuals;

/// <summary>A single cinematic's render-only camera. Native HUD layout and targeting stay in world space.</summary>
internal sealed partial class MotionCamera : CanvasLayer
{
    private readonly List<CanvasItem> _roots = [];
    private bool _released;

    internal static MotionCamera Create(NCombatRoom room)
    {
        var camera = new MotionCamera { Name = "DohnaMotionCamera", Layer = -1 };
        room.AddChild(camera);
        camera._roots.Add(room.SceneContainer);
        if (!room.SceneContainer.IsAncestorOf(room.CombatVfxContainer)) camera._roots.Add(room.CombatVfxContainer);
        RenderingServer.FramePreDraw += camera.Sync;
        camera.Sync();
        return camera;
    }

    internal void Apply(float zoom, Vector2 desiredOrigin, Rect2 subjects)
    {
        zoom = Math.Max(1, zoom);
        var viewport = GetViewport().GetVisibleRect();
        var focus = (viewport.GetCenter() - desiredOrigin) / zoom;
        const float margin = 32;
        float fit = Math.Min((viewport.Size.X - 2 * margin) / Math.Max(1, subjects.Size.X),
            (viewport.Size.Y - 2 * margin) / Math.Max(1, subjects.Size.Y));
        zoom = Math.Max(1, Math.Min(zoom, fit));
        desiredOrigin = viewport.GetCenter() - focus * zoom;
        var origin = new Vector2(
            FrameAxis(desiredOrigin.X, viewport.Position.X, viewport.End.X, subjects.Position.X, subjects.End.X),
            FrameAxis(desiredOrigin.Y, viewport.Position.Y, viewport.End.Y, subjects.Position.Y, subjects.End.Y));
        Transform = new Transform2D(0, Vector2.One * zoom, 0, origin);

        float FrameAxis(float desired, float low, float high, float subjectLow, float subjectHigh)
        {
            float min = Math.Max(high * (1 - zoom), low + margin - subjectLow * zoom);
            float max = Math.Min(low * (1 - zoom), high - margin - subjectHigh * zoom);
            // Oversized/distant subjects cannot justify exposing outside the scene.
            return min <= max ? Math.Clamp(desired, min, max)
                : MotionCameraBounds.ClampOrigin(desired, low, high, zoom);
        }
    }

    private void Sync()
    {
        if (_released) return;
        foreach (var root in _roots)
        {
            if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) continue;
            // Adapted from NinjaSlayer's render-only camera: do not reparent Godot
            // nodes or write Control positions that native HP layout reads back.
            RenderingServer.CanvasItemSetParent(root.GetCanvasItem(), GetCanvas());
            RenderingServer.CanvasItemSetTransform(root.GetCanvasItem(), root.GetGlobalTransform());
        }
    }

    internal void Release()
    {
        if (_released) return;
        _released = true;
        RenderingServer.FramePreDraw -= Sync;
        foreach (var root in _roots)
        {
            if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) continue;
            var parent = root.GetParent() as CanvasItem;
            RenderingServer.CanvasItemSetParent(root.GetCanvasItem(),
                !root.TopLevel && parent != null ? parent.GetCanvasItem() : root.GetCanvas());
            RenderingServer.CanvasItemSetTransform(root.GetCanvasItem(),
                root.TopLevel ? root.GetGlobalTransform() : root.GetTransform());
        }
        _roots.Clear();
        QueueFree();
    }

    public override void _ExitTree() => Release();
}
