using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Helpers;
using DohnaDohna.Cards;

namespace DohnaDohna.Code.Visuals;

internal enum SaucerShotStyle { Automatic, SmallCannon, LargeCannon, SweepingBeam, Hyperbeam, Laser, PriestBeam }

// Native art, with a role-owned emission clock. Stock missile factories force
// an origin above the screen; stock beam controllers hard-code their windup.
// Only those visual tracks are adapted. No damage, orb or command is run here.
internal sealed partial class SaucerShot : Node2D
{
    private RoleVisuals _source = null!;
    private Node2D _art = null!;
    private NCreature[] _targets = [];
    private Vector2[] _points = [];
    private Vector2 _origin;
    private double _lead, _time, _contactTime;
    private bool _contact, _ended, _deferredTarget, _launched;
    private SaucerShotStyle _style;
    private bool _nativeImpact;
    private IReadOnlyList<Creature> _intended = [];
    internal readonly DohnaDohna.Code.Squad.SquadPlaySelection? Play = DohnaDohna.Code.Squad.SquadPlaySelection.Current;
    private MegaSprite? _laser;
    private Node2D? _laserTarget;
    private Node2D? _sweepRig;
    private Sprite2D? _sweep;
    private Line2D? _beamLine;
    private Vector2[] _beamPoints = [];
    private float _nativeBeamLength;
    // Alpha-weighted directions of the native 3x2 flipbook, about its left
    // centre pivot. Undo the baked sweep before aiming at real world targets.
    private static readonly float[] SweepAxes = [-1.2121f, -.4464f, .4117f, .9394f, 1.2561f, 1.3006f];
    private readonly List<(GpuParticles2D Node, double Speed)> _particles = [];
    private bool _paused;
    internal bool Pending => !_contact;
    // Particle emission ending is not the end of the visible fade/retraction.
    // Keep the saucer still until that tail is gone; a new action may cancel it.
    internal bool Attached => !IsProjectile && !IsQueuedForDeletion();
    private bool IsProjectile => _style is SaucerShotStyle.SmallCannon or SaucerShotStyle.LargeCannon;
    internal static bool IsBeam(SaucerShotStyle style) => style is SaucerShotStyle.SweepingBeam
        or SaucerShotStyle.Hyperbeam or SaucerShotStyle.Laser or SaucerShotStyle.PriestBeam;

    internal static SaucerShotStyle Resolve(CardModel? card) => card is SquadCardModel { SaucerShot: not SaucerShotStyle.Automatic } edited
        ? edited.SaucerShot : card is SquadCatalogCard { Prototype: "SweepingBeam", IsFallback: false }
        ? SaucerShotStyle.SweepingBeam : SaucerShotStyle.SmallCannon;

    internal static SaucerShot Create(RoleVisuals source, IReadOnlyList<Creature> targets, double lead,
        SaucerShotStyle style, bool nativeImpact, bool deferredTarget = false)
    {
        var shot = new SaucerShot { Name = "DohnaSaucerShot", _source = source, _lead = Math.Max(0, lead),
            _style = style, _nativeImpact = nativeImpact, _deferredTarget = deferredTarget, _intended = targets.ToArray(),
            _targets = targets.Select(t => t.GetCreatureNode()).OfType<NCreature>().ToArray() };
        shot._points = shot._targets.Select(t => t.VfxSpawnPosition).ToArray();
        shot._origin = source.SaucerMuzzle ?? throw new InvalidOperationException("Saucer shot without a living emitter.");
        (NCombatRoom.Instance ?? throw new InvalidOperationException("Saucer shot outside combat room")).CombatVfxContainer.AddChild(shot);
        return shot;
    }

