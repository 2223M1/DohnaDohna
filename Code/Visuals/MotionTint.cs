using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DohnaDohna.Code.Visuals;

/// <summary>Temporary original-game color operation on one target's current body.</summary>
internal sealed class MotionTint(NCreatureVisuals visuals) : IDisposable
{
    private readonly Node2D _body = visuals.GetCurrentBody();
    private readonly ShaderMaterial _material = new()
    { Shader = GD.Load<Shader>("res://DohnaDohna/shaders/motion_tint.gdshader") };
    private Material? _previous;
    private bool _installed;
    private readonly bool IsSpine = visuals.SpineBody != null && !visuals.IsUsingPhobiaModeBody;
    private Material? Current => IsSpine ? visuals.SpineBody!.GetNormalMaterial() : _body.Material;

    private void Set(Material? material)
    {
        if (IsSpine) visuals.SpineBody!.SetNormalMaterial(material!);
        else _body.Material = material;
    }

    public void Apply(Color color)
    {
        if (!GodotObject.IsInstanceValid(_body)) return;
        if (color.A <= 0) { Restore(); return; }
        // If native liquid overlays or another owner replaced the material,
        // preserve that new baseline rather than later restoring a stale one.
        if (!_installed || Current != _material)
        {
            _previous = Current;
            Set(_material);
            _installed = true;
        }
        _material.SetShaderParameter("motion_tint", color);
    }

    private void Restore()
    {
        if (_installed && GodotObject.IsInstanceValid(_body) && Current == _material) Set(_previous);
        _installed = false;
        _previous = null;
    }

    public void Dispose() { Restore(); _material.Dispose(); }
}
