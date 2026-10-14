using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DohnaDohna.Code.Visuals;

public partial class RoleVisuals
{
    // The green underside aperture in the imported 128x79 fixture, relative
    // to its middle-bottom origin. This is deliberately NOT the orb anchor.
    private Marker2D? _saucerMuzzle;
    private Marker2D? _saucerOrbAnchor;
    private double _saucerClock, _saucerRecoilAge = 10, _saucerHurtAge = 10;
    private Vector2 _saucerFloat, _saucerKick;
    private float _saucerTilt;
    internal Vector2? SaucerMuzzle => _saucerMuzzle != null && !_dead ? _saucerMuzzle.GlobalPosition : null;
    private readonly List<SaucerShot> _shots = [];
    private double _saucerHold;

    private void AdvanceSaucerMotion(double delta)
    {
        if (_saucer == null || _dead || _heldPose.ValueKind == System.Text.Json.JsonValueKind.Object) return;
        _saucerClock += delta;
        _saucerRecoilAge += delta;
        _saucerHurtAge += delta;
        // Once a ray is visible, keep its chosen safe firing lane. Motion
        // resumes from this transform, rather than jumping to an idle phase.
        if (_shots.Any(s => GodotObject.IsInstanceValid(s) && s.Attached && !s.Pending)) return;
        bool charged = IsPreparing || _cut is { HitStarted: false };
        float t = (float)_saucerClock;
        float recoil = MathF.Exp(-12 * (float)_saucerRecoilAge) * MathF.Sin(18 * (float)_saucerRecoilAge);
        float hurt = MathF.Exp(-7 * (float)_saucerHurtAge) * MathF.Sin(22 * (float)_saucerHurtAge);
        var wanted = new Vector2(3 * MathF.Sin(t * 1.1f), 5 * MathF.Sin(t * 1.9f))
            + _saucerKick * recoil + new Vector2(-8, 5) * hurt;
        float tilt = .025f * MathF.Sin(t * 1.35f) + .1f * hurt
            + (charged ? .014f * MathF.Sin(t * 7) : 0);
        float blend = 1 - MathF.Exp(-(float)delta * 18);
        _saucerFloat = _saucerFloat.Lerp(wanted, blend);
        _saucerTilt = Mathf.Lerp(_saucerTilt, tilt, blend);
    }

    internal void SaucerDischarged(Vector2 target)
    {
        if (_saucer == null || _dead) return;
        // Multiple simultaneous AOE pellets refresh one kick, not N impulses.
        _saucerKick = -(_motionBody.ToLocal(target) - _saucerPosition).Normalized() * 18;
        _saucerRecoilAge = 0;
    }

    private Vector2 ChooseSaucerDestination(SaucerShotStyle style, IReadOnlyList<NCreature> targets)
    {
        // Small shots do not take the beam's approach lane. A shot following
        // another action keeps the current fixture and lets its tail return.
        if (style == SaucerShotStyle.SmallCannon) return _saucerPosition;
        var target = targets.Aggregate(Vector2.Zero, (sum, node) => sum + node.VfxSpawnPosition) / targets.Count;
        Vector2 aim = _motionBody.ToLocal(target);
        Vector2 destination = style switch
        {
            SaucerShotStyle.SweepingBeam => new(Math.Min(aim.X - 280, 260), Math.Clamp(aim.Y + 31, -270, -65)),
            SaucerShotStyle.Hyperbeam => new(Math.Min(aim.X - 240, 320), Math.Clamp(aim.Y - 80, -330, -120)),
            SaucerShotStyle.PriestBeam => new(Math.Min(aim.X - 280, 220), Math.Clamp(aim.Y + 10, -260, -80)),
            SaucerShotStyle.Laser => new(Math.Min(aim.X - 230, 210), Math.Clamp(aim.Y - 15, -280, -100)),
            _ => new(Math.Min(aim.X - 200, 190), Math.Clamp(aim.Y - 35, -280, -100))
        };
        if (!SaucerShot.IsBeam(style) || targets.Count != 1) return destination;

        // One victim must read as one victim even with piercing native art.
        // Prefer an overhead Hyperbeam; keep other native angles when their
        // whole ray clears the visible bodies of the uninvolved enemies.
        var obstacles = GetParent<NCreature>().Entity.CombatState!.HittableEnemies
            .Where(c => c != targets[0].Entity).Select(c => c.GetCreatureNode()).OfType<NCreature>()
            .Select(n => n.Visuals.Bounds.GetGlobalRect().Grow(70)).ToArray();
        var muzzleOffset = _saucer!.Transform.BasisXform(_saucerMuzzle!.Position);
        var usual = _motionBody.ToGlobal(destination + muzzleOffset);
        Rect2 targetBounds = targets[0].Visuals.Bounds.GetGlobalRect();
        // Clear the actual top, including native/external body scale and
        // floating targets. A clamp relative to the centre puts the saucer
        // INSIDE tall enemies. Include the fixture below its muzzle as well.
        float clearance = 120 + Math.Abs(_saucer.GlobalScale.Y)
            * Math.Max(0, _saucer.GetRect().End.Y - _saucerMuzzle.Position.Y);
        var overhead = new Vector2(target.X, targetBounds.Position.Y - clearance);
        float rise = Math.Max(120, target.Y - overhead.Y);
        // Muzzle visibility alone is insufficient: leave room for the body,
        // upper orb arc and native muzzle flash below the top HUD.
        var viewport = GetViewportRect();
        var visible = new Rect2(viewport.Position + new Vector2(90, 170), viewport.Size - new Vector2(180, 260));
        var candidates = new List<Vector2>();
        if (style != SaucerShotStyle.Hyperbeam) candidates.Add(usual);
        foreach (float slope in new[] { 0f, -.3f, .3f, -.6f, .6f, -1f, 1f })
            candidates.Add(overhead + new Vector2(rise * slope, 0));
        // If a tall foe leaves no headroom, take a visible upper flank rather
        // than lowering the emitter into its head or moving the game camera.
        float visibleTop = (GetCanvasTransform().AffineInverse() * visible.Position).Y;
        float flankY = Math.Max(visibleTop, overhead.Y);
        candidates.Add(new Vector2(targetBounds.Position.X - clearance, flankY));
        candidates.Add(new Vector2(targetBounds.End.X + clearance, flankY));
        if (style == SaucerShotStyle.Hyperbeam) candidates.Add(usual);
        var best = overhead;
        int fewest = int.MaxValue;
        foreach (var origin in candidates)
        {
            if (!visible.HasPoint(GetCanvasTransform() * origin)
                || targetBounds.Grow(45).HasPoint(origin)) continue;
            var direction = (target - origin).Normalized();
            int intersections = obstacles.Count(rect => BeamCrosses(origin, direction, rect));
            if (intersections >= fewest) continue;
            best = origin;
            fewest = intersections;
            if (intersections == 0) break;
        }
        return _motionBody.ToLocal(best) - muzzleOffset;
    }