    public override void _Ready()
    {
        if (_style == SaucerShotStyle.Laser)
        {
            _art = Extract("res://scenes/vfx/laser_vfx.tscn", "SpineSprite");
            AddChild(_art);
            _laser = new MegaSprite(_art);
            _laserTarget = _art.GetNode<Node2D>("TargetingBone");
            _nativeBeamLength = 3000; // NCubexConstructVfx.StartLaser, not target distance.
            _art.RunWhenSpineReady(_laser, state => state.SetAnimation("animation"));
        }
        else
        {
            _art = _style switch
            {
                SaucerShotStyle.SmallCannon or SaucerShotStyle.LargeCannon => Extract(
                    _style == SaucerShotStyle.SmallCannon ? NSmallMagicMissileVfx.scenePath : NLargeMagicMissileVfx.scenePath, "projectile_vfx_container"),
                SaucerShotStyle.SweepingBeam => Extract(NSweepingBeamVfx.scenePath, "start"),
                SaucerShotStyle.Hyperbeam => Extract(NHyperbeamVfx.scenePath, "laser"),
                SaucerShotStyle.PriestBeam => Extract("res://scenes/vfx/monsters/kin_priest_beam_vfx.tscn", "BeamHolder"),
                _ => throw new InvalidOperationException("Unresolved saucer shot style")
            };
            AddChild(_art);
            _art.Rotation = 0;
            if (_style == SaucerShotStyle.SweepingBeam)
            {
                // Stock particles bake a perspective floor sweep and use an
                // unrelated particle lifetime. Keep their exact flipbook/LUT,
                // but bind its frame and ray to this command's release gate.
                var native = _art.GetNode<GpuParticles2D>("vfx_sweeping_beam_flipbook");
                _nativeBeamLength = native.Texture.GetWidth() / 3f * ((ParticleProcessMaterial)native.ProcessMaterial).ScaleMax;
                var palette = ((ShaderMaterial)native.Material).GetShaderParameter("lut");
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://DohnaDohna/shaders/saucer_sweep.gdshader") };
                material.SetShaderParameter("lut", palette);
                _sweepRig = new Node2D();
                _sweep = new Sprite2D { Texture = native.Texture, Hframes = 3, Vframes = 2, Material = material,
                    Offset = new Vector2(native.Texture.GetWidth() / 6f, 0) };
                _art.RemoveChild(native); native.Free();
                foreach (var node in _art.GetChildren().OfType<Node2D>()) { node.Position *= .3f; node.Scale *= .3f; }
                _art.AddChild(_sweepRig); _sweepRig.AddChild(_sweep);
            }
            if (_style == SaucerShotStyle.Hyperbeam)
            {
                _beamLine = _art.GetNode<Line2D>("vfx_hyperbeam_laser_line");
                _beamPoints = _beamLine.Points;
                _nativeBeamLength = _beamPoints[^1].X;
                foreach (var node in _art.GetChildren().OfType<Node2D>().Where(n => n is not Line2D)) node.Scale *= .4f;
            }
            if (_style == SaucerShotStyle.PriestBeam)
            {
                var beam = _art.GetNode<Sprite2D>("Beam");
                _nativeBeamLength = beam.Texture.GetWidth() * beam.Scale.X * 4; // NKinPriestBeamVfx.Fire.
            }
            if (IsProjectile) _art.Scale = Vector2.One * (_style == SaucerShotStyle.SmallCannon ? .35f : .7f);
            Particles(_art, true);
        }
        foreach (var particle in _art.FindChildren("*", "GPUParticles2D", true, false).OfType<GpuParticles2D>())
        {
            particle.LocalCoords = true;
            _particles.Add((particle, particle.SpeedScale));
        }
        Place();
    }

    private static Node2D Extract(string path, string child)
    {
        // Reuse the exact native particle/material subtree, not the fixed
        // screen-origin controller. Off-tree extraction cannot start its task.
        var root = GD.Load<PackedScene>(path).Instantiate<Node2D>();
        var art = root.GetNode<Node2D>(child);
        root.RemoveChild(art);
        root.Free();
        art.Position = Vector2.Zero;
        return art;
    }

    private static void Particles(Node node, bool start)
    {
        if (node is GpuParticles2D particle)
        {
            particle.Emitting = start;
            if (start) particle.Restart();
        }
        foreach (var child in node.GetChildren()) Particles(child, start);
    }

    internal void Contact(IReadOnlyList<Creature> actualTargets)
    {
        if (_contact) return;
        actualTargets = actualTargets.Where(_intended.Contains).ToArray();
        if (actualTargets.Count == 0) { QueueFree(); return; }
        _contact = true;
        _contactTime = _time;
        if (actualTargets.Count > 0)
        {
            _targets = actualTargets.Select(t => t.GetCreatureNode()).OfType<NCreature>().ToArray();
            _points = _targets.Select(t => t.VfxSpawnPosition).ToArray();
        }
        // A random command reveals its chosen target only at Damage. Only
        // then can a beam take a safe lane; never preview all RNG candidates.
        if (_deferredTarget && !IsProjectile) _source.PlaceDeferredSaucer(_style, _targets);
        if (!IsProjectile) Particles(_art, true);
        if (!IsProjectile) _source.SaucerDischarged(_points[0]);
        Place();
        // SweepingBeam has no separate WithHitFx. Its original impact factory
        // is emitted at real Damage, not at a guessed fraction of a film.
        if (_nativeImpact && NCombatRoom.Instance is { } room)
            foreach (var point in _points)
            {
                Node2D? impact = _style == SaucerShotStyle.Hyperbeam
                    ? NHyperbeamImpactVfx.Create(_origin, point) : NSweepingBeamImpactVfx.Create(point);
                if (impact != null) room.CombatVfxContainer.AddChild(impact);
            }
        // The real command owns contact audio. Selecting a different beam
        // must not append a second hit sound (or one sound per AOE target).
        if (IsProjectile && !_deferredTarget) { _art.Hide(); _ended = true; }
    }

