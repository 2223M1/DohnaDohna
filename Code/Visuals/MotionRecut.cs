using System.Text.Json;
using System.Text.Json.Nodes;

namespace DohnaDohna.Code.Visuals;

/// <summary>Hand-edited poses from motions.json; never owns damage or a second card state.</summary>
internal sealed class MotionRecut
{
    public MotionTimeline Timeline { get; }
    public int ContactIndex { get; }
    public int ResumeIndex => ContactIndex - 2;
    public double Contact => Timeline.Starts[ContactIndex];

    private MotionRecut(JsonElement[] frames, int contact)
    {
        Timeline = new MotionTimeline(JsonSerializer.SerializeToElement(frames));
        ContactIndex = contact;
    }

    // Anticipation / visible release / recovery. Long travel, projectile tracking
    // and jump-home are cut. This is an STS2 adaptation, not original timing.
    public static MotionRecut Create(string role, string cue, JsonElement motion)
    {
        bool special = cue == "special";
        (int[] Lead, int Contact, int[] Tail, float Step) selection = (role, special) switch
        {
            ("kuma", false) => ([0, 12, 15, 18], 19, [20, 21, 18, 15, 12, 0], 24),
            ("kuma", true) => ([0, 12, 17, 20], 21, [22, 23, 25, 27, 17, 12, 0], 24),
            ("alyce", false) => ([0, 3, 6, 8], 9, [10, 11, 12, 14, 16, 17], 18),
            ("alyce", true) => ([0, 24, 31, 37], 39, [40, 41, 42, 37, 31, 24, 0], 0),
            ("antena", _) => ([11, 25, 33, 39], 40, [42, 43, 46, 49, 51, 54, 59, 66, 71], 0),
            ("tora", false) => ([0, 3, 6, 19], 20, [21, 22, 24, 25, 26, 3, 0], 35),
            ("tora", true) => ([0, 3, 5, 23], 24, [25, 26, 28, 31, 34, 35, 0], 30),
            ("kikuchiyo", false) => ([0, 2, 4, 6], 9, [10, 12, 13, 15, 18, 23, 25], 32),
            ("kikuchiyo", true) => ([0, 2, 14, 17], 23, [24, 25, 27, 30, 34, 35, 36], 38),
            ("medhico", _) => ([0, 4, 10, 17], 19, [20, 18, 12, 6, 3, 0], 22),
            ("joker", false) => ([0, 11, 17, 22], 24, [25, 26, 27, 28, 29, 30], 28),
            ("joker", true) => ([0, 3, 25, 27], 28, [29, 28, 27, 25, 3, 0], 12),
            ("zappa", false) => ([0, 11, 14, 18], 19, [20, 21, 22, 25, 29, 32, 34], 35),
            ("zappa", true) => ([0, 3, 8, 11], 19, [21, 23, 26, 28, 29, 30], 40),
            ("kirakira", false) => ([0, 2, 4, 6], 8, [9, 11, 13, 15, 17, 20, 21], 25),
            ("kirakira", true) => ([0, 1, 2, 4], 6, [7, 4, 2, 1, 0], 0),
            ("porno", false) => ([0, 2, 9, 43], 50, [52, 53, 66, 67, 69, 71, 73], 28),
            ("porno", true) => ([0, 25, 29, 31], 35, [36, 38, 40, 45, 48, 50, 59], 0),
            _ => throw new InvalidDataException($"Missing combat edit: {role}/{cue}")
        };
        var (lead, contact, tail, step) = selection;
        var source = motion.GetProperty("frames").EnumerateArray().ToArray();
        var indices = lead.Append(contact).Concat(tail).ToArray();
        var frames = new JsonElement[indices.Length];
        for (int n = 0; n < indices.Length; n++)
        {
            var frame = JsonNode.Parse(source[indices[n]].GetRawText())!.AsObject();
            if (role == "antena" && frame["CgLayers"] is JsonObject rig)
            {
                // Equipment flies as one rigid fixture. Keep its open pose
                // throughout the flight; the role owns continuous travel and
                // return, instead of replaying the offscreen summon/withdrawal.
                var fixture = source[39].GetProperty("CgLayers").EnumerateObject()
                    .Where(p => p.Value.GetProperty("IsGlobalPosition").GetInt32() != 0).ToArray();
                var composition = new JsonObject();
                foreach (var layer in fixture)
                    composition["saucer-" + layer.Name] = JsonNode.Parse(layer.Value.GetRawText());
                foreach (var layer in rig.Where(p => p.Value!["IsGlobalPosition"]!.GetValue<int>() == 0))
                    composition[layer.Key] = layer.Value!.DeepClone();
                frame["CgLayers"] = composition;
            }
            frame["Frame"] = n < lead.Length ? (n == lead.Length - 1 ? 3 : 2)
                : n == lead.Length ? 5 : Math.Clamp(source[indices[n]].GetProperty("Frame").GetInt32(), 2, 5);
            if (role == "antena" && n < lead.Length - 1) frame["Frame"] = 4;
            float progress = n <= lead.Length ? (float)n / lead.Length
                : 1 - (float)(n - lead.Length) / tail.Length;
            Normalize(frame, step * Math.Clamp(progress, 0, 1), role == "porno" && special,
                bodyLast: special && role is "alyce" or "joker" or "kirakira" or "zappa" or "porno");
            if (n > lead.Length && indices[n] < contact) { frame.Remove("Sound"); frame.Remove("Effects"); }
            frames[n] = JsonSerializer.SerializeToElement(frame);
        }
        if (role == "antena")
        {
            // The source beams last a full second. Play their complete decay
            // inside the shortened firing hold, before source pose 51 retracts
            // the UFO. Charging and firing keep separate visual envelopes.
            var timing = new MotionTimeline(JsonSerializer.SerializeToElement(frames));
            double beamEnd = timing.Starts[9] - 1d / 60;
            frames[3] = LimitEmission(frames[3], special ? .10 : beamEnd - timing.Starts[3]);
            frames[4] = LimitEmission(frames[4], beamEnd - timing.Starts[4]);
        }
        return new MotionRecut(frames, lead.Length);
    }