    // Ray/rectangle slabs, including the continuation beyond the actual hit.
    // Only display bounds are inspected; no target, RNG or damage is changed.
    private static bool BeamCrosses(Vector2 origin, Vector2 direction, Rect2 rect)
    {
        float enter = 0, leave = 4000;
        for (int axis = 0; axis < 2; axis++)
        {
            if (Math.Abs(direction[axis]) < .0001f)
            {
                if (origin[axis] < rect.Position[axis] || origin[axis] > rect.End[axis]) return false;
                continue;
            }
            float a = (rect.Position[axis] - origin[axis]) / direction[axis];
            float b = (rect.End[axis] - origin[axis]) / direction[axis];
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
            if (leave < enter) return false;
        }
        return true;
    }

    internal void PlaceDeferredSaucer(SaucerShotStyle style, IReadOnlyList<NCreature> targets)
    {
        _saucerPosition = ChooseSaucerDestination(style, targets);
        DrawFrame(_primaryFrame.ValueKind == System.Text.Json.JsonValueKind.Object ? _primaryFrame : _idle.Frames[0], true);
    }

    internal IReadOnlyList<SaucerShot> BeginSaucerShot(IReadOnlyList<Creature> targets, float seconds, CardModel? card = null, bool nativeImpact = false)
    {
        if (SaucerMuzzle == null || targets.Count == 0) return [];
        _shots.RemoveAll(s => !GodotObject.IsInstanceValid(s) || !s.IsInsideTree());
        var style = SaucerShot.Resolve(card);
        bool random = DohnaDohna.Code.Squad.SquadPlaySelection.Current?.PresentedCommand?.IsRandomlyTargeted == true;
        SaucerShot[] emitted = random || style == SaucerShotStyle.SweepingBeam
            ? [SaucerShot.Create(this, targets, seconds, style, nativeImpact, random)]
            : targets.Select(t => SaucerShot.Create(this, [t], seconds, style, nativeImpact)).ToArray();
        _shots.AddRange(emitted);
        _saucerHold = seconds + .3;
        return emitted;
    }

    internal void CancelPendingEmissions(DohnaDohna.Code.Squad.SquadPlaySelection play)
    {
        foreach (var shot in _shots.Where(s => GodotObject.IsInstanceValid(s) && s.Pending && s.Play == play))
        { shot.Hide(); shot.QueueFree(); }
    }

    internal void ContactEmissions(IReadOnlyList<Creature> targets)
    {
        _primaryEffects?.ContactTargets(targets);
        var play = DohnaDohna.Code.Squad.SquadPlaySelection.Current;
        foreach (var shot in _shots.Where(s => GodotObject.IsInstanceValid(s) && !s.IsQueuedForDeletion() && s.Pending && s.Play == play))
            shot.Contact(targets);
    }

    private void AdvanceSaucerReturn(double delta)
    {
        _shots.RemoveAll(s => !GodotObject.IsInstanceValid(s) || !s.IsInsideTree());
        _saucerHold = Math.Max(0, _saucerHold - delta);
        if (_saucer == null || _saucerHold > 0 || _shots.Any(s => s.Attached)
            || _cut is { HitStarted: false } or { PhaseHold: true }) return;
        _saucerPosition = _saucerPosition.Lerp(SaucerHover, 1 - MathF.Exp(-(float)delta * 5));
    }
}
