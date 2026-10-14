using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.Code.Visuals;

internal sealed partial class RoleFinisher
{
    private readonly List<Node> _impactContainers = [];
    private readonly Dictionary<Node, double> _impactBirths = [];
    private readonly List<(Node Node, ProcessModeEnum Mode, double Speed)> _frozen = [];
    private readonly HashSet<Node> _captured = [];
    private readonly List<(Node2D Body, Color Before, Color Written)> _flashes = [];
    private readonly AttackCommand? _command = SquadPlaySelection.Current?.PresentedCommand;
    private double _actionTime;

    private static bool Active(Node node) => GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();

    private void WatchImpacts()
    {
        // Listen only on this room/these victims for this exact command.
        // Existing VFX and nested autoplay/reflection are never acquired.
        foreach (var container in _targets.Select(t => t.Entity.GetVfxContainer())
                     .Append(_room.CombatVfxContainer).Append(_room.BackCombatVfxContainer).OfType<Node>().Distinct())
        {
            _impactContainers.Add(container);
            container.ChildEnteredTree += ObserveImpact;
        }
    }

    private void ObserveImpact(Node node)
    {
        if (_released || _command == null || SquadPlaySelection.Current?.PresentedCommand != _command
            || node is not Node2D || node is NDamageNumVfx || node is SaucerShot) return;
        _impactBirths.TryAdd(node, _actionTime);
    }

    private void FreezeImpacts()
    {
        if (_released) return;
        foreach (var (root, born) in _impactBirths)
        {
            if (!Active(root) || _captured.Contains(root)) continue;
            // Pre-existing flights/attacker cues are not hit sparks. Native
            // contact nodes are created just before/inside the real Damage.
            if (_actionTime - born - _time > .12) continue;
            if (!_targets.Where(Active).Any(t => Near(root, t.Visuals.Bounds.GetGlobalRect().Grow(150)))) continue;
            Freeze(root, Math.Max(0, .10 - (_actionTime - born)));
        }
    }

    private static bool Near(Node node, Rect2 region) =>
        node is Node2D art && region.HasPoint(art.GlobalPosition)
        || node.GetChildren().Any(child => Active(child) && Near(child, region));

    private void Freeze(Node node, double warmup)
    {
        if (!Active(node) || !_captured.Add(node)) return;
        double speed = node switch { GpuParticles2D gpu => gpu.SpeedScale, CpuParticles2D cpu => cpu.SpeedScale, _ => 1 };
        _frozen.Add((node, node.ProcessMode, speed));
        if (warmup > 0)
        {
            if (node is GpuParticles2D { Emitting: true, ProcessMaterial: not null } gpu) gpu.RequestParticlesProcess((float)warmup);
            if (node is CpuParticles2D { Emitting: true } cpu) cpu.RequestParticlesProcess((float)warmup);
            if (node is AnimationPlayer player && player.IsPlaying())
                player.Seek(player.CurrentAnimationPosition + warmup, update: true, updateOnly: true);
        }
        node.ProcessMode = ProcessModeEnum.Disabled;
        if (node is GpuParticles2D particles) particles.SpeedScale = 0;
        if (node is CpuParticles2D cpuParticles) cpuParticles.SpeedScale = 0;
        foreach (var child in node.GetChildren()) Freeze(child, warmup);
    }

    private void FlashTargets(float amount)
    {
        for (int i = 0; i < _flashes.Count; i++)
        {
            var (body, before, written) = _flashes[i];
            if (!Active(body) || body.Modulate != written) continue;
            var color = before.Lerp(new Color(1.8f, 1.8f, 1.8f, before.A), amount);
            body.Modulate = color;
            _flashes[i] = (body, before, color);
        }
    }

    private void RestoreImpacts()
    {
        foreach (var (node, mode, speed) in _frozen)
        {
            if (!Active(node)) continue;
            if (node.ProcessMode == ProcessModeEnum.Disabled) node.ProcessMode = mode;
            if (node is GpuParticles2D { SpeedScale: 0 } gpu) gpu.SpeedScale = speed;
            if (node is CpuParticles2D { SpeedScale: 0 } cpu) cpu.SpeedScale = (float)speed;
        }
        foreach (var (body, before, written) in _flashes)
            if (Active(body) && body.Modulate == written) body.Modulate = before;
        _flashes.Clear();
        _frozen.Clear();
        _captured.Clear();
    }

    private void UnwatchImpacts()
    {
        foreach (var container in _impactContainers.Where(GodotObject.IsInstanceValid)) container.ChildEnteredTree -= ObserveImpact;
        _impactContainers.Clear();
        _impactBirths.Clear();
    }
}
