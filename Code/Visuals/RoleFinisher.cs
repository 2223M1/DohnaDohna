using System.Text.Json;
using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.Code.Visuals;

// A room-owned visual tail, not a death/HP protection system. Its completion is
// never awaited by Card.OnPlay, AttackCommand or any gameplay hook.
internal sealed partial class RoleFinisher : Node
{
    private const string NodeName = "DohnaFinisher";
    // NinjaSlayer production B: reference frames at 30000/1001 fps, played 2x.
    private const double Frame = 1001d / 60000;
    private const double ZoomEnd = 4 * Frame, ReturnStart = 26 * Frame, ReleaseAt = 27 * Frame, EndAt = 31 * Frame;
    private readonly List<(CanvasItem Item, Color Before, Color Written)> _hidden = [];
    private readonly List<RoleDoomHold> _holds = [];
    private RoleVisuals _source = null!;
    private NCreature[] _targets = [];
    private NCombatRoom _room = null!;
    private MotionCamera _camera = null!;
    private ColorRect _backdrop = null!;
    private Control? _shakeSink;
    private Vector2 _approach;
    private float _contactGap;
    private bool _melee, _impact, _released, _closed, _suspendedShake, _returnTransferred;
    private double _time;
    private float _shakeAngle;
    private readonly TaskCompletionSource _release = new();
    private readonly SquadPlaySelection? _play = SquadPlaySelection.Current;
    internal Creature Actor { get; private set; } = null!;
    internal Task ReleaseTask => _release.Task;
    internal bool Holding => _impact && !_released;
    internal bool OwnsWindup(Creature actor) => !_closed && !_impact && Actor == actor
        && ReferenceEquals(_play, SquadPlaySelection.Current);

    internal static RoleFinisher? Current => NCombatRoom.Instance?.GetNodeOrNull<RoleFinisher>(NodeName);

    internal static RoleFinisher? Begin(Creature actor, string cue, IReadOnlyList<Creature> targets)
    {
        Current?.Close();
        if (targets.Count == 0) return null;
        if (actor.GetCreatureNode()?.Visuals is not RoleVisuals source || NCombatRoom.Instance is not { } room) return null;
        var nodes = targets.Select(t => t.GetCreatureNode()).OfType<NCreature>().ToArray();
        if (nodes.Length == 0) return null; // Native headless path.
        var stage = new RoleFinisher { Name = NodeName, Actor = actor, _source = source, _room = room,
            _targets = nodes, _melee = MotionRecut.IsMelee(source.RoleId, cue) };
        room.AddChild(stage);
        try { stage.Initialize(cue); }
        catch { stage.Close(); throw; }
        return stage;
    }

    private void Initialize(string cue)
    {
        _camera = MotionCamera.Create(_room);
        _backdrop = new ColorRect { Name = "DohnaFinisherBackdrop", MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.Transparent };
        _room.BackCombatVfxContainer.AddChild(_backdrop);
        _backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var motion = _source.SourceMotion(cue);
        var original = new MotionTimeline(motion.GetProperty("frames"));
        int contact = motion.GetProperty("positiveHits")[0].GetInt32();
        double contactTime = original.Starts[contact];
        WatchImpacts();
        if (_melee)
        {
            // The author's target fixture and contact pose define reach. A
            // weapon or cape changing the picture bounds must not move the actor.
            var frame = original.Frames[contact];
            float authoredAnchor = frame.GetProperty("actionAnchorX").GetSingle();
            if (cue == "special" && _source.RoleId is "zappa" or "porno")
                authoredAnchor = frame.GetProperty("CgLayers").EnumerateObject().Select(p => p.Value)
                    .Last(l => l.GetProperty("IsShadow").GetInt32() != 0 && l.GetProperty("IsGlobalPosition").GetInt32() == 0)
                    .GetProperty("PosX").GetSingle();
            var edit = MotionRecut.Create(_source.RoleId, cue, motion);
            float editedAnchor = edit.Timeline.Frames[edit.ContactIndex].GetProperty("actionAnchorX").GetSingle();
            float distance = MotionPlacement.DistanceFor(motion.GetProperty("metadata").GetProperty("Range").GetInt32());
            // DamageFrameController mirrors the source's target-side X.
            // In throws the enemy travels with the attacker; omitting this
            // displacement placed the actor several hundred pixels past it.
            float targetMove = original.Sample("EnemyPosition", "PosX", "EasingX", contactTime);
            _contactGap = Math.Abs(distance - targetMove - authoredAnchor) + editedAnchor;
            PlaceApproach();
        }
    }

