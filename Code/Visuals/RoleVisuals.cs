using System.Text.Json;
using Godot;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Audio;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DohnaDohna.Code.Visuals;

public partial class RoleVisuals : NCreatureVisuals
{
    public static void RegisterFactory() => RitsuGodotNodeFactories.RegisterFactory<RoleVisuals>((source, _) =>
    {
        var result = new RoleVisuals { Name = source.Name };
        foreach (Node child in source.GetChildren())
        {
            child.Owner = null;
            source.RemoveChild(child);
            result.AddChild(child);
            child.Owner = result;
            child.UniqueNameInOwner = true;
        }
        result.AddChild(new Control { Name = "FormVfx", MouseFilter = Control.MouseFilterEnum.Ignore });
        var form = result.GetNode<Control>("FormVfx");
        form.Owner = result;
        form.UniqueNameInOwner = true;
        source.Free();
        return result;
    });
    private JsonDocument? _document;
    private Node2D _layers = null!;
    private Node2D _motionBody = null!;
    private readonly Dictionary<string, Sprite2D> _sprites = [];
    private readonly Dictionary<Sprite2D, int> _layerOrder = [];
    private readonly HashSet<RoleMotionEffects> _effectLayers = [];
    private int _beforeActionDepth, _actionDepth;
    private readonly Dictionary<string, Texture2D> _textures = [];
    private MotionTimeline _idle = null!;
    private MotionTimeline? _hurt;
    private double _hurtTime;
    private int _hurtNext;
    private Vector2 _hurtOffset;
    // The authored reaction carries its knockback inside the artwork: the draw
    // position travels away from the slot and then a separate jump brings it
    // back. Ordinary hits keep the drawn pose but play where the character
    // stands, so that net travel is removed from the drawn position.
    private RoleMotionEffects? _hurtEffects;
    private readonly List<IAudioHandle> _hurtSounds = [];
    private CancellationToken _hurtCancellation;
    private JsonElement _primaryFrame;
    private readonly List<Sprite2D> _shadows = [];
    private Vector2 _ground;
    private Sprite2D? _saucer;
    private static readonly Vector2 SaucerHover = new(-35, -270);
    private static readonly Vector2 SaucerFixture = new(36, -360);
    private Vector2 _saucerPosition = SaucerHover;
    private float _saucerSpread;
    public Vector2? SaucerAnchor => _saucer != null && !_dead ? _motionBody.ToGlobal(_saucerPosition + new Vector2(0, -38)) : null;
    private ulong? _lastHurtVoice;
    private double _idleTime;
    private bool _playing;
    private bool _cinematic;
    private bool _dead;
    public bool IsPlaying => _playing || _hurt != null || _cut != null;
    public NCreature? FormationOrigin { get; set; }
    private CancellationTokenSource? _action;
    private RoleMotionEffects? _primaryEffects;
    private MotionPlacement? _placement;
    private Vector2 _returnOffset;
    // A role owns its visual tail. No detached task, shared actor dictionary or
    // card copy: replacement releases only this presentation.
    private sealed class CutPlayback(MotionRecut edit, string cue, RoleMotionEffects effects,
        CancellationTokenSource cancellation, double rate, IReadOnlyList<Creature> targets)
    {
        public readonly MotionRecut Edit = edit;
        public readonly string Cue = cue;
        public readonly RoleMotionEffects Effects = effects;
        public readonly CancellationTokenSource Cancellation = cancellation;
        public readonly CancellationToken Token = cancellation.Token;
        public readonly List<IAudioHandle> Sounds = [];
        public readonly double Rate = rate;
        public readonly IReadOnlyList<Creature> Targets = targets;
        public double Time, EntryTime;
        public int Next;
        public bool HitStarted, LogicDone;
        public Vector2 Carry;
        public Vector2 SaucerStart, SaucerDestination;
    }
    private readonly Dictionary<string, MotionRecut> _edits = [];
    private CutPlayback? _cut;
    private readonly List<IAudioHandle> _sounds = [];
    private IAudioHandle? _reactionVoice;
    private bool _audioPaused;

