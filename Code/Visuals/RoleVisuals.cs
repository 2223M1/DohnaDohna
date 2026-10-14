using System.Text.Json;
using Godot;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Audio;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Commands;
using DohnaDohna.Code.Squad;

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
    private readonly List<RoleMotionEffects> _effectTails = [];
    private int _beforeActionDepth, _actionDepth;
    private readonly Dictionary<string, Texture2D> _textures = [];
    private MotionTimeline _idle = null!;
    private MotionTimeline? _hurt;
    private double _hurtTime;
    private Vector2 _hurtOffset;
    private float _hurtStartX;
    // Full authored hit poses, with a small body-only recoil. Formation/HUD
    // never move and no return-jump controller is entered by an ordinary hit.
    private CancellationToken _hurtCancellation;
    private JsonElement _primaryFrame;
    private readonly List<Sprite2D> _shadows = [];
    private Vector2 _ground;
    private Sprite2D? _saucer;
    private static readonly Vector2 SaucerHover = new(-35, -270);
    private static readonly Vector2 SaucerFixture = new(36, -360);
    private Vector2 _saucerPosition = SaucerHover;
    private float _saucerSpread;
    public Vector2? SaucerAnchor => _saucerOrbAnchor != null && !_dead ? _saucerOrbAnchor.GlobalPosition : null;
    private ulong? _lastHurtVoice;
    private double _idleTime;
    private bool _playing;
    private bool _cinematic;
    private bool _dead;
    public bool IsPlaying => _playing || _hurt != null || _cut != null || _jump != null || IsPreparing;
    public NCreature? FormationOrigin { get; set; }
    private CancellationTokenSource? _action;
    private RoleMotionEffects? _primaryEffects;
    private MotionPlacement? _placement;
    private Vector2 _returnOffset;
    private Vector2 _finisherOffset;
    private JsonElement _heldPose;
    internal string RoleId => _document!.RootElement.GetProperty("roleId").GetString()!;
    internal JsonElement SourceMotion(string cue) => _document!.RootElement.GetProperty("motions").GetProperty(cue);
    internal Vector2 FinisherOffset
    {
        get => _finisherOffset;
        set { _finisherOffset = value; _motionBody.Position = -_ground + value; }
    }
    internal void HoldImpact(bool hold) => _heldPose = hold ? _primaryFrame : default;
    // A role owns its visual tail. No detached task, shared actor dictionary or
    // card copy: replacement releases only this presentation.
    private sealed class CutPlayback(MotionRecut edit, string cue, RoleMotionEffects effects,
        CancellationTokenSource cancellation, IReadOnlyList<Creature> targets)
    {
        public readonly MotionRecut Edit = edit;
        public readonly string Cue = cue;
        public readonly RoleMotionEffects Effects = effects;
        public readonly CancellationTokenSource Cancellation = cancellation;
        public readonly CancellationToken Token = cancellation.Token;
        public readonly List<IAudioHandle> Sounds = [];
        public readonly List<SaucerShot> Emissions = [];
        public double LeadRate;
        public readonly IReadOnlyList<Creature> Targets = targets;
        public double Time, EntryTime;
        public int Next;
        public bool HitStarted, LogicDone;
        public bool PhaseHold;
        public readonly SquadPlaySelection? Play = SquadPlaySelection.Current;
        public Vector2 Carry;
        public Vector2 SaucerStart, SaucerDestination;
    }
    private readonly Dictionary<(string Cue, float Seconds), MotionRecut> _edits = [];
    private CutPlayback? _cut;
    private readonly List<IAudioHandle> _sounds = [];
    private readonly List<(IAudioHandle Sound, double Age, double Duration)> _fadingSounds = [];
    private IAudioHandle? _reactionVoice;
    private IAudioHandle? _attackVoice;
    private bool _castVoiced;
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
            _saucerMuzzle = new Marker2D { Name = "EmissionMuzzle", Position = new Vector2(3, -31) };
            _saucer.AddChild(_saucerMuzzle);
            _saucerOrbAnchor = new Marker2D { Name = "OrbAnchor", Position = new Vector2(0, -38) };
            _saucer.AddChild(_saucerOrbAnchor);
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
        AdvancePoseBridge(delta);
        AdvanceMovementGhosts(delta);
        AdvanceSaucerReturn(delta);
        AdvanceSaucerMotion(delta);
        AdvanceSoundTails(delta);
        for (int i = _effectTails.Count - 1; i >= 0; i--)
        {
            var effect = _effectTails[i];
            effect.AdvanceTail(delta);
            if (effect.HasEmissions && !_dead) continue;
            effect.Restore(); effect.QueueFree(); _effectTails.RemoveAt(i);
        }
        if (_cut is { } cut)
        {
            if (_dead || (cut.Token.IsCancellationRequested && (!cut.LogicDone || !MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding)
                && _heldPose.ValueKind != JsonValueKind.Object)) StopCut(cut, true);
            else if (_heldPose.ValueKind != JsonValueKind.Object && !cut.PhaseHold)
            {
                cut.Time += delta * (cut.HitStarted ? 1 : cut.LeadRate);
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
                _hurtOffset = new Vector2(MotionRecut.HurtDisplacement(_hurtTime, _hurt.Duration, _hurtStartX), 0);
                if (_hurtTime >= _hurt.Duration) StopHurt(false);
                if (_hurt != null) { DrawFrame(_hurt.Frames[_hurt.IndexAt(_hurtTime)]); return; }
            }
        }
        if (_heldPose.ValueKind == JsonValueKind.Object)
        {
            DrawFrame(_heldPose, true);
            return;
        }
        if (AdvanceJump(delta)) return;
        if (AdvancePreparation(delta)) return;
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

    internal Node2D CopyPose(Node parent, bool afterimage = true)
    {
        var pose = new Node2D { Name = "DohnaPreviousPose" };
        parent.AddChild(pose);
        foreach (var sprite in _sprites.Values.Where(s => s.Visible).OrderBy(s => _layerOrder[s]))
        {
            var copy = new Sprite2D { Texture = sprite.Texture, Centered = false,
                Offset = sprite.Offset, FlipH = sprite.FlipH, FlipV = sprite.FlipV,
                Material = sprite.Material,
                SelfModulate = afterimage ? new Color(80f / 255, 80f / 255, 1) : sprite.SelfModulate };
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
            _saucer.Position = _saucerPosition + _saucerFloat;
            _saucer.Rotation = _saucerTilt;
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
        BlendPreparedPose();
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
                + (!action && _hurt != null ? _hurtOffset : _returnOffset) + new Vector2(_finisherOffset.X, 0);
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
        if (cue is "strike" or "special" or "cast")
        {
            await PlayCut(cue, hit, cancellation, targets ?? [], cinematic, cue == "cast" ? .25f : .15f);
            return;
        }
        PruneSounds();
        ClearPreparation();
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

    internal Task PlayNative(string trigger, float waitTime, CancellationToken cancellation)
    {
        var play = SquadPlaySelection.Current;
        bool cast = trigger is "Cast" or "PowerUp";
        string cue = cast ? "cast" : SquadMotionTiming.Cue(play?.Card);
        waitTime = cast ? SquadMotionTiming.Cast(play?.Card, trigger == "PowerUp", waitTime)
            : SquadMotionTiming.Attack(play?.Card, waitTime);
        // A second phase of this same native attack (e.g. SweepingBeam's
        // Cast) uses the readied pose, not another summon/holster cycle. It
        // still pays its entire original logical gate.
        if (cast && play?.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack
            && _cut is { HitStarted: true } current && current.Play == play)
            return ContinueAttackPhase(current, waitTime, cancellation);
        var targets = play?.AnimationTargets ?? [];
        if (targets.Count == 0 && play?.Play?.Target is { } target) targets = new[] { target };
        // Some prototypes (SweepingBeam) have an explicit Attack before they
        // construct their command. Its all-enemy intent is already known;
        // resolve visual anchors here without choosing any random target.
        if (targets.Count == 0 && play?.Card is { Type: MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack,
                TargetType: MegaCrit.Sts2.Core.Entities.Cards.TargetType.AllEnemies } areaCard)
            targets = areaCard.CombatState!.HittableEnemies.Where(c => c.IsAlive).ToArray();
        return PlayCut(cue, null, cancellation, targets, false, waitTime);
    }

    private async Task ContinueAttackPhase(CutPlayback state, float seconds, CancellationToken cancellation)
    {
        state.PhaseHold = true;
        try
        {
            await Cmd.CustomScaledWait(Math.Min(seconds * .5f, .25f), seconds, cancellationToken: cancellation);
            await MegaCrit.Sts2.Core.Combat.CombatManager.Instance.WaitForUnpause();
            cancellation.ThrowIfCancellationRequested();
        }
        finally { state.PhaseHold = false; }
    }

    private async Task PlayCut(string cue, Func<Task>? hit, CancellationToken cancellation,
        IReadOnlyList<Creature> targets, bool cinematic, float waitTime)
    {
        cancellation.ThrowIfCancellationRequested();
        var mode = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode;
        double nativeWait = mode == MegaCrit.Sts2.Core.Settings.FastModeType.Instant ? 0
            : mode == MegaCrit.Sts2.Core.Settings.FastModeType.Fast ? Math.Min(waitTime * .5f, .25f) : waitTime;
        var actorNode = GetParent<NCreature>();
        if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is { } combatRoom)
            SquadFormation.Ensure(combatRoom).BeginWindup(actorNode.Entity, (float)nativeWait);
        PruneSounds();
        Vector2? interruptedHurt = _hurt != null ? _hurtOffset : null;
        if (interruptedHurt != null) { BeginPoseBridge(); StopHurt(true); }
        // A subsequent action may take over a visual tail, never an earlier
        // command's windup. This restores the camera before sampling anchors.
        if (RoleFinisher.Current is { Holding: true } previousFinish) previousFinish.Close();
        Vector2 interruptedReturn = _jump?.From != null ? _finisherOffset : Vector2.Zero;
        _jump = null;
        if (interruptedReturn != Vector2.Zero) FinisherOffset = Vector2.Zero;
        if (!_edits.TryGetValue((cue, waitTime), out var edit))
            _edits.Add((cue, waitTime), edit = MotionRecut.Create(RoleId, cue, SourceMotion(cue), waitTime));
        edit = TakePreparation(cue, edit, waitTime);
        RoleFinisher? preview = cinematic ? RoleFinisher.Begin(GetParent<NCreature>().Entity, cue, targets) : null;
        var previous = _cut;
        bool resume = cue != "cast" && previous != null && previous.Cue != "cast" && previous.HitStarted && previous.Targets.SequenceEqual(targets)
            && previous.Time < previous.Edit.Timeline.Starts[^3] &&
            (previous.Cue == cue || _document!.RootElement.GetProperty("roleId").GetString() is "kuma" or "antena");
        var carry = interruptedReturn + (interruptedHurt ?? (previous == null ? Vector2.Zero :
            new Vector2(Number(_primaryFrame, "actionAnchorX"), 0) + _returnOffset));
        var saucerStart = _saucerPosition;
        if (previous != null) StopCut(previous, true);
        _action?.Cancel();
        // A cast/return coroutine must lose ownership before this cut starts;
        // its later finally must not reset the new attack's frame/placement.
        _action = null;
        _playing = false;
        if (GodotObject.IsInstanceValid(_primaryEffects)) { _primaryEffects!.Restore(); _primaryEffects.Hide(); }
        bool instant = mode == MegaCrit.Sts2.Core.Settings.FastModeType.Instant;
        var effects = new RoleMotionEffects();
        _layers.AddChild(effects);
        var state = new CutPlayback(edit, cue, effects,
            CancellationTokenSource.CreateLinkedTokenSource(cancellation), targets);
        if (resume)
        {
            state.Next = edit.ResumeIndex;
            state.Time = edit.Timeline.Starts[state.Next];
        }
        state.EntryTime = state.Time;
        state.LeadRate = nativeWait > 0 ? (edit.Contact - state.EntryTime) / nativeWait : 0;
        state.Carry = carry - new Vector2(Number(edit.Timeline.Frames[state.Next], "actionAnchorX"), 0);
        _cut = state;
        _primaryEffects = effects;
        var nodes = targets.Select(c => c.GetCreatureNode()).OfType<NCreature>().ToArray();
        var motion = _document!.RootElement.GetProperty("motions").GetProperty(cue);
        _returnOffset = Vector2.Zero;
        _placement = cue == "cast" || nodes.Length == 0 ? null : new MotionPlacement(this, nodes,
            motion.GetProperty("metadata").GetProperty("Range").GetInt32(), compact: true);
        state.SaucerStart = saucerStart;
        state.SaucerDestination = SaucerHover;
        if (_saucer != null && _placement != null)
        {
            // A new firing lane replaces this actor's attached visual tail,
            // not a projectile already in flight. An old beam must not sweep
            // through uninvolved enemies while the saucer takes its next shot.
            foreach (var shot in _shots.Where(s => GodotObject.IsInstanceValid(s) && s.Attached && !s.Pending))
            { shot.Hide(); shot.QueueFree(); }
            state.SaucerDestination = ChooseSaucerDestination(SaucerShot.Resolve(SquadPlaySelection.Current?.Card), nodes);
        }
        // Keep existing finisher emissions; ordinary actions use native VFX.
        // The lease is captured for this action, not read from a later card.
        _cinematic = false;
        effects.Initialize(this, targets, edit.Timeline,
            originalEffects: RoleFinisher.Current?.OwnsWindup(actorNode.Entity) == true);
        var playingCard = SquadPlaySelection.Current?.Card;
        bool sweeping = playingCard is DohnaDohna.Cards.SquadCatalogCard { Prototype: "SweepingBeam", IsFallback: false };
        if (_saucer != null && cue != "cast" && !sweeping && (SquadPlaySelection.Current?.PresentedCommand != null || hit != null))
            state.Emissions.AddRange(BeginSaucerShot(targets, (float)nativeWait, playingCard));
        bool resolved = false;
        try
        {
            AdvanceCut(state);
            // Exactly the host TriggerAnim gate. The visual cursor is not a
            // second gameplay clock; compatible continuation keeps this wait.
            await Cmd.CustomScaledWait(Math.Min(waitTime * .5f, .25f), waitTime, cancellationToken: cancellation);
            await MegaCrit.Sts2.Core.Combat.CombatManager.Instance.WaitForUnpause();
            cancellation.ThrowIfCancellationRequested();
            if (_cut == state)
            {
                state.Time = edit.Contact;
                AdvanceCut(state);
            }
            state.HitStarted = true; // Before await: reflection can cause real death.
            if (cue == "cast" && SquadPlaySelection.Current?.Card.Type != MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack && !_castVoiced && !_dead)
            {
                _castVoiced = true; // RoleVisuals is recreated with each combat.
                PlayActionVoice("cast");
            }
            preview?.Impact();
            if (hit != null) { ContactEmissions(targets); await hit(); }
            resolved = true;
            state.LogicDone = true;
        }
        finally
        {
            preview?.CommandComplete();
            if (_cut == state && (!resolved || instant || state.Token.IsCancellationRequested))
                StopCut(state, !resolved || state.Token.IsCancellationRequested, skipVisualTail: instant);
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
        if (_saucer == null || state.Cue == "cast") return;
        var timeline = state.Edit.Timeline;
        // Reach the firing fixture before the first charge/beam, hold still
        // while the emitted beam sweeps, then return during the visual tail.
        double arrival = timeline.Starts[state.Edit.ContactIndex - 1];
        float outbound = MotionTimeline.Ease("EaseInOutCubic",
            (float)((time - state.EntryTime) / Math.Max(arrival - state.EntryTime, 1d / 60)));
        if (!state.HitStarted) _saucerPosition = state.SaucerStart.Lerp(state.SaucerDestination, outbound);
        _saucerSpread = outbound;
    }

    private void StopCut(CutPlayback state, bool canceled, bool skipVisualTail = false)
    {
        if (canceled) state.Cancellation.Cancel();
        if (skipVisualTail || canceled && !state.HitStarted)
            foreach (var shot in state.Emissions.Where(GodotObject.IsInstanceValid)) shot.QueueFree();
        if (GodotObject.IsInstanceValid(state.Effects))
        {
            state.Effects.EndEmission();
            if (!skipVisualTail && state.Effects.HasEmissions && !_dead && IsInsideTree()) _effectTails.Add(state.Effects);
            else { state.Effects.Restore(); state.Effects.QueueFree(); }
        }
        if (_primaryEffects == state.Effects) _primaryEffects = null;
        // Emitted one-shots belong to this role/room, not to the body cursor.
        // Replacing a pose must not truncate a gunshot or its recorded release.
        if (canceled && !state.HitStarted)
            foreach (var sound in state.Sounds) FadeSound(sound, .12);
        state.Cancellation.Dispose();
        if (_cut != state) return;
        _cut = null;
        if (skipVisualTail) { _saucerPosition = SaucerHover; _saucerHold = 0; ClearPoseBridge(); }
        _saucerSpread = 0;
        _placement = null;
        _returnOffset = Vector2.Zero;
        _primaryFrame = default;
        if (!_dead && IsInsideTree() && _hurt == null)
        { if (!skipVisualTail) BeginPoseBridge(); DrawFrame(_idle.Frames[0]); }
        PruneSounds();
    }

    private void ReleaseSounds(IEnumerable<IAudioHandle> sounds)
    {
        foreach (var sound in sounds)
            if (_sounds.Contains(sound)) FadeSound(sound, .12);
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
        _fadingSounds.Clear();
        _reactionVoice = null;
        _attackVoice = null;
    }

    private void FadeSound(IAudioHandle sound, double seconds)
    {
        if (!sound.IsValid || _fadingSounds.Any(f => f.Sound == sound)) return;
        // Not all existing Studio one-shots have a release envelope. Use the
        // installed handle API, so allowFadeOut cannot silently become a cut.
        _fadingSounds.Add((sound, 0, seconds));
    }

    private void AdvanceSoundTails(double delta)
    {
        for (int i = _fadingSounds.Count - 1; i >= 0; i--)
        {
            var (sound, age, duration) = _fadingSounds[i];
            age += delta;
            if (!sound.IsValid || age >= duration)
            {
                if (sound.IsValid) { sound.TryStop(false); sound.Dispose(); }
                _sounds.Remove(sound);
                _fadingSounds.RemoveAt(i);
                continue;
            }
            float fraction = (float)(1 - age / duration);
            if (!sound.TrySetVolume(MathF.Pow(10, SquadAudio.GainDb / 20) * fraction * fraction))
                throw new InvalidOperationException("Role event cannot fade through the installed audio handle.");
            _fadingSounds[i] = (sound, age, duration);
        }
        PruneSounds();
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

    private void StopHurt(bool canceled)
    {
        if (_hurt != null && !canceled) BeginPoseBridge();
        _hurt = null;
        _hurtOffset = Vector2.Zero;
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
            if (_attackVoice == sound) _attackVoice = null;
        }
    }

    private void PlayReactionVoice(string category)
    {
        if (_attackVoice != null)
        {
            FadeSound(_attackVoice, category == "deathVoice" ? .04 : .08);
            _attackVoice = null;
        }
        ulong now = Time.GetTicksMsec();
        if (category == "hurtVoice")
        {
            // AIN30389 uses strict last + 300 < now; death bypasses this gate.
            if (!CanReplayHurtVoice(now, _lastHurtVoice)) return;
            _lastHurtVoice = now;
        }
        if (_reactionVoice != null)
        {
            FadeSound(_reactionVoice, category == "deathVoice" ? .04 : .08);
        }
        var voices = _document!.RootElement.GetProperty(category);
        if (voices.GetArrayLength() == 0) throw new InvalidDataException("Missing role reaction voice: " + category);
        // Presentation-only random selection: never consume the run/card RNG.
        _reactionVoice = SquadAudio.Play(voices[GD.RandRange(0, voices.GetArrayLength() - 1)].GetString()!);
        _sounds.Add(_reactionVoice);
    }

    private static bool CanReplayHurtVoice(ulong now, ulong? last) => last == null || now > last.Value + 300;

    internal void PlayFinisherVoice() => PlayActionVoice("strike");

    private void PlayActionVoice(string cue)
    {
        // The regular attack's authored utterance, independent of special cue
        // selection. It can outlive the body tail but never the role/room.
        var voices = SourceMotion(cue).GetProperty("frames").EnumerateArray()
            .Where(f => MotionTimeline.Enabled(f, "Voice", out _))
            .SelectMany(f => f.GetProperty("Voice").EnumerateObject())
            .Where(p => p.Name.StartsWith("Voice", StringComparison.Ordinal) && p.Value.ValueKind == JsonValueKind.String)
            .Select(p => p.Value.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        if (voices.Length == 0) throw new InvalidDataException("Missing action voice: " + RoleId + "/" + cue);
        if (_attackVoice != null) { FadeSound(_attackVoice, .12); _attackVoice = null; }
        _attackVoice = SquadAudio.PlayInRoom(voices[^1]!);
        _sounds.Add(_attackVoice);
    }

    public Task PlayHurt(CancellationToken token)
    {
        if (_dead) return Task.CompletedTask;
        token.ThrowIfCancellationRequested();
        float previousRecoil = _hurtOffset.X;
        StopHurt(true);
        PlayReactionVoice("hurtVoice");
        var source = _document!.RootElement.GetProperty("motions").GetProperty("hit").GetProperty("frames");
        _hurt = MotionRecut.Hurt(source);
        _hurtTime = 0;
        _hurtStartX = previousRecoil;
        _hurtOffset = new Vector2(previousRecoil, 0);
        _hurtCancellation = token;
        _saucerHurtAge = 0;
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
        _jump = null;
        ClearPreparation();
        if (_cut is { } cut) StopCut(cut, true);
        _action?.Cancel();
        if (GodotObject.IsInstanceValid(_primaryEffects)) _primaryEffects!.Restore();
        foreach (var effect in _effectTails) { effect.Restore(); effect.QueueFree(); }
        _effectTails.Clear();
        StopHurt(true);
        StopSounds();
        _document?.Dispose();
        foreach (var shot in _shots.Where(GodotObject.IsInstanceValid)) shot.QueueFree();
        _shots.Clear();
        foreach (var (pose, _) in _movementGhosts) if (GodotObject.IsInstanceValid(pose)) pose.QueueFree();
        _movementGhosts.Clear();
        base._ExitTree();
    }
}