    private static JsonElement LimitEmission(JsonElement source, double duration)
    {
        var frame = JsonNode.Parse(source.GetRawText())!.AsObject();
        if (frame["Effects"] is JsonObject effects)
            foreach (var effect in effects.Select(p => p.Value!.AsObject()))
            {
                if (effect["impact"]!.GetValue<bool>()) continue;
                double minimumRate = effect["images"]!.AsArray().Count * (200d / 60) / duration;
                effect["TimeScalingPer"] = Math.Max(effect["TimeScalingPer"]!.GetValue<double>(), minimumRate);
            }
        return JsonSerializer.SerializeToElement(frame);
    }

    internal static JsonElement StationaryHurt(JsonElement source)
    {
        var frame = JsonNode.Parse(source.GetRawText())!.AsObject();
        Normalize(frame, 0, false, grounded: true);
        frame["Frame"] = Math.Clamp(source.GetProperty("Frame").GetInt32(), 2, 5);
        frame.Remove("Effects");
        frame.Remove("Sound");
        return JsonSerializer.SerializeToElement(frame);
    }

    internal static MotionTimeline Hurt(JsonElement source, JsonElement idle)
    {
        var frames = source.EnumerateArray().ToArray();
        // The second half of hit already contains jump/landing poses; omitting
        // the separate return cue alone would still show a jump in place.
        var reaction = new[] { 0, 4, 8, 12 }.Select(i => StationaryHurt(frames[i])).Append(idle)
            .Select(f => { var n = JsonNode.Parse(f.GetRawText())!; n["Frame"] = 3; return JsonSerializer.SerializeToElement(n); });
        return new MotionTimeline(JsonSerializer.SerializeToElement(reaction.ToArray()));
    }

    private static void Normalize(JsonObject frame, float x, bool cutRope, bool grounded = false, bool bodyLast = false)
    {
        float anchor = frame["actionAnchorX"]!.GetValue<float>();
        var layers = frame["CgLayers"]?.AsObject();
        if (cutRope && layers != null)
        {
            // Source has two IsShadow rig layers before the actual actor.
            // Keep the actor and its own footprint, not the offscreen rope rig.
            string bodyKey = layers.Last().Key;
            foreach (string key in layers.Select(p => p.Key).Where(k => k != bodyKey).ToArray()) layers.Remove(key);
            var shadows = frame["shadows"]!.AsArray();
            while (shadows.Count > 1) shadows.RemoveAt(0);
        }
        var local = layers?.Select(p => p.Value!.AsObject()).Where(l =>
            l["IsShadow"]!.GetValue<int>() != 0 && l["IsGlobalPosition"]!.GetValue<int>() == 0
            && !l["impact"]!.GetValue<bool>()).ToArray();
        // These authored special actions put a ball, grenade or detached fist
        // before the body. IsShadow describes both; it is not an actor identity.
        var body = bodyLast ? local?.LastOrDefault() : local?.FirstOrDefault();
        if (bodyLast && body != null) anchor = body["PosX"]!.GetValue<float>();
        float y = body?["PosY"]?.GetValue<float>() ?? 0;
        float dy = grounded ? -y : Math.Clamp(y, -45, 15) - y;
        float dx = x - anchor;
        if (layers != null)
            foreach (var key in layers.Select(p => p.Key).ToArray())
            {
                var layer = layers[key]!.AsObject();
                if (cutRope && layer["IsShadow"]!.GetValue<int>() == 0) { layers.Remove(key); continue; }
                if (layer["IsGlobalPosition"]!.GetValue<int>() == 0)
                { layer["PosX"] = layer["PosX"]!.GetValue<float>() + dx; layer["PosY"] = layer["PosY"]!.GetValue<float>() + dy; }
            }
        foreach (var shadow in frame["shadows"]!.AsArray())
        {
            if (shadow!["global"]!.GetValue<bool>()) continue;
            shadow["anchorX"] = shadow["anchorX"]!.GetValue<float>() + dx;
            shadow["origin"]![0] = shadow["origin"]![0]!.GetValue<float>() + dx;
            shadow["elevation"] = grounded ? 0 : Math.Min(45, shadow["elevation"]!.GetValue<float>());
        }
        if (frame["Effects"] is JsonObject effects)
            foreach (var effect in effects.Select(p => p.Value!.AsObject()))
                if (effect["anchorBinding"]!.GetValue<string>() == "actor")
                { effect["PosX"] = effect["PosX"]!.GetValue<float>() + dx; effect["PosY"] = effect["PosY"]!.GetValue<float>() + dy; }
        frame["actionAnchorX"] = x;
        frame["OffsetX"] = 0;
        frame["OffsetY"] = 0;
        frame.Remove("PreviousImages");
    }
}