    private void PlaceApproach()
    {
        if (!_melee || _released || !GodotObject.IsInstanceValid(_targets[0])) return;
        // Insertion can still be moving the real slot during this windup.
        // Keep the visual contact distance stable without touching that slot.
        Vector2 stand = _source.OriginalToGlobal(new Vector2(_contactGap, 0), false)
            - _source.GlobalTransform.BasisXform(_source.FinisherOffset);
        _approach = _source.GlobalTransform.BasisXformInv(new Vector2(_targets[0].GetBottomOfHitbox().X - stand.X, 0));
        _source.FinisherOffset = _approach;
    }

    internal void Impact()
    {
        if (_closed || _impact) return;
        PlaceApproach();
        _impact = true;
        _source.HoldImpact(true);
        _source.PlayFinisherVoice();
        if (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant) { Close(); return; }
        _shakeAngle = (float)GD.RandRange(0, Math.Tau); // Visual randomness, never the run RNG.
        var game = NGame.Instance;
        if (game != null && ReferenceEquals(game.ScreenshakeTarget, _room.SceneContainer))
        {
            // Native hit commands still request their normal shake. Route only
            // this short lease to an invisible control, so there is no second
            // camera shake and the native API always has a valid target.
            game.ClearScreenShakeTarget();
            _shakeSink = new Control { Name = "DohnaFinisherShakeSink", MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_shakeSink);
            game.SetScreenShakeTarget(_shakeSink);
            _suspendedShake = true;
        }
        foreach (var target in _targets.Where(GodotObject.IsInstanceValid))
        {
            _holds.Add(RoleDoomHold.Attach(target, this));
            var body = target.Visuals.GetCurrentBody();
            _flashes.Add((body, body.Modulate, body.Modulate));
        }
        foreach (var node in _room.CreatureNodes.Where(n => n.Entity != Actor && !_targets.Contains(n)))
        {
            Hide(node.Visuals.GetCurrentBody());
            Hide(node.GetNode<Control>("%HealthBar"));
            Hide(node.IntentContainer);
        }
        ApplyFrame();
    }

    private void Hide(CanvasItem item)
    {
        Color before = item.Modulate, written = new(before.R, before.G, before.B, 0);
        _hidden.Add((item, before, written));
        item.Modulate = written;
    }

    public override void _Process(double delta)
    {
        if (_closed || CombatManager.Instance.IsPaused) return;
        if (!GodotObject.IsInstanceValid(_source) || !_source.IsInsideTree() || Actor.IsDead || NCombatRoom.Instance != _room)
        { Close(); return; }
        PlaceApproach();
        _actionTime += delta;
        if (!_impact) return;
        _time += delta;
        FreezeImpacts();
        if (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant) { Close(); return; }
        if (_time >= ReleaseAt) ReleasePose();
        if (_time >= EndAt) { Close(); return; }
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        float zoomIn = Mathf.Clamp((float)(_time / ZoomEnd), 0, 1);
        float zoom = 1 / ((1 - zoomIn) + zoomIn / 2.12f);
        float back = Mathf.Clamp((float)((_time - ReturnStart) / (EndAt - ReturnStart)), 0, 1);
        back = 1 - MathF.Pow(1 - back, 3);
        zoom = Mathf.Lerp(zoom, 1, back);
        Rect2 subjects = _targets[0].Visuals.Bounds.GetGlobalRect();
        foreach (var target in _targets.Skip(1).Where(GodotObject.IsInstanceValid))
            subjects = subjects.Merge(target.Visuals.Bounds.GetGlobalRect());
        if (_melee) subjects = subjects.Merge(_source.PoseBounds());
        var center = GetViewport().GetVisibleRect().GetCenter();
        float intensity = NScreenshakePaginator.GetShakeMultiplier(SaveManager.Instance.PrefsSave.ScreenShakeOptionIndex);
        float envelope = _time <= ReleaseAt ? 1
            : MotionTimeline.Ease("EaseOutCubic", (float)((EndAt - _time) / (EndAt - ReleaseAt)));
        var shake = Vector2.Right.Rotated(_shakeAngle) * (80 * intensity * MathF.Cos((.3f - (float)_time) * 60) * envelope);
        _camera.Apply(zoom, center - subjects.GetCenter() * zoom + shake);
        _backdrop.Color = new Color(.025f, .002f, .006f, .55f * (1 - back));
        FlashTargets(Math.Clamp(1 - (float)_time / .04f, 0, 1));
    }

    private void ReleasePose(bool allowReturn = true)
    {
        if (_released) return;
        _released = true;
        if (GodotObject.IsInstanceValid(_source)) _source.HoldImpact(false);
        foreach (var hold in _holds.Where(GodotObject.IsInstanceValid)) hold.Release();
        _holds.Clear();
        RestoreImpacts();
        _release.TrySetResult();
        if (allowReturn && _impact && _melee && GodotObject.IsInstanceValid(_source) && Actor.IsAlive)
        {
            _returnTransferred = true;
            _source.BeginFinisherReturn(_approach);
        }
    }

