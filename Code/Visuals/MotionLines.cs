using System.Text.Json;
using Godot;

namespace DohnaDohna.Code.Visuals;

/// <summary>Original LineEffect / LineEffectCollection (AIN33399–33416), in 1280×720 space.</summary>
public sealed partial class MotionLines : Node2D
{
    private sealed class Line(Sprite2D sprite)
    {
        public Sprite2D Sprite = sprite;
        public Vector2 Start, End;
        public double Elapsed, Delay, Duration;
        public bool Finished;
    }
    private readonly List<Line> _lines = [];
    private Vector2 _direction;
    private double _elapsed, _duration;
    private float _speed;
    public bool Finished => _elapsed >= _duration && _lines.All(l => l.Finished);

    public static MotionLines Create(float direction, double duration, int strength)
    {
        using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://DohnaDohna/effects.json"));
        var texture = GD.Load<Texture2D>(data.RootElement.GetProperty("lineTexture").GetString()!);
        var result = new MotionLines { _direction = Vector2.Right.Rotated(Mathf.DegToRad(direction)),
            _duration = duration, _speed = 5000 + strength * 50 };
        int count = (int)(10 + strength / 100f * 50);
        for (int i = 0; i < count; i++)
        {
            var sprite = new Sprite2D { Texture = texture, Centered = false,
                Offset = -texture.GetSize() * .5f, RotationDegrees = direction };
            result.AddChild(sprite);
            var line = new Line(sprite);
            result._lines.Add(line);
            result.Restart(line);
        }
        return result;
    }

    private static float Jitter() => .8f + .3f * GD.Randf();
    private void Restart(Line line)
    {
        line.Sprite.Scale = new Vector2(Jitter(), .5f + .5f * GD.Randf());
        float distance = (int)(new Vector2(1280, 720).Length() + line.Sprite.Texture.GetWidth() * line.Sprite.Scale.X * .5f);
        var center = new Vector2(GD.Randf() * 1280, GD.Randf() * 720);
        line.Start = center - _direction * distance;
        line.End = center + _direction * distance;
        line.Duration = (int)(2 * distance / (_speed * Jitter()) * 1000) / 1000.0;
        line.Delay = (int)(line.Duration * 1000 * GD.Randf()) / 1000.0;
        line.Elapsed = 0;
        line.Finished = false;
        line.Sprite.Position = line.Start;
        line.Sprite.Show();
    }

    public void Advance(double delta)
    {
        _elapsed += delta;
        foreach (var line in _lines)
        {
            if (line.Finished) continue;
            line.Elapsed += delta;
            float progress = (float)Math.Clamp((line.Elapsed - line.Delay) / line.Duration, 0, 1);
            line.Sprite.Position = line.Start.Lerp(line.End, progress);
            if (progress < 1) continue;
            line.Finished = true;
            line.Sprite.Hide();
            if (_elapsed < _duration) Restart(line);
        }
    }
}
