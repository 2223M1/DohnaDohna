using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DohnaDohna.Code.Visuals;

/// <summary>Action-owned emissions and optional compact finisher camera; no target body motion or damage.</summary>
public sealed partial class RoleMotionEffects : Node2D
{
    private sealed class Sequence(Sprite2D sprite, Texture2D[] frames, double frameTime, double start, Transform2D emission)
    {
        public Sprite2D Sprite = sprite;
        public Texture2D[] Frames = frames;
        public double FrameTime = frameTime, Start = start;
        public Transform2D Emission = emission;
    }
    private readonly List<Sequence> _sequences = [];
    private static readonly CanvasItemMaterial Additive = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    private static readonly ShaderMaterial Multiply = new()
    { Shader = GD.Load<Shader>("res://DohnaDohna/shaders/motion_multiply.gdshader") };
    internal static Material? Blend(string filter) => filter switch
    {
        "Normal" => null, "Add" => Additive, "Multiply" => Multiply,
        _ => throw new InvalidDataException("Unsupported original blend: " + filter)
    };

    private RoleVisuals _source = null!;
    private NCreature[] _targets = [];
    private Rect2? _targetBounds;
    private MotionTimeline _timeline = null!;
    private bool _stage, _cinematic, _recut, _drawConnected;
    private MotionCamera? _camera;
    private ColorRect? _background;
    private readonly List<AlphaChange> _bodies = [];
    private readonly List<AlphaChange> _otherHud = [];
    private sealed class AlphaChange(CanvasItem item)
    {
        private Color? _before, _written;
        public void Undo()
        {
            if (GodotObject.IsInstanceValid(item) && _written is { } written && item.Modulate == written)
                item.Modulate = _before!.Value;
            _before = _written = null;
        }
        public void Apply(float alpha)
        {
            Undo();
            if (!GodotObject.IsInstanceValid(item)) return;
            _before = item.Modulate;
            item.Modulate *= new Color(1, 1, 1, alpha);
            _written = item.Modulate;
        }
    }