    internal void Close(bool exiting = false)
    {
        if (_closed) return;
        _closed = true;
        ReleasePose(allowReturn: !exiting);
        UnwatchImpacts();
        if (!_returnTransferred && GodotObject.IsInstanceValid(_source)) _source.FinisherOffset = Vector2.Zero;
        foreach (var (item, before, written) in _hidden)
            if (GodotObject.IsInstanceValid(item) && item.Modulate == written) item.Modulate = before;
        _hidden.Clear();
        if (GodotObject.IsInstanceValid(_backdrop)) _backdrop.QueueFree();
        if (GodotObject.IsInstanceValid(_camera)) _camera.Release();
        if (_suspendedShake && NGame.Instance is { } game && game.ScreenshakeTarget == _shakeSink)
        {
            game.ClearScreenShakeTarget();
            if (NCombatRoom.Instance == _room) game.SetScreenShakeTarget(_room.SceneContainer);
        }
        if (!exiting) { GetParent()?.RemoveChild(this); QueueFree(); }
    }

    public override void _ExitTree() => Close(exiting: true);

    internal void CommandComplete()
    {
        if (!_impact) Close();
    }
}

// The host already supports death-node retention. No game state is kept alive
// for the pose, and the native StartDeathAnim/AfterDeath still run on time.
internal sealed partial class RoleDoomHold : Node, IDeathDelayer
{
    private NCreature _creature = null!;
    private RoleFinisher _stage = null!;
    private bool _hasHurt;
    private float _heldTime;
    private string? _deathAnimation;
    private bool _deathStarted, _released;
    private Node2D? _body;
    private ProcessModeEnum _process;

    internal static RoleDoomHold Attach(NCreature creature, RoleFinisher stage)
    {
        var hold = new RoleDoomHold { Name = "DohnaDoomHold", _creature = creature, _stage = stage };
        creature.AddChild(hold);
        hold.Freeze();
        return hold;
    }

    internal void DeathStarted()
    {
        _deathStarted = true;
        if (_released || !_creature.SpineAnimation.IsValid || !_hasHurt) return;
        // The animator is already in its real Dead state, so its Hit trigger
        // deliberately cannot transition back. Override only the visible track;
        // leave StartDeathAnim's task, duration, sound and all death hooks intact.
        _deathAnimation = _creature.SpineAnimation.GetCurrentAnimationName();
        SetTrack("hurt");
        FreezeTrack();
    }
    public Task GetDelayTask() => _stage.ReleaseTask;

    private void Freeze()
    {
        if (_released) return;
        if (!_creature.SpineAnimation.IsValid)
        {
            if (_body == null) { _body = _creature.Visuals.GetCurrentBody(); _process = _body.ProcessMode; }
            _body.ProcessMode = ProcessModeEnum.Disabled;
            return;
        }
        _creature.SetAnimationTrigger("Hit");
        FreezeTrack();
    }

    internal void HitTriggered() { if (!_released) FreezeTrack(); }

    private void FreezeTrack()
    {
        var track = _creature.SpineAnimation.GetCurrentTrack();
        using GodotObject? bound = track?.BoundObject;
        if (track?.GetAnimationName() != "hurt") return;
        _hasHurt = true;
#if DOHNADOHNA_STABLE
        _heldTime = .1f;
#else
        _heldTime = _creature.Entity.Monster?.HurtAnimationTrackOffsetForDoom ?? .1f;
#endif
        track.SetMixDuration(0);
        track.SetTrackTime(_heldTime);
        track.SetTimeScale(0);
    }

    private void SetTrack(string name)
    {
#if DOHNADOHNA_STABLE
        using GodotObject? lease = _creature.SpineAnimation.SetAnimation(name, false)?.BoundObject;
#else
        _creature.SpineAnimation.SetAnimation(name, false);
#endif
    }

    internal void Release()
    {
        if (_released) return;
        _released = true;
        if (GodotObject.IsInstanceValid(_creature) && _creature.SpineAnimation.IsValid)
        {
            var track = _creature.SpineAnimation.GetCurrentTrack();
            using GodotObject? bound = track?.BoundObject;
            // Each GetCurrentTrack mints a new managed/native wrapper; its
            // object ID is not the underlying track's identity. Restore only
            // the exact frozen pose, never another feature's replacement.
            if (_hasHurt && track?.GetAnimationName() == "hurt" && Math.Abs(track.GetTrackTime() - _heldTime) < .00001f)
            {
                track.SetTimeScale(1);
                if (_deathStarted && _deathAnimation != null) SetTrack(_deathAnimation);
            }
        }
        if (GodotObject.IsInstanceValid(_body) && _body!.ProcessMode == ProcessModeEnum.Disabled) _body.ProcessMode = _process;
        QueueFree();
    }

    public override void _ExitTree() => Release();
}