    public static RoleVisuals Create(RoleDefinition role)
    {
        var node = RitsuGodotNodeFactories.CreateFromScenePath<RoleVisuals>("res://DohnaDohna/scenes/member.tscn")
            ?? throw new InvalidOperationException("Role visuals could not be instantiated.");
        node._document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(role.AssetRoot + "/motions.json"));
        return node;
    }

    public override void _Ready()
    {
        base._Ready();
        _layers = GetNode<Node2D>("%Visuals");
        _motionBody = new Node2D { Name = "OriginalMotion" };
        _layers.AddChild(_motionBody);
        _idle = new MotionTimeline(_document!.RootElement.GetProperty("motions").GetProperty("idle").GetProperty("frames"));
        var ground = _document.RootElement.GetProperty("groundContact");
        _ground = new Vector2(ground[0].GetSingle(), ground[1].GetSingle());
        _motionBody.Position = -_ground;
        if (_document.RootElement.GetProperty("roleId").GetString() == "antena")
        {
            var texture = Texture("res://DohnaDohna/images/original/193b76f1c92b762da1ac.png");
            _saucer = new Sprite2D { Name = "PersistentSaucer", Texture = texture, Centered = false,
                Position = SaucerHover, Offset = new Vector2(-texture.GetWidth() * .5f, -texture.GetHeight()) };
            _motionBody.AddChild(_saucer);
        }
        float height = _document.RootElement.GetProperty("bodyHeight").GetSingle();
        Bounds.Position = new Vector2(-75, -height);
        Bounds.Size = new Vector2(150, height);
        VfxSpawnPosition.Position = new Vector2(0, -height * .5f);
        IntentPosition.Position = new Vector2(0, -height - 25);
        if (TalkPosition != null) TalkPosition.Position = new Vector2(55, -height * .85f);
        DrawFrame(_idle.Frames[0]);
    }

    public override void _Process(double delta)
    {
        bool paused = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPaused;
        if (paused != _audioPaused)
        {
            foreach (var sound in _sounds.Where(s => s.IsValid))
                if (!(paused ? sound.TryPause() : sound.TryResume())) throw new InvalidOperationException("Could not synchronize role audio pause.");
            _audioPaused = paused;
        }
        if (_document == null || _layers == null || paused) return;
        if (_cut is { } cut)
        {
            if (cut.Token.IsCancellationRequested || _dead) StopCut(cut, true);
            else
            {
                cut.Time += delta * cut.Rate;
                if (!cut.HitStarted) cut.Time = Math.Min(cut.Time, cut.Edit.Contact);
                AdvanceCut(cut);
                if (cut.LogicDone && cut.Time >= cut.Edit.Timeline.Duration) StopCut(cut, false);
            }
        }
        if (_hurt != null)
        {
            _hurtTime += delta;
            if (_hurtCancellation.IsCancellationRequested || _dead) StopHurt(true);
            else
            {
                AdvanceHurt();
                if (_hurtTime >= _hurt.Duration) StopHurt(false);
                if (_hurt != null) { DrawFrame(_hurt.Frames[_hurt.IndexAt(_hurtTime)]); return; }
            }
        }
        if (_playing || _cut != null)
        {
            if (_primaryFrame.ValueKind == JsonValueKind.Object) DrawFrame(_primaryFrame, true);
            return;
        }
        if (_dead) return;
        _idleTime = (_idleTime + delta) % _idle.Duration;
        DrawFrame(_idle.Frames[_idle.IndexAt(_idleTime)]);
    }

    private static float Number(JsonElement value, string key, float fallback = 0) =>
        value.TryGetProperty(key, out var number) ? number.GetSingle() : fallback;

    internal Rect2 PoseBounds()
    {
        Rect2? bounds = null;
        foreach (var sprite in _sprites.Values.Where(s => s.Visible))
        {
            var rect = sprite.GlobalTransform * sprite.GetRect();
            bounds = bounds is { } previous ? previous.Merge(rect) : rect;
        }
        return bounds ?? Bounds.GetGlobalRect();
    }

    internal Vector2 OriginalToGlobal(Vector2 point, bool global, bool action = false, bool impact = false)
    {
        // The flying equipment and its emitter share one rigid translation.
        // Non-action calls retain the authored formation space so placement
        // can resolve the real target before the flight starts.
        if (_saucer != null && action && global && !impact)
            return _motionBody.ToGlobal(point + _saucerPosition - SaucerFixture);
        if (action && _placement != null) point = impact ? _placement.Impact(point) : _placement.Map(point, global);
        if (global && FormationOrigin is { } origin && GodotObject.IsInstanceValid(origin))
            point += GlobalTransform.BasisXformInv(origin.Visuals.GlobalPosition - GlobalPosition);
        return _motionBody.ToGlobal(point + (!action && _hurt != null ? _hurtOffset : _returnOffset));
    }

    internal Node2D CopyPose(Node2D parent)
    {
        var pose = new Node2D { Name = "DohnaPreviousPose" };
        parent.AddChild(pose);
        foreach (var sprite in _sprites.Values.Where(s => s.Visible).OrderBy(s => _layerOrder[s]))
        {
            var copy = new Sprite2D { Texture = sprite.Texture, Centered = false,
                Offset = sprite.Offset, FlipH = sprite.FlipH, FlipV = sprite.FlipV,
                Material = sprite.Material,
                SelfModulate = new Color(80f / 255, 80f / 255, 1) };
            pose.AddChild(copy);
            copy.GlobalTransform = sprite.GlobalTransform;
        }
        return pose;
    }

    // AIN33624 uses a character-local order (Z*1000 + 50-index, +75
    // for effects). Godot Z is canvas-wide: using original negative values
    // directly hides art behind the room; positive ones cover native HitVfx.
    // Keep one local draw list at the native creature depth instead.
    internal void AttachEffect(Sprite2D sprite, int z, int index)
    {
        _motionBody.AddChild(sprite);
        _layerOrder.Add(sprite, z * 1000 + 50 - index + 75);
        SortLayers();
    }

    internal void AcquireActionDepth(RoleMotionEffects action)
    {
        if (_effectLayers.Count == 0)
        {
            _beforeActionDepth = _motionBody.ZIndex;
            // The host reserves scene=-10 and native combat VFX=-9. One local
            // step puts acting art in front of enemy bodies, but below the
            // later native VFX container at that same depth. Only action art
            // rises: ground shadows remain in the ally layer before enemies.
            _actionDepth = _beforeActionDepth + 1;
            _motionBody.ZIndex = _actionDepth;
        }
        if (!_effectLayers.Add(action)) throw new InvalidOperationException("Duplicate action draw owner");
    }

    internal void ReleaseActionDepth(RoleMotionEffects action)
    {
        if (!_effectLayers.Remove(action) || _effectLayers.Count != 0) return;
        if (GodotObject.IsInstanceValid(_motionBody) && _motionBody.ZIndex == _actionDepth)
            _motionBody.ZIndex = _beforeActionDepth;
    }

    internal void ReleaseEffect(Sprite2D sprite)
    {
        _layerOrder.Remove(sprite);
        if (GodotObject.IsInstanceValid(sprite)) { sprite.Hide(); sprite.QueueFree(); }
    }

    private void SortLayers()
    {
        foreach (var pair in _layerOrder.OrderBy(p => p.Value))
            _motionBody.MoveChild(pair.Key, -1);
    }

    private void DrawFrame(JsonElement frame, bool action = false)
    {
        foreach (var sprite in _sprites.Values) sprite.Hide();
        if (_saucer != null)
        {
            _saucer.Visible = !_dead;
            _saucer.Position = _saucerPosition;
        }
        // AIN33857 applies CgLayers and ShadowOffset only. FrameInfo.Offset is
        // end-of-action return metadata, not an additional per-frame translation.
        if (!frame.TryGetProperty("CgLayers", out var layers)) { DrawShadows(frame, action); return; }
        int layerIndex = 0;
        foreach (var layerProperty in layers.EnumerateObject())
        {
            int order = layerIndex++;
            var layer = layerProperty.Value;
            bool saucerLayer = _saucer != null && Number(layer, "IsGlobalPosition") != 0 && Number(layer, "IsShadow") != 0;
            if (saucerLayer && (_cut == null || !action)) continue;
            if (!_cinematic && layer.GetProperty("impact").GetBoolean()) continue;
            if (!layer.TryGetProperty("CgName", out var name) || string.IsNullOrEmpty(name.GetString())) continue;
            if (!_sprites.TryGetValue(layerProperty.Name, out var sprite))
            {
                sprite = new Sprite2D { Centered = false };
                _sprites.Add(layerProperty.Name, sprite);
                _motionBody.AddChild(sprite);
            }
            var path = name.GetString()!;
            if (!_textures.TryGetValue(path, out var texture))
                _textures.Add(path, texture = GD.Load<Texture2D>(path) ?? throw new InvalidDataException(path));
            sprite.Texture = texture;
            sprite.SelfModulate = Colors.White;
            sprite.Position = new Vector2(Number(layer, "PosX"), Number(layer, "PosY"));
            if (saucerLayer)
            {
                _saucer!.Hide();
                if (sprite.Position != SaucerFixture)
                    sprite.SelfModulate = new Color(1, 1, 1, _saucerSpread);
                sprite.Position += _saucerPosition - SaucerFixture;
            }
            else if (action && _placement != null)
                sprite.Position = _placement.Map(sprite.Position, Number(layer, "IsGlobalPosition") != 0);
            sprite.Position += !action && _hurt != null ? _hurtOffset : _returnOffset;
            // AIN33878 adds order × PlayerSpan. Use the actual formation
            // displacement in local body space so native camera/scale changes remain valid.
            if (!saucerLayer && Number(layer, "IsGlobalPosition") != 0 && FormationOrigin is { } origin && GodotObject.IsInstanceValid(origin))
                sprite.Position += GlobalTransform.BasisXformInv(origin.Visuals.GlobalPosition - GlobalPosition);
            sprite.Material = RoleMotionEffects.Blend(layer.GetProperty("DrawFilter").GetString()!);
            // AIN9224 proves EOriginPos is one-based: MM=5, MB=8.
            // Using the metadata array index incorrectly shifted every width-changing frame.
            sprite.Offset = new Vector2(-texture.GetWidth() * .5f, -texture.GetHeight() * (Number(layer, "IsCenterOrigin") == 1 ? 0.5f : 1));
            sprite.FlipH = Number(layer, "ReverseLR") != 0;
            sprite.RotationDegrees = Number(layer, "Rotation");
            _layerOrder[sprite] = (int)Number(layer, "Z") * 1000 + 50 - order;
            sprite.Show();
        }
        SortLayers();
        DrawShadows(frame, action);
    }

    private Texture2D Texture(string path)
    {
        if (!_textures.TryGetValue(path, out var texture))
            _textures.Add(path, texture = GD.Load<Texture2D>(path) ?? throw new InvalidDataException(path));
        return texture;
    }

    private void DrawShadows(JsonElement frame, bool action)
    {
        foreach (var shadow in _shadows) shadow.Hide();
        int index = 0;
        foreach (var entry in frame.GetProperty("shadows").EnumerateArray())
        {
            if (_saucer != null && entry.GetProperty("global").GetBoolean()) continue;
            if (!_cinematic && entry.GetProperty("impact").GetBoolean()) continue;
            if (index == _shadows.Count)
            {
                // Native scene depth, before the body in tree order. Negative
                // canvas Z escaped the actor and put shadows behind the scenery.
                var sprite = new Sprite2D { Centered = false };
                _layers.AddChild(sprite);
                _layers.MoveChild(sprite, 0);
                _shadows.Add(sprite);
            }
            var shadow = _shadows[index++];
            shadow.Texture = Texture(entry.GetProperty("image").GetString()!);
            var origin = entry.GetProperty("origin");
            // The baked painted footprint is already projected onto the floor.
            // Do not flatten it a second time or translate it with body elevation.
            float elevation = entry.GetProperty("elevation").GetSingle();
            float size = 1 / (1 + elevation / 500);
            float anchor = entry.GetProperty("anchorX").GetSingle();
            shadow.Scale = Vector2.One * size;
            // Shrink around its own projection anchor, not the actor origin:
            // otherwise a travelling/jumping shadow slides backwards as it shrinks.
            shadow.Offset = new Vector2(origin[0].GetSingle() - anchor, origin[1].GetSingle());
            shadow.Position = new Vector2(anchor - _ground.X + Number(frame, "ShadowOffsetX"), Number(frame, "ShadowOffsetY"))
                + (!action && _hurt != null ? _hurtOffset : _returnOffset);
            if (action && _placement != null)
            {
                shadow.Position += _placement.Map(new Vector2(anchor, 0), entry.GetProperty("global").GetBoolean(), true) - new Vector2(anchor, 0);
            }
            if (entry.GetProperty("global").GetBoolean() && FormationOrigin is { } front && GodotObject.IsInstanceValid(front))
                shadow.Position += GlobalTransform.BasisXformInv(front.Visuals.GlobalPosition - GlobalPosition);
            shadow.Modulate = new Color(1, 1, 1, size);
            shadow.Show();
        }
    }

    public async Task Play(string cue, Func<Task>? hit, CancellationToken cancellation, IReadOnlyList<Creature>? targets = null, bool cinematic = false)
    {
        if (cue is "strike" or "special")
        {
            await PlayCut(cue, hit, cancellation, targets ?? [], cinematic);
            return;
        }
        PruneSounds();
        if (_cut is { } cut) StopCut(cut, true);
        _action?.Cancel();
        if (GodotObject.IsInstanceValid(_primaryEffects)) { _primaryEffects!.Restore(); _primaryEffects.Hide(); }
        using var token = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _action = token;
        var sounds = new List<IAudioHandle>();
        var motion = _document!.RootElement.GetProperty("motions").GetProperty(cue);
        var timeline = new MotionTimeline(motion.GetProperty("frames"));
        var frames = timeline.Frames;
        var effects = new RoleMotionEffects();
        _primaryEffects = effects;
        _layers.AddChild(effects);
        _playing = true;
        _cinematic = false;
        _placement = null;
        double elapsed = 0;
        int next = 0;
        try
        {
            effects.Initialize(this, targets ?? [], timeline, stage: false);
            while (next < frames.Length || elapsed < timeline.Duration)
            {
                token.Token.ThrowIfCancellationRequested();
                while (next < frames.Length && timeline.Starts[next] <= elapsed)
                {
                    int i = next++;
                    _primaryFrame = frames[i];
                    effects.AddFrame(frames[i], timeline.Starts[i]);
                    PlayFrameAudio(frames[i], sounds);
                }
                if (_hurt == null) DrawFrame(_primaryFrame);
                effects.Advance(elapsed);
                await this.AwaitProcessFrame(token.Token);
                if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPaused) elapsed += GetProcessDeltaTime();
            }
            float endOffset = Number(frames[^1], "OffsetX");
            if (cue == "cast" && endOffset != 0 && !_dead)
                await PlayReturn(new Vector2(endOffset, 0), endOffset, sounds, token.Token, true);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
        finally
        {
            if (GodotObject.IsInstanceValid(effects)) { effects.Restore(); effects.QueueFree(); }
            if (_primaryEffects == effects) _primaryEffects = null;
            if (_action == token)
            {
                _playing = false;
                _placement = null;
                _returnOffset = Vector2.Zero;
                _action = null;
                _primaryFrame = default;
                if (IsInsideTree()) DrawFrame(_dead ? frames[^1] : _idle.Frames[0]);
            }
            if (token.IsCancellationRequested) ReleaseSounds(sounds);
            PruneSounds();
        }
    }

    private async Task PlayCut(string cue, Func<Task>? hit, CancellationToken cancellation,
        IReadOnlyList<Creature> targets, bool cinematic)
    {
        cancellation.ThrowIfCancellationRequested();
        PruneSounds();
        if (!_edits.TryGetValue(cue, out var edit))
            _edits.Add(cue, edit = MotionRecut.Create(_document!.RootElement.GetProperty("roleId").GetString()!,
                cue, _document.RootElement.GetProperty("motions").GetProperty(cue)));
        var previous = _cut;
        bool resume = previous != null && previous.HitStarted && previous.Targets.SequenceEqual(targets)
            && previous.Time < previous.Edit.Timeline.Starts[^3] &&
            (previous.Cue == cue || _document!.RootElement.GetProperty("roleId").GetString() is "kuma" or "antena");
        var carry = previous == null ? Vector2.Zero :
            new Vector2(Number(_primaryFrame, "actionAnchorX"), 0) + _returnOffset;
        var saucerStart = _saucerPosition;
        if (previous != null) StopCut(previous, true);
        _action?.Cancel();
        // A cast/return coroutine must lose ownership before this cut starts;
        // its later finally must not reset the new attack's frame/placement.
        _action = null;
        _playing = false;
        if (GodotObject.IsInstanceValid(_primaryEffects)) { _primaryEffects!.Restore(); _primaryEffects.Hide(); }
        var mode = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode;
        double rate = mode == MegaCrit.Sts2.Core.Settings.FastModeType.Fast ? 2 : 1;
        bool instant = mode == MegaCrit.Sts2.Core.Settings.FastModeType.Instant;
        // A finisher gets a readable anticipation and a short recovery, not the
        // uncut source movie. Instant mode never waits for visual presentation.
        if (cinematic && !instant) rate *= .65;
        var effects = new RoleMotionEffects();
        _layers.AddChild(effects);
        var state = new CutPlayback(edit, cue, effects,
            CancellationTokenSource.CreateLinkedTokenSource(cancellation), rate, targets);
        if (resume)
        {
            state.Next = edit.ResumeIndex;
            state.Time = edit.Timeline.Starts[state.Next];
        }
        state.EntryTime = state.Time;
        state.Carry = carry - new Vector2(Number(edit.Timeline.Frames[state.Next], "actionAnchorX"), 0);
        _cut = state;
        _primaryEffects = effects;
        var nodes = targets.Select(c => c.GetCreatureNode()).OfType<NCreature>().ToArray();
        var motion = _document!.RootElement.GetProperty("motions").GetProperty(cue);
        _returnOffset = Vector2.Zero;
        _placement = nodes.Length == 0 ? null : new MotionPlacement(this, nodes,
            motion.GetProperty("metadata").GetProperty("Range").GetInt32(), compact: true);
        state.SaucerStart = saucerStart;
        state.SaucerDestination = SaucerHover;
        if (_saucer != null && _placement != null)
        {
            // AIN33555/33841 keep equipment and emission in the same fixture.
            // Resolve that fixture against actual targets, without moving the
            // member, its health bar, or a monster's native body.
            state.SaucerDestination = _placement.Map(SaucerFixture, true);
            if (FormationOrigin is { } origin && GodotObject.IsInstanceValid(origin))
                state.SaucerDestination += GlobalTransform.BasisXformInv(origin.Visuals.GlobalPosition - GlobalPosition);
        }
        // Original impact art/audio is omitted in the short edit, including
        // finishers. Native contact feedback remains exactly once per command.
        _cinematic = false;
        effects.Initialize(this, targets, edit.Timeline, cinematic: cinematic, recut: true);
        bool resolved = false;
        try
        {
            AdvanceCut(state);
            if (instant) { state.Time = edit.Contact; AdvanceCut(state); }
            while (state.Time < edit.Contact)
            {
                state.Token.ThrowIfCancellationRequested();
                await this.AwaitProcessFrame(state.Token);
            }
            state.Token.ThrowIfCancellationRequested();
            state.HitStarted = true; // Before await: reflection can cause real death.
            if (hit != null) await hit();
            resolved = true;
            state.LogicDone = true;
            if (cinematic && !instant)
                while (_cut == state && state.Time < edit.Timeline.Duration)
                {
                    state.Token.ThrowIfCancellationRequested();
                    await this.AwaitProcessFrame(state.Token);
                }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
        finally
        {
            if (_cut == state && (!resolved || cinematic || instant || state.Token.IsCancellationRequested))
                StopCut(state, !resolved || state.Token.IsCancellationRequested);
        }
    }

    private void AdvanceCut(CutPlayback state)
    {
        var timeline = state.Edit.Timeline;
        _returnOffset = state.Carry * (1 - (float)Math.Clamp((state.Time - state.EntryTime) / .12, 0, 1));
        while (state.Next < timeline.Frames.Length && timeline.Starts[state.Next] <= state.Time)
        {
            int index = state.Next++;
            _primaryFrame = timeline.Frames[index];
            _placement?.SetFrame(_primaryFrame);
            AdvanceSaucer(state, timeline.Starts[index]);
            // Update the body before sampling emission transforms.
            if (_hurt == null) DrawFrame(_primaryFrame, true);
            state.Effects.AddFrame(_primaryFrame, timeline.Starts[index]);
            PlayFrameAudio(_primaryFrame, state.Sounds, playVoice: false);
        }
        AdvanceSaucer(state, state.Time);
        state.Effects.Advance(state.Time);
    }

    private void AdvanceSaucer(CutPlayback state, double time)
    {
        if (_saucer == null) return;
        var timeline = state.Edit.Timeline;
        // Reach the firing fixture before the first charge/beam, hold still
        // while the emitted beam sweeps, then return during the visual tail.
        double arrival = timeline.Starts[state.Edit.ContactIndex - 1];
        float outbound = MotionTimeline.Ease("EaseInOutCubic",
            (float)((time - state.EntryTime) / Math.Max(arrival - state.EntryTime, 1d / 60)));
        double returning = timeline.Starts[9];
        float inbound = MotionTimeline.Ease("EaseInOutCubic",
            (float)((time - returning) / (timeline.Duration - returning)));
        _saucerPosition = state.SaucerStart.Lerp(state.SaucerDestination, outbound).Lerp(SaucerHover, inbound);
        _saucerSpread = outbound * (1 - inbound);
    }

    private void StopCut(CutPlayback state, bool canceled)
    {
        if (canceled) state.Cancellation.Cancel();
        if (GodotObject.IsInstanceValid(state.Effects)) { state.Effects.Restore(); state.Effects.QueueFree(); }
        if (_primaryEffects == state.Effects) _primaryEffects = null;
        // Edited movement/charge clips must not outlive their shortened action.
        ReleaseSounds(state.Sounds);
        state.Cancellation.Dispose();
        if (_cut != state) return;
        _cut = null;
        _saucerPosition = SaucerHover;
        _saucerSpread = 0;
        _placement = null;
        _returnOffset = Vector2.Zero;
        _primaryFrame = default;
        if (!_dead && IsInsideTree() && _hurt == null) DrawFrame(_idle.Frames[0]);
        PruneSounds();
    }

    private void ReleaseSounds(IEnumerable<IAudioHandle> sounds)
    {
        foreach (var sound in sounds)
            if (_sounds.Remove(sound)) { sound.TryStop(false); sound.Dispose(); }
    }

    private async Task PlayReturn(Vector2 from, float authoredOffset, List<IAudioHandle> sounds, CancellationToken token, bool playVoice)
    {
        // AIN33516/33508/33743: a nonzero end offset switches to the actual
        // jump frames. Translation is linear between JumpStart/JumpEnd; the
        // artwork supplies the vertical arc. Duration is 400 + .8*clamp(d,0,1000) ms.
        var timeline = new MotionTimeline(_document!.RootElement.GetProperty("motions").GetProperty("return").GetProperty("frames"));
        double speed = timeline.Duration / MotionTimeline.ReturnDuration(authoredOffset);
        var effects = new RoleMotionEffects();
        _primaryEffects = effects;
        _layers.AddChild(effects);
        effects.Initialize(this, [], timeline, stage: false);
        int next = 0;
        double elapsed = 0;
        try
        {
            while (elapsed < timeline.Duration || next < timeline.Frames.Length)
            {
                token.ThrowIfCancellationRequested();
                while (next < timeline.Frames.Length && timeline.Starts[next] <= elapsed)
                {
                    int i = next++;
                    _returnOffset = from * (1 - timeline.JumpProgress(timeline.Starts[i]));
                    effects.AddFrame(timeline.Frames[i], timeline.Starts[i]);
                    _primaryFrame = timeline.Frames[i];
                    PlayFrameAudio(_primaryFrame, sounds, playVoice: playVoice);
                }
                _returnOffset = from * (1 - timeline.JumpProgress(elapsed));
                if (_hurt == null) DrawFrame(_primaryFrame, true);
                effects.Advance(elapsed);
                await this.AwaitProcessFrame(token);
                if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPaused) elapsed += GetProcessDeltaTime() * speed;
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(effects)) { effects.Restore(); effects.QueueFree(); }
            if (_primaryEffects == effects) _primaryEffects = null;
        }
    }

    public async Task Wait(double seconds, CancellationToken token)
    {
        double passed = 0;
        while (passed < seconds)
        {
            token.ThrowIfCancellationRequested();
            await this.AwaitProcessFrame(token);
            if (!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsPaused) passed += GetProcessDeltaTime();
        }
    }

    private void StopSounds()
    {
        foreach (var sound in _sounds) { sound.TryStop(false); sound.Dispose(); }
        _sounds.Clear();
        _reactionVoice = null;
    }

    private void PlayFrameAudio(JsonElement frame, List<IAudioHandle> owner, bool cinematic = false, bool playVoice = true)
    {
        foreach (var category in new[] { "Sound", "Voice" })
        {
            if (category == "Voice" && !playVoice) continue;
            if (!MotionTimeline.Enabled(frame, category, out var audio)) continue;
            for (int n = 1; n <= 3; n++)
                if (audio.TryGetProperty(category + n, out var reference) && !string.IsNullOrEmpty(reference.GetString()))
                {
                    if (category == "Sound" && !cinematic && audio.GetProperty("impact" + n).GetBoolean()) continue;
                    var sound = SquadAudio.Play(reference.GetString()!);
                    _sounds.Add(sound);
                    owner.Add(sound);
                }
        }
    }

    private void AdvanceHurt()
    {
        while (_hurtNext < _hurt!.Frames.Length && _hurt.Starts[_hurtNext] <= _hurtTime)
        {
            int next = _hurtNext++;
            _hurtEffects!.AddFrame(_hurt.Frames[next], _hurt.Starts[next]);
        }
        _hurtEffects!.Advance(_hurtTime);
    }

    private void StopHurt(bool canceled)
    {
        _hurt = null;
        _hurtOffset = Vector2.Zero;
        if (GodotObject.IsInstanceValid(_hurtEffects)) { _hurtEffects!.Restore(); _hurtEffects.QueueFree(); }
        _hurtEffects = null;
        if (canceled)
            foreach (var sound in _hurtSounds)
                if (_sounds.Remove(sound)) { sound.TryStop(false); sound.Dispose(); }
        _hurtSounds.Clear();
        PruneSounds();
    }

    private void PruneSounds()
    {
        // The installed FMOD Studio event exposes native STOPPED=2. Retain
        // playing tails for room/role cancellation; release finished events.
        for (int i = _sounds.Count - 1; i >= 0; i--)
        {
            var sound = _sounds[i];
            if (sound.IsValid && sound.RawInstance!.Call("get_playback_state").AsInt32() != 2) continue;
            sound.Dispose();
            _sounds.RemoveAt(i);
            if (_reactionVoice == sound) _reactionVoice = null;
        }
    }

    private void PlayReactionVoice(string category)
    {
        ulong now = Time.GetTicksMsec();
        if (category == "hurtVoice")
        {
            // AIN30389 uses strict last + 300 < now; death bypasses this gate.
            if (!CanReplayHurtVoice(now, _lastHurtVoice)) return;
            _lastHurtVoice = now;
        }
        if (_reactionVoice != null)
        {
            _reactionVoice.TryStop(false);
            _reactionVoice.Dispose();
            _sounds.Remove(_reactionVoice);
        }
        var voices = _document!.RootElement.GetProperty(category);
        if (voices.GetArrayLength() == 0) throw new InvalidDataException("Missing role reaction voice: " + category);
        // Presentation-only random selection: never consume the run/card RNG.
        _reactionVoice = SquadAudio.Play(voices[GD.RandRange(0, voices.GetArrayLength() - 1)].GetString()!);
        _sounds.Add(_reactionVoice);
    }

    private static bool CanReplayHurtVoice(ulong now, ulong? last) => last == null || now > last.Value + 300;

    public Task PlayHurt(CancellationToken token)
    {
        if (_dead) return Task.CompletedTask;
        token.ThrowIfCancellationRequested();
        StopHurt(true);
        PlayReactionVoice("hurtVoice");
        var source = _document!.RootElement.GetProperty("motions").GetProperty("hit").GetProperty("frames");
        _hurt = MotionRecut.Hurt(source, _idle.Frames[0]);
        _hurtTime = 0;
        _hurtNext = 0;
        _hurtCancellation = token;
        _hurtEffects = new RoleMotionEffects();
        _layers.AddChild(_hurtEffects);
        _hurtEffects.Initialize(this, [], _hurt, stage: false);
        AdvanceHurt();
        DrawFrame(_hurt.Frames[0]);
        return Task.CompletedTask;
    }

    public static Task PlayAttack(Creature actor, string cue, Func<Task> hit, CancellationToken token, IEnumerable<Creature> targets, bool cinematic = false) =>
        (actor.GetCreatureNode()?.Visuals as RoleVisuals
            ?? throw new InvalidOperationException("Missing DohnaDohna actor visuals.")).Play(cue, hit, token,
                targets.ToArray(), cinematic);

    public static async Task PlayDeath(Creature actor, CancellationToken token)
    {
        if (actor.GetCreatureNode()?.Visuals is not RoleVisuals visuals) return;
        visuals._dead = true;
        visuals.StopHurt(true);
        visuals.PlayReactionVoice("deathVoice");
        // Snapshot once: changing the preference does not interrupt a death in flight.
        var poster = PresentationSettings.ShowFemaleDeathCutIn
            ? visuals._document!.RootElement.GetProperty("poster").GetString() : null;
        await Task.WhenAll(visuals.Play("dead", null, token), poster == null ? Task.CompletedTask
            : DohnaDohna.Code.UI.DeathCutIn.Show(visuals.GetTree().Root, poster, token));
        actor.GetCreatureNode()?.Hide();
    }

    public override void _ExitTree()
    {
        if (_cut is { } cut) StopCut(cut, true);
        _action?.Cancel();
        if (GodotObject.IsInstanceValid(_primaryEffects)) _primaryEffects!.Restore();
        StopHurt(true);
        StopSounds();
        _document?.Dispose();
        base._ExitTree();
    }
}