    internal void Initialize(RoleVisuals source, IReadOnlyList<Creature> targets, MotionTimeline timeline,
        bool stage = true, bool cinematic = false, bool recut = false)
    {
        _source = source;
        _targets = targets.Select(c => c.GetCreatureNode()).OfType<NCreature>().ToArray();
        foreach (var target in _targets)
        {
            var bounds = target.Visuals.Bounds.GetGlobalRect();
            _targetBounds = _targetBounds is { } previous ? previous.Merge(bounds) : bounds;
        }
        _timeline = timeline;
        _stage = stage;
        _cinematic = cinematic;
        _recut = recut;
        var room = NCombatRoom.Instance!;
        Reparent(room.SceneContainer, false);
        GlobalTransform = source.GlobalTransform;
        source.AcquireActionDepth(this);
        RenderingServer.FramePreDraw += PlaceEmissions;
        _drawConnected = true;
        if (!cinematic) return;
        _camera = MotionCamera.Create(room);
        _background = new ColorRect { Name = "DohnaActionLighting", MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.Transparent };
        room.BackCombatVfxContainer.AddChild(_background);
        _background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var node in room.CreatureNodes.Where(n => n.Visuals != source && !_targets.Contains(n)))
        {
            _bodies.Add(new AlphaChange(node.Visuals.GetCurrentBody()));
            _otherHud.Add(new AlphaChange(node.GetNode<Control>("%HealthBar")));
            _otherHud.Add(new AlphaChange(node.IntentContainer));
        }
    }

    public void AddFrame(JsonElement frame, double time)
    {
        Advance(time);
        if (frame.TryGetProperty("Effects", out var effects))
        {
            int effectIndex = 0;
            foreach (var entry in effects.EnumerateObject())
            {
                int order = effectIndex++;
                var effect = entry.Value;
                if ((!_cinematic || _recut) && effect.GetProperty("impact").GetBoolean()) continue;
                bool toTarget = effect.GetProperty("AssignToTarget").GetInt32() != 0;
                IEnumerable<NCreature?> anchors = toTarget ? _targets : new NCreature?[] { null };
                foreach (var anchor in anchors)
                {
                    var textures = effect.GetProperty("images").EnumerateArray().Select(p => GD.Load<Texture2D>(p.GetString()!)).ToArray();
                    if (textures.Length == 0) throw new InvalidDataException("Empty imported original effect sequence.");
                    var offset = new Vector2(effect.GetProperty("PosX").GetSingle(), effect.GetProperty("PosY").GetSingle());
                    Vector2 position;
                    if (anchor != null)
                    {
                        // Original target-side effects are mirrored. Use the
                        // native hit anchor (including airborne/tall bodies), not HUD origin.
                        var visuals = anchor.Visuals;
                        var body = visuals.GetCurrentBody();
                        var hitPoint = body.ToGlobal(visuals.ToLocal(visuals.VfxSpawnPosition.GlobalPosition));
                        // Original target effects are relative to the target foot,
                        // not its hit marker. -160 is the adapted human torso plane.
                        position = ToLocal(hitPoint + _source.GlobalTransform.BasisXform(new Vector2(-offset.X, offset.Y + 160)));
                    }
                    else
                    {
                        position = ToLocal(_source.OriginalToGlobal(offset, effect.GetProperty("IsGlobalPosition").GetInt32() != 0,
                            _stage, effect.GetProperty("anchorBinding").GetString() == "impact"));
                    }
                    var sprite = new Sprite2D { Centered = false, Texture = textures[0],
                        Position = position,
                        Scale = Vector2.One * (effect.GetProperty("ScalingPer").GetSingle() / 100f),
                        FlipH = (effect.GetProperty("Reverse").GetInt32() != 0) != toTarget,
                        Material = Blend(effect.GetProperty("DrawFilter").GetString()!) };
                    // AIN33833 uses MM=5 (the original enum is one-based).
                    sprite.Offset = -textures[0].GetSize() * .5f;
                    var emission = sprite.Transform;
                    _source.AttachEffect(sprite, effect.GetProperty("Z").GetInt32(), order);
                    // FrameInfoEffect.GetFrameTime (AIN29505): two original frames, scaled by playback speed.
                    _sequences.Add(new Sequence(sprite, textures, 2.0 / 60 * 100 / effect.GetProperty("TimeScalingPer").GetDouble(), time, emission));
                    sprite.GlobalTransform = GlobalTransform * emission;
                }
            }
        }

    }

    public void Advance(double time)
    {
        for (int i = _sequences.Count - 1; i >= 0; i--)
        {
            var sequence = _sequences[i];
            int index = (int)((time - sequence.Start) / sequence.FrameTime);
            if (index >= sequence.Frames.Length)
            { _source.ReleaseEffect(sequence.Sprite); _sequences.RemoveAt(i); continue; }
            var texture = sequence.Frames[index];
            sequence.Sprite.Texture = texture;
            sequence.Sprite.Offset = -texture.GetSize() * .5f;
        }

        if (_cinematic) ApplyCompactFinish(time);
        PlaceEmissions();
    }

    private void ApplyCompactFinish(double time)
    {
        float blend = (float)Math.Clamp(Math.Min(time / .09, (_timeline.Duration - time) / .20), 0, 1);
        blend = blend * blend * (3 - 2 * blend);
        var subjects = _source.PoseBounds();
        if (_targetBounds is { } bounds) subjects = subjects.Merge(bounds);
        float zoom = 1 + .16f * blend;
        var center = GetViewport().GetVisibleRect().GetCenter();
        _camera?.Apply(zoom, center - subjects.GetCenter() * zoom, subjects);
        if (_background != null) _background.Color = new Color(.025f, .035f, .055f, .70f * blend);
        foreach (var body in _bodies) body.Apply(1 - .85f * blend);
        foreach (var hud in _otherHud) hud.Apply(1 - .85f * blend);
    }

    private void PlaceEmissions()
    {
        // Emissions stay in captured scene space, including native screen shake;
        // moving the actor afterward must not drag an already-fired effect.
        foreach (var sequence in _sequences)
            sequence.Sprite.GlobalTransform = GlobalTransform * sequence.Emission;
    }

    public void Restore()
    {
        if (_drawConnected) { RenderingServer.FramePreDraw -= PlaceEmissions; _drawConnected = false; }
        foreach (var sequence in _sequences) _source.ReleaseEffect(sequence.Sprite);
        _sequences.Clear();
        _source?.ReleaseActionDepth(this);
        if (GodotObject.IsInstanceValid(_camera)) _camera!.Release();
        _camera = null;
        foreach (var body in _bodies) body.Undo();
        _bodies.Clear();
        foreach (var hud in _otherHud) hud.Undo();
        _otherHud.Clear();
        if (GodotObject.IsInstanceValid(_background)) _background!.QueueFree();
        _background = null;
    }
    public override void _ExitTree() => Restore();
}
