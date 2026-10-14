using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DohnaDohna.Code.Visuals;

/// <summary>Action-owned emissions only; no camera, target body motion or damage.</summary>
public sealed partial class RoleMotionEffects : Node2D
{
    private sealed class Sequence(Sprite2D sprite, Texture2D[] frames, double frameTime, double start, Transform2D emission)
    {
        public Sprite2D Sprite = sprite;
        public Texture2D[] Frames = frames;
        public double FrameTime = frameTime, Start = start;
        public Transform2D Emission = emission;
        public float? LeftPivot;
    }
    private readonly List<Sequence> _sequences = [];
    private readonly List<(JsonElement Effect, double Time, int Order)> _tracers = [];
    private readonly List<(JsonElement Effect, int Order)> _contactTrails = [];
    private double _time;
    internal bool HasEmissions => _sequences.Count != 0;
    internal void AdvanceTail(double delta) => Advance(_time + delta);
    internal void EndEmission() { _tracers.Clear(); _contactTrails.Clear(); }
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
    private bool _stage, _drawConnected, _originalEffects;
    internal void Initialize(RoleVisuals source, IReadOnlyList<Creature> targets, MotionTimeline timeline,
        bool stage = true, bool originalEffects = false)
    {
        _source = source;
        _targets = targets.Select(c => c.GetCreatureNode()).OfType<NCreature>().ToArray();
        _stage = stage;
        _originalEffects = originalEffects;
        var room = NCombatRoom.Instance!;
        Reparent(room.SceneContainer, false);
        GlobalTransform = source.GlobalTransform;
        source.AcquireActionDepth(this);
        RenderingServer.FramePreDraw += PlaceEmissions;
        _drawConnected = true;
    }

    public void AddFrame(JsonElement frame, double time)
    {
        // Only a confirmed finisher owns imported emissions. Ordinary actions
        // keep body/weapon drawings and their single native contact feedback.
        if (!_originalEffects) return;
        Advance(time);
        if (frame.TryGetProperty("Effects", out var effects))
        {
            int effectIndex = 0;
            foreach (var entry in effects.EnumerateObject())
            {
                int order = effectIndex++;
                var effect = entry.Value;
                if (effect.GetProperty("impact").GetBoolean()) continue;
                if (effect.TryGetProperty("contactTrail", out var trail) && trail.GetBoolean())
                { _contactTrails.Add((effect, order)); continue; }
                if (_stage && _source.RoleId == "kuma" && effect.GetProperty("FirstCgName").GetString()
                    == "res://DohnaDohna/images/original/375b92627e7c234e3c22.png")
                {
                    // This sequence contains a large expansion flash followed
                    // by a thin trail, not a uniformly thin laser. Separate
                    // those phases when the native target becomes known.
                    _tracers.Add((effect, time, order));
                    continue;
                }
                Emit(effect, time, order);
            }
        }
    }

    private void Emit(JsonElement effect, double time, int order, Vector2? contact = null)
    {
                bool toTarget = effect.GetProperty("AssignToTarget").GetInt32() != 0;
                IEnumerable<NCreature?> anchors = toTarget && contact == null ? _targets : new NCreature?[] { null };
                foreach (var anchor in anchors)
                {
                    var textures = effect.GetProperty("images").EnumerateArray().Select(p => GD.Load<Texture2D>(p.GetString()!)).ToArray();
                    if (textures.Length == 0) throw new InvalidDataException("Empty imported original effect sequence.");
                    var offset = new Vector2(effect.GetProperty("PosX").GetSingle(), effect.GetProperty("PosY").GetSingle());
                    Vector2 position;
                    if (contact != null) position = ToLocal(contact.Value);
                    else if (anchor != null)
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
                    var sprite = new Sprite2D { Name = contact != null ? "DohnaContactTrail" : "DohnaMotionEffect", Centered = false, Texture = textures[0],
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

    internal void ContactTargets(IReadOnlyList<Creature> targets)
    {
        var points = targets.Select(t => t.GetCreatureNode()).OfType<NCreature>().Select(n => n.VfxSpawnPosition).ToArray();
        if (points.Length != 0)
            foreach (var (effect, order) in _contactTrails)
                Emit(effect, _time, order, points.Aggregate(Vector2.Zero, (a, b) => a + b) / points.Length);
        _contactTrails.Clear();
        foreach (var (effect, time, order) in _tracers)
        {
            var textures = effect.GetProperty("images").EnumerateArray().Select(p => GD.Load<Texture2D>(p.GetString()!)).ToArray();
            float scale = effect.GetProperty("ScalingPer").GetSingle() / 100;
            var offset = new Vector2(effect.GetProperty("PosX").GetSingle(), effect.GetProperty("PosY").GetSingle());
            var muzzle = offset + new Vector2((13 - textures[0].GetWidth() * .5f) * scale, 0);
            var origin = ToLocal(_source.OriginalToGlobal(muzzle, false, true));
            bool muzzleCreated = false;
            foreach (var target in targets.Select(t => t.GetCreatureNode()).OfType<NCreature>())
            {
                var direction = ToLocal(target.VfxSpawnPosition) - origin;
                // Original frames 0–7 bloom to 152px high before the trail
                // settles. They stay compact at the muzzle instead of being
                // stretched across the field. Frames 8+ supply the thin line.
                if (!muzzleCreated)
                {
                    AddShot("DohnaGunMuzzle", textures.Take(8).ToArray(), Vector2.One * .55f, 0);
                    muzzleCreated = true;
                }
                AddShot("DohnaGunTracer", textures.Skip(8).ToArray(),
                    new Vector2(direction.Length() / (textures[0].GetWidth() - 13), .65f), direction.Angle());

                void AddShot(string name, Texture2D[] pictures, Vector2 size, float angle)
                {
                    var sprite = new Sprite2D { Name = name, Centered = false, Texture = pictures[0],
                        Position = origin, Rotation = angle, Offset = new Vector2(-13, -pictures[0].GetHeight() * .5f),
                        Scale = size, Material = Blend(effect.GetProperty("DrawFilter").GetString()!) };
                    var emission = sprite.Transform;
                    _source.AttachEffect(sprite, effect.GetProperty("Z").GetInt32(), order);
                    _sequences.Add(new Sequence(sprite, pictures, 2d / 60 * 100 / effect.GetProperty("TimeScalingPer").GetDouble(), time, emission)
                        { LeftPivot = 13 });
                    sprite.GlobalTransform = GlobalTransform * emission;
                }
            }
        }
        _tracers.Clear();
    }

    public void Advance(double time)
    {
        _time = time;
        for (int i = _sequences.Count - 1; i >= 0; i--)
        {
            var sequence = _sequences[i];
            int index = (int)((time - sequence.Start) / sequence.FrameTime);
            if (index >= sequence.Frames.Length)
            { _source.ReleaseEffect(sequence.Sprite); _sequences.RemoveAt(i); continue; }
            var texture = sequence.Frames[index];
            sequence.Sprite.Texture = texture;
            sequence.Sprite.Offset = sequence.LeftPivot is { } left ? new Vector2(-left, -texture.GetHeight() * .5f) : -texture.GetSize() * .5f;
        }

        PlaceEmissions();
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
        _tracers.Clear();
        _contactTrails.Clear();
        _source?.ReleaseActionDepth(this);
    }
    public override void _ExitTree() => Restore();
}
