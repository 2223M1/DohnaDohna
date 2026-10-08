using System.Text.Json;

namespace DohnaDohna.Code.Visuals;

/// <summary>Original 60 Hz motion time, independent of render frame rate and host damage waits.</summary>
internal sealed class MotionTimeline
{
    public JsonElement[] Frames { get; }
    public double[] Starts { get; }
    public double Duration { get; }
    public MotionTimeline(JsonElement frames)
    {
        Frames = frames.EnumerateArray().ToArray();
        if (Frames.Length == 0) throw new InvalidDataException("Empty original motion.");
        Starts = new double[Frames.Length];
        int ticks = 0;
        for (int i = 0; i < Frames.Length; i++)
        {
            Starts[i] = ticks / 60d;
            int duration = Frames[i].GetProperty("Frame").GetInt32();
            if (duration < 0) throw new InvalidDataException("Negative original frame duration.");
            ticks += duration;
        }
        if (ticks == 0) throw new InvalidDataException("Zero-duration original motion.");
        Duration = ticks / 60d;
    }

    public static float Number(JsonElement value, string key, float fallback = 0) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var number) ? number.GetSingle() : fallback;
    public static bool Enabled(JsonElement frame, string key, out JsonElement value) =>
        frame.TryGetProperty(key, out value) && Number(value, "Enable") != 0;

    public int IndexAt(double time)
    {
        int index = Array.BinarySearch(Starts, time);
        if (index < 0) return Math.Clamp(~index - 1, 0, Frames.Length - 1);
        while (index + 1 < Starts.Length && Starts[index + 1] <= time) index++;
        return index;
    }

    public float JumpProgress(double time)
    {
        double start = 0, end = Duration;
        for (int i = 0; i < Frames.Length; i++)
            if (Frames[i].TryGetProperty("MoveFrame", out var move))
            {
                if (Number(move, "JumpStart") != 0) start = Starts[i];
                if (Number(move, "JumpEnd") != 0) end = Starts[i];
            }
        if (end <= start) throw new InvalidDataException("Invalid original jump interval.");
        return (float)Math.Clamp((time - start) / (end - start), 0, 1);
    }

    public static double ReturnDuration(float distance) => (400 + .8 * Math.Clamp(Math.Abs(distance), 0, 1000)) / 1000;

    // AIN26951–26962: "Quad" is fourth power, "Jump" is a step, not a jump parabola.
    public static float Ease(string name, float progress)
    {
        float t = Math.Clamp(progress, 0, 1);
        return name switch
        {
            "Linear" => t,
            "Jump" => t < 1 ? 0 : 1,
            "EaseIn" => t * t,
            "EaseOut" => 1 - (1 - t) * (1 - t),
            "EaseInCubic" => t * t * t,
            "EaseOutCubic" => 1 - MathF.Pow(1 - t, 3),
            "EaseInQuad" => MathF.Pow(t, 4),
            "EaseOutQuad" => 1 - MathF.Pow(1 - t, 4),
            "EaseInExp" => MathF.Pow(2, 10 * (t - 1)),
            "EaseOutExp" => 1 - MathF.Pow(2, -10 * t),
            "EaseInSine" => 1 - MathF.Cos(t * MathF.PI / 2),
            "EaseOutSine" => MathF.Sin(t * MathF.PI / 2),
            "EaseInOut" => t < .5f ? Ease("EaseIn", t * 2) / 2 : .5f + Ease("EaseOut", (t - .5f) * 2) / 2,
            "EaseInOutCubic" or "EaseInOutQuad" or "EaseInOutExp" or "EaseInOutSine" => t < .5f
                ? Ease("EaseIn" + name[9..], t * 2) / 2
                : .5f + Ease("EaseOut" + name[9..], (t - .5f) * 2) / 2,
            _ => throw new InvalidDataException("Unsupported original easing: " + name)
        };
    }

    // CameraCalculator / GetPositionFromEnemyMoveFrame use the NEXT key's easing.
    public float Sample(string track, string component, string easing, double time, float initial = 0,
        Func<JsonElement, float>? resolve = null)
    {
        float from = initial;
        double start = 0;
        for (int i = 0; i < Frames.Length; i++)
        {
            if (!Enabled(Frames[i], track, out var key)) continue;
            float to = resolve != null ? resolve(key) : Number(key, component, initial);
            if (Starts[i] > time)
            {
                float t = (float)((time - start) / (Starts[i] - start));
                return from + (to - from) * Ease(key.GetProperty(easing).GetString()!, t);
            }
            from = to;
            start = Starts[i];
        }
        return from;
    }
}
