using System.Text.Json;
using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace DohnaDohna.Code.Visuals;

public partial class RoleVisuals
{
    // The role owns the pose; NCardPlay owns only the input lease. Submission
    // transfers that lease until the native command consumes it or cancels it.
    private sealed class Preparation(Node owner, CardModel card, string cue, MotionPreparation motion)
    {
        public Node Owner = owner;
        public CardModel Card = card;
        public readonly string Cue = cue;
        public readonly MotionPreparation Motion = motion;
        public double Time, ReturnRate, Age;
        public Vector2 SaucerStart;
        public bool Visible, Pending, Returning;
    }

    private readonly Dictionary<string, MotionPreparation> _preparations = [];
    private Preparation? _preparation;
    private Node2D? _poseBridge;
    private double _poseBridgeTime;
    internal bool IsPreparing => _preparation is { Visible: true };

    internal void Prepare(Node input, CardModel card)
    {
        if (_dead) return;
        string cue = card.Type == CardType.Attack ? SquadMotionTiming.Cue(card) : "cast";
        if (_preparation is { } current && current.Cue == cue)
        {
            current.Owner = input;
            current.Card = card;
            current.Pending = current.Returning = false;
            return;
        }
        if (_preparation is { Visible: true }) BeginPoseBridge();
        if (!_preparations.TryGetValue(cue, out var motion))
            _preparations.Add(cue, motion = MotionRecut.Preparation(RoleId, cue, SourceMotion(cue)));
        _preparation = new Preparation(input, card, cue, motion);
    }

    internal void EndPreparation(Node input, bool submitted)
    {
        if (_preparation is not { } state || state.Owner != input) return;
        if (submitted) state.Pending = true;
        else ReturnPreparation(state);
    }

    internal void CancelPreparation(CardModel card)
    {
        if (_preparation is { Pending: true } state && state.Card == card) ReturnPreparation(state);
    }

    private void ReturnPreparation(Preparation state)
    {
        if (state.Returning) return;
        if (!state.Visible) { _preparation = null; return; }
        state.Pending = false;
        state.Time = state.Motion.SourceTime(state.Time);
        state.Returning = true;
        state.ReturnRate = Math.Max(1, state.Time / .3);
    }

    private MotionRecut TakePreparation(string cue, MotionRecut edit, float waitTime)
    {
        if (_preparation is not { } state) return edit;
        if (state.Card == SquadPlaySelection.Current?.Card)
        {
            _preparation = null;
            if (state.Visible && state.Cue == cue)
                return edit.FromPreparation(state.Motion, state.Time, waitTime);
        }
        else if (state.Returning) _preparation = null;
        else state.Visible = false; // Older queued commands take visual priority.
        return edit;
    }

    private bool AdvancePreparation(double delta)
    {
        if (_preparation is not { } state || _dead || _playing || _cut is { LogicDone: false }
            || _heldPose.ValueKind == JsonValueKind.Object || _hurt != null) return false;
        if (!state.Visible)
        {
            BeginPoseBridge();
            state.SaucerStart = _saucerPosition;
            if (_cut is { } tail)
            {
                if (tail.HitStarted && tail.Cue == state.Cue && tail.Time < tail.Edit.Timeline.Starts[^3])
                    state.Time = state.Motion.Timeline.Starts[^1]; // Already readied; don't re-holster just to aim again.
                StopCut(tail, true);
            }
            state.Visible = true;
        }
        state.Age += delta;
        // The next card readies the person; it does not recall a still-firing UFO.
        if (state.Returning)
        {
            state.Time = Math.Max(0, state.Time - delta * state.ReturnRate);
            if (state.Time <= 0)
            {
                BeginPoseBridge();
                _preparation = null;
                _primaryFrame = default;
                return false;
            }
        }
        else state.Time += delta;
        double sourceTime = state.Returning ? state.Time : state.Motion.SourceTime(state.Time);
        _primaryFrame = state.Motion.Timeline.Frames[state.Motion.Timeline.IndexAt(sourceTime)];
        DrawFrame(_primaryFrame);
        return true;
    }

    private void BeginPoseBridge()
    {
        ClearPoseBridge();
        if (_primaryFrame.ValueKind != JsonValueKind.Object && _sprites.Count == 0) return;
        _poseBridge = CopyPose(_motionBody, afterimage: false);
        _poseBridgeTime = 0;
    }

    private void AdvancePoseBridge(double delta)
    {
        if (_poseBridge == null) return;
        _poseBridgeTime += delta;
        if (_poseBridgeTime >= .08) { ClearPoseBridge(); return; }
        _poseBridge.Modulate = new Color(1, 1, 1, 1 - (float)(_poseBridgeTime / .08));
    }

    private void BlendPreparedPose()
    {
        if (_poseBridge == null) return;
        float blend = (float)Math.Clamp(_poseBridgeTime / .08, 0, 1);
        foreach (var sprite in _sprites.Values.Where(s => s.Visible))
        {
            var color = sprite.SelfModulate;
            color.A *= blend;
            sprite.SelfModulate = color;
        }
    }

    private void ClearPoseBridge()
    {
        if (GodotObject.IsInstanceValid(_poseBridge)) { _poseBridge!.Hide(); _poseBridge.QueueFree(); }
        _poseBridge = null;
    }

    private void ClearPreparation()
    {
        _preparation = null;
        ClearPoseBridge();
    }
}
