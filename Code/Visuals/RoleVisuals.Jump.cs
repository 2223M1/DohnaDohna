using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.Code.Visuals;

public partial class RoleVisuals
{
    private sealed class JumpPlayback(MotionTimeline timeline, double duration, Vector2? from)
    {
        internal readonly MotionTimeline Timeline = timeline;
        internal readonly double Duration = duration;
        internal readonly Vector2? From = from;
        internal double Time;
        internal int Next;
    }
    private JumpPlayback? _jump;
    private readonly List<(Node2D Pose, double Time)> _movementGhosts = [];
    private double _ghostInterval;

    internal void MovementAfterimage(double delta)
    {
        _ghostInterval += delta;
        if (_ghostInterval < .035 || MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is not { } room) return;
        _ghostInterval = 0;
        _movementGhosts.Add((CopyPose(room.CombatVfxContainer), 0));
    }

    private void AdvanceMovementGhosts(double delta)
    {
        for (int i = _movementGhosts.Count - 1; i >= 0; i--)
        {
            var (pose, time) = _movementGhosts[i];
            time += delta;
            if (!GodotObject.IsInstanceValid(pose) || time >= .16)
            {
                if (GodotObject.IsInstanceValid(pose)) pose.QueueFree();
                _movementGhosts.RemoveAt(i);
            }
            else { pose.Modulate = new Color(1, 1, 1, .4f * (1 - (float)time / .16f)); _movementGhosts[i] = (pose, time); }
        }
    }

    internal void BeginFormationJump(double duration) => BeginJump(duration, null);

    internal float? FormationJumpProgress(float fraction) => _jump is { From: null } jump
        ? jump.Timeline.JumpProgress(fraction * jump.Timeline.Duration) : null;

    internal void BeginFinisherReturn(Vector2 from) => BeginJump(MotionTimeline.ReturnDuration(from.X), from);

    private void BeginJump(double duration, Vector2? from)
    {
        if (_dead) return;
        ClearPreparation();
        // An intentional swap/finisher return is a new visual action; the
        // longer full hit recovery must not hide it while formation moves.
        StopHurt(true);
        if (_cut is { LogicDone: true } tail) StopCut(tail, false);
        var frames = SourceMotion("return").GetProperty("frames").EnumerateArray().Select(MotionRecut.JumpFrame).ToArray();
        _jump = new JumpPlayback(new MotionTimeline(JsonSerializer.SerializeToElement(frames)), duration, from);
        BeginPoseBridge();
    }

    private bool AdvanceJump(double delta)
    {
        if (_jump is not { } jump || _dead) return false;
        var mode = SaveManager.Instance.PrefsSave.FastMode;
        jump.Time = mode == FastModeType.Instant ? jump.Duration : jump.Time + delta;
        double t = Math.Min(jump.Timeline.Duration, jump.Time / jump.Duration * jump.Timeline.Duration);
        while (jump.Next < jump.Timeline.Frames.Length && jump.Timeline.Starts[jump.Next] <= t)
            PlayFrameAudio(jump.Timeline.Frames[jump.Next++], [], playVoice: false);
        if (jump.From is { } from) FinisherOffset = from * (1 - jump.Timeline.JumpProgress(t));
        if (jump.Time >= jump.Duration)
        {
            _jump = null;
            if (jump.From != null) FinisherOffset = Vector2.Zero;
            BeginPoseBridge();
            return false;
        }
        _primaryFrame = jump.Timeline.Frames[jump.Timeline.IndexAt(t)];
        DrawFrame(_primaryFrame);
        return true;
    }
}
