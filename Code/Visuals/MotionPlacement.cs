using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DohnaDohna.Code.Visuals;

/// <summary>One rigid transform per authored composition, never per image.</summary>
internal sealed class MotionPlacement
{
    private readonly Vector2 _localDelta, _globalDelta;
    private readonly float _distance;
    private Vector2 _actorOffset;
    private readonly bool _compact;

    public MotionPlacement(RoleVisuals source, IReadOnlyList<NCreature> targets, int authoredRange, bool compact = false)
    {
        _compact = compact;
        // AIN28478: targetOrder = skillRange - playerOrder - 1.
        // AIN33246: opposing front feet are 320 apart, each rear slot adds 100.
        // The motion table's Range is the authoring fixture, not a runtime locator.
        _distance = DistanceFor(authoredRange);
        var aim = targets.Aggregate(Vector2.Zero, (sum, target) => sum + target.Visuals.VfxSpawnPosition.GlobalPosition) / targets.Count;
        var reference = new Vector2(_distance, -160);
        _localDelta = source.GlobalTransform.BasisXformInv(aim - source.OriginalToGlobal(reference, false));
        _globalDelta = source.GlobalTransform.BasisXformInv(aim - source.OriginalToGlobal(reference, true));
    }

    /// <summary>Authored fixture gap, in body-space pixels, for one motion Range.</summary>
    public static float DistanceFor(int authoredRange) => 320 + (authoredRange - 1) * 100;

    public void SetFrame(JsonElement frame) => _actorOffset = ActorOffsetFor(frame.GetProperty("actionAnchorX").GetSingle());

    /// <summary>Rigid actor correction an authored anchor asks for, in body space.</summary>
    public Vector2 ActorOffsetFor(float anchorX) =>
        new(_localDelta.X * Math.Clamp(anchorX / _distance, 0, 1), 0);

    public Vector2 Map(Vector2 point, bool global, bool ground = false)
    {
        // AIN33555: body and emitted source effects share one action offset.
        // Formation equipment (UFO/charge/beam) uses one target-fixture offset.
        // Neither image X/Y nor texture dimensions may distort the composition.
        var offset = global ? _globalDelta : _compact ? Vector2.Zero : _actorOffset;
        if (ground) offset.Y = 0;
        return point + offset;
    }

    public Vector2 Impact(Vector2 point) => point + _localDelta;
}