    public override void _Process(double delta)
    {
        bool paused = CombatManager.Instance.IsPaused;
        if (_paused != paused)
        {
            foreach (var (node, speed) in _particles) node.SpeedScale = paused ? 0 : speed;
            _laser?.GetAnimationState().SetTimeScale(paused ? 0 : 1);
            _paused = paused;
        }
        if (paused) return;
        if (!GodotObject.IsInstanceValid(_source) || !_source.IsInsideTree() || _source.SaucerMuzzle == null)
        { QueueFree(); return; }
        // Lead is the actual native gate (some projectile waits deliberately
        // do not halve in Fast). Contact is always the real Damage command.
        _time += delta;
        Place();
        if (_contact && _time - _contactTime > (_deferredTarget && IsProjectile ? .08 : .28) && !_ended)
        {
            _ended = true;
            Particles(_art, false);
        }
        if (_ended)
        {
            _art.Modulate = new Color(1, 1, 1, Math.Clamp(1 - (float)(_time - _contactTime - .28) / .2f, 0, 1));
            if (_time - _contactTime > .5) QueueFree();
        }
    }

    private void Place()
    {
        if (_points.Length == 0) return;
        // Native random selection happens AFTER the animation gate. Do not
        // consume RNG early or fire at every candidate: reveal a contact
        // burst only when Damage supplies the actual chosen target.
        if (_deferredTarget && !_contact) { _art.Hide(); return; }
        for (int i = 0; i < _targets.Length; i++)
            if (GodotObject.IsInstanceValid(_targets[i]) && _targets[i].IsInsideTree()) _points[i] = _targets[i].VfxSpawnPosition;
        var target = _points.Aggregate(Vector2.Zero, (a, b) => a + b) / _points.Length;
        float progress = _contact ? 1 : (float)Math.Clamp(_time / Math.Max(.001, _lead), 0, 1);
        Vector2 muzzle = _source.SaucerMuzzle ?? _origin;
        if (IsProjectile)
        {
            double launchAt = _lead * .4;
            if (!_contact && _time <= launchAt) _origin = muzzle;
            if (!_launched && !_contact && _time >= launchAt)
            {
                _origin = muzzle;
                _launched = true;
                _source.SaucerDischarged(target);
            }
            _art.Visible = _deferredTarget ? !_ended : !_contact && _time >= launchAt;
            progress = _contact ? 1 : (float)Math.Clamp((_time - launchAt) / Math.Max(.001, _lead - launchAt), 0, 1);
            GlobalPosition = _origin.Lerp(target, progress);
            // Native missile art points up; align the projectile, not its hit.
            _art.GlobalRotation = (target - _origin).Angle() + Mathf.Pi / 2;
        }
        else
        {
            // Native beams release their full ray at contact. Growing a long
            // ray during the approach would visibly cross a foe BEFORE damage.
            // Charge/approach remain on the saucer's own animation track.
            _art.Visible = _contact;
            GlobalPosition = muzzle;
            _origin = muzzle;
            Vector2 direction = target - muzzle;
            // The target is an aim/contact anchor, never the range endpoint.
            // Keep the native reach and still pass the far edge of unusually
            // large/distant targets. This cannot change command target sets.
            float beyond = _targets.Where(GodotObject.IsInstanceValid)
                .Select(t => t.Visuals.Bounds.GetGlobalRect().Size.Length() * .5f).DefaultIfEmpty(0).Max();
            float length = Math.Max(_nativeBeamLength, _points.Max(point => point.DistanceTo(muzzle)) + beyond + 160);
            if (_laser != null)
            {
                float retract = _ended ? Math.Clamp(1 - (float)(_time - _contactTime - .28) / .15f, 0, 1) : 1;
                _laserTarget!.GlobalPosition = muzzle + direction.Normalized() * length * retract;
            }
            else
            {
                _art.Rotation = direction.Angle();
                if (_style == SaucerShotStyle.SweepingBeam)
                {
                    _art.Rotation = 0;
                    int frame = _contact ? Math.Min(5, 2 + (int)((_time - _contactTime) / .085))
                        : Math.Min(2, (int)(progress * 3));
                    _sweep!.Frame = frame;
                    _sweep.Rotation = -SweepAxes[frame];
                    _sweepRig!.Rotation = direction.Angle();
                    _sweepRig.Scale = new Vector2(length / 711.2f, .35f);
                }
                else if (_style == SaucerShotStyle.PriestBeam)
                {
                    // Native priest art points left, including its particle
                    // distribution. Rotate the whole rig, not just the beam.
                    _art.Rotation += Mathf.Pi;
                    var beam = _art.GetNode<Sprite2D>("Beam");
                    beam.Centered = false;
                    beam.Offset = new Vector2(-beam.Texture.GetWidth(), -beam.Texture.GetHeight() * .5f);
                    beam.Scale = new Vector2(length / beam.Texture.GetWidth(), .6f);
                    var sparks = _art.GetNode<GpuParticles2D>("StaticParticles");
                    sparks.Position = new Vector2(-length * .5f, 0);
                    sparks.Scale = new Vector2(length / 362, .6f);
                }
                else if (_style == SaucerShotStyle.Hyperbeam)
                {
                    _beamLine!.Points = _beamPoints.Select(p => p * (length / _nativeBeamLength)).ToArray();
                    _beamLine.Width = 100;
                }
            }
        }
    }
}
