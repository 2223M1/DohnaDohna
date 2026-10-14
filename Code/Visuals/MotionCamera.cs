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

    internal void Apply(float zoom, Vector2 desiredOrigin)
    {
        zoom = Math.Max(1, zoom);
        var viewport = GetViewport().GetVisibleRect();
        // Fit the scene, not every dying enemy's complete picture. The latter
        // unintentionally erased the close-up and most of its shake.
        var origin = new Vector2(
            MotionCameraBounds.ClampOrigin(desiredOrigin.X, viewport.Position.X, viewport.End.X, zoom),
            MotionCameraBounds.ClampOrigin(desiredOrigin.Y, viewport.Position.Y, viewport.End.Y, zoom));
        Transform = new Transform2D(0, Vector2.One * zoom, 0, origin);
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

    internal void Release(bool exiting = false)
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
        if (!exiting) { GetParent()?.RemoveChild(this); QueueFree(); }
    }

    public override void _ExitTree() => Release(exiting: true);
}
