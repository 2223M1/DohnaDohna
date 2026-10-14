using System.Text.Json;
using System.Text.Json.Nodes;

namespace DohnaDohna.Code.Visuals;

/// <summary>Hand-edited poses from motions.json; never owns damage or a second card state.</summary>
internal sealed class MotionRecut
{
    public MotionTimeline Timeline { get; }
    public int ContactIndex { get; }
    public int ResumeIndex => Math.Max(0, ContactIndex - 2);
    public double Contact => Timeline.Starts[ContactIndex];
    private readonly int _preparationCount;

    private MotionRecut(JsonElement[] frames, int contact, double releaseSeconds, int? preparationCount = null)
    {
        Timeline = new MotionTimeline(JsonSerializer.SerializeToElement(frames), contact, releaseSeconds);
        ContactIndex = contact;
        _preparationCount = preparationCount ?? contact;
    }

    // The drag cursor is visual, not credit against the next native windup.
    // Keep the exact visible pose, finish the remaining preparation, then join
    // the selected edit. Only this new lead is retimed; recovery is unchanged.
    internal MotionRecut FromPreparation(MotionPreparation preparation, double time, double seconds)
    {
        int index = preparation.Timeline.IndexAt(preparation.SourceTime(time));
        var lead = preparation.Timeline.Frames.Skip(index).ToArray();
        int join = preparation.JoinIndex;
        return new MotionRecut([.. lead, .. Timeline.Frames.Skip(join)],
            lead.Length + ContactIndex - join, seconds);
    }

    internal static MotionPreparation Preparation(string role, string cue, JsonElement motion)
    {
        // The actual edit is the sole pose definition. Preparation is its
        // safe prefix, with travel/events removed; no second frame-index table
        // or second body registration can select a different gesture.
        var edit = Create(role, cue, motion, cue == "cast" ? .25 : .15);
        var frames = edit.Timeline.Frames.Take(edit._preparationCount).Select(p =>
        {
            var frame = JsonNode.Parse(JumpFrame(p).GetRawText())!.AsObject();
            bool registeredGesture = role == "antena" || role == "kuma" && cue != "cast";
            // Already registered by Create; JumpFrame removes only its step.
            if (frame["CgLayers"] is JsonObject layers)
                foreach (string key in layers.Where(p => p.Value!["impact"]!.GetValue<bool>()
                    || p.Value!["IsGlobalPosition"]!.GetValue<int>() != 0).Select(p => p.Key).ToArray()) layers.Remove(key);
            var shadows = frame["shadows"]!.AsArray();
            for (int i = shadows.Count - 1; i >= 0; i--)
                if (shadows[i]!["global"]!.GetValue<bool>() || shadows[i]!["impact"]!.GetValue<bool>()) shadows.RemoveAt(i);
            foreach (string key in new[] { "Effects", "Sound", "Voice", "Damage", "BgLighting", "EnemyMoveFrame", "Camera", "PreviousImages" })
                frame.Remove(key);
            int ticks = cue == "cast" && role != "antena" ? 3 : p.GetProperty("Frame").GetInt32();
            // Preserve intervening drawings, shorten their exposure instead of
            // discarding them. Long anticipation holds belong to the drag, not
            // to every frame on the way there.
            // The source accelerates two copies of this six-drawing rotation
            // and holds the last drawing before punching. A held card needs
            // ONE steady revolution, including its last-to-first boundary.
            frame["Frame"] = role == "zappa" && cue == "strike" ? 3 : registeredGesture
                ? Math.Clamp((int)Math.Round(ticks * (role == "kuma" ? .6 : .75)), 1, 6)
                : Math.Clamp(ticks, 1, 6);
            return JsonSerializer.SerializeToElement(frame);
        }).ToArray();
        // Restore the pre-October-11 ready-and-hold behaviour for everyone
        // else. Only Zappa's actual fist rotation loops, with no easing/hold.
        return new MotionPreparation(new MotionTimeline(JsonSerializer.SerializeToElement(frames)),
            edit._preparationCount, role == "zappa" && cue == "strike");
    }

    // Anticipation / visible release / recovery. Long travel, projectile tracking
    // and jump-home are cut. This is an STS2 adaptation, not original timing.
    public static MotionRecut Create(string role, string cue, JsonElement motion, double releaseSeconds = .15)
    {
        if (cue == "cast") return Cast(role, motion, releaseSeconds);
        bool special = cue == "special";
        static int[] Span(int a, int b) => Enumerable.Range(a, b - a + 1).ToArray();
        (int[] Lead, int Ready, int Contact, int[] Tail, float Step) selection = (role, special) switch
        {
            ("kuma", false) => (Span(14, 18), 5, 19, [20, 21, 18, 17, 16, 15, 14], 0),
            ("kuma", true) => (Span(14, 20), 5, 21, [22, 23, 24, 25, 20, 19, 18, 17, 16, 15, 14], 0),
            ("alyce", false) => (Span(0, 8), 7, 9, Span(10, 17), 18),
            ("alyce", true) => ([0, 1, 2, .. Span(24, 38)], 11, 39, [40, 41, 42, .. Span(24, 38).Reverse(), 2, 1, 0], 0),
            // Complete the authored hand signal/spin in order. The equipment
            // is already present; do not replay the summon or reverse the spin.
            ("antena", _) => (Span(0, 8), 9, 9, [.. Span(10, 14), .. Span(54, 62)], 0),
            ("tora", false) => ([.. Span(0, 6), .. Span(13, 19)], 7, 20, [.. Span(21, 26), 3, 2, 1, 0], 35),
            ("tora", true) => ([.. Span(0, 5), 23], 6, 24, [.. Span(25, 35), 5, 4, 3, 2, 1, 0], 30),
            ("kikuchiyo", false) => (Span(0, 5), 5, 6, Span(7, 25), 32),
            ("kikuchiyo", true) => ([0, 1, 2, .. Span(11, 16)], 7, 17, Span(18, 36), 38),
            ("medhico", _) => ([.. Span(0, 4), .. Span(8, 18)], 8, 19, [20, .. Span(0, 18).Reverse()], 22),
            ("joker", false) => ([0, 1, 2, 3, .. Span(14, 23)], 8, 24, Span(25, 30), 28),
            ("joker", true) => ([0, 1, 2, .. Span(25, 27)], 4, 28, [29, 30, 31, 34, 35, 36, 49, 50, 51, 52], 12),
            ("zappa", false) => ([.. Span(0, 5), .. Span(12, 16), 18], 6, 19, Span(20, 34), 35),
            ("zappa", true) => ([.. Span(0, 8), 10, 11], 9, 23, [24, 25, 26, 27, 28, 29, 30], 40),
            ("kirakira", false) => (Span(0, 7), 5, 8, Span(9, 21), 25),
            ("kirakira", true) => ([0, 1, 2, 3, 4, 5], 1, 6, [.. Span(7, 17), 0], 0),
            ("porno", false) => ([0, 1, 2, .. Span(43, 49)], 3, 50, [51, 52, 53, .. Span(66, 73)], 28),
            ("porno", true) => ([0, .. Span(25, 34)], 6, 35, Span(36, 59), 0),
            _ => throw new InvalidDataException($"Missing combat edit: {role}/{cue}")
        };
        var (lead, ready, contact, tail, step) = selection;
        var source = motion.GetProperty("frames").EnumerateArray().ToArray();
        // Restore intervening recovery drawings, not the omitted travel/jump.
        if (role != "kuma") tail = ContinuousRecovery(tail);
        var indices = lead.Append(contact).Concat(tail).ToArray();
        var frames = new JsonElement[indices.Length];
        for (int n = 0; n < indices.Length; n++)
        {
            var frame = JsonNode.Parse(source[indices[n]].GetRawText())!.AsObject();
            if (role == "antena" && frame["CgLayers"] is JsonObject rig)
            {
                foreach (string key in rig.Where(p => p.Value!["IsGlobalPosition"]!.GetValue<int>() != 0).Select(p => p.Key).ToArray())
                    rig.Remove(key);
                // The persistent equipment and native emission own all shots.
                frame.Remove("Effects");
                frame.Remove("Sound");
            }
            int ticks = source[indices[n]].GetProperty("Frame").GetInt32();
            frame["Frame"] = n < lead.Length ? Math.Max(1, ticks)
                : Math.Clamp((int)Math.Round(ticks * (role == "antena" ? .85 : .8)), 2, 14);
            RebindSlash(frame, role);
            float progress = n <= lead.Length ? (float)n / lead.Length
                : 1 - (float)(n - lead.Length) / tail.Length;
            Normalize(frame, step * Math.Clamp(progress, 0, 1), role == "porno" && special,
                bodyLast: special && role is "alyce" or "joker" or "kirakira" or "zappa" or "porno",
                station: role == "antena" ? 0 : role == "kuma" ? 222 : null);
            if (n > lead.Length && indices[n] < contact) { frame.Remove("Sound"); frame.Remove("Effects"); }
            FilterEffects(frame, role, cast: false);
            frames[n] = JsonSerializer.SerializeToElement(frame);
        }
        return new MotionRecut(frames, lead.Length, releaseSeconds, ready);
    }

    private static MotionRecut Cast(string role, JsonElement motion, double seconds)
    {
        static int[] Span(int a, int b) => Enumerable.Range(a, b - a + 1).ToArray();
        // Selected from the ten source contact sheets. Keep expressive personal
        // gestures; omit enemy hits, airborne return and the old UFO summon.
        (int[] Lead, int Release, int[] Tail) poses = role switch
        {
            "kuma" => ([0, 1, 2, 3, 4, 5, 6, 7], 8, [9, 10, 11, 12, 6, 4, 2, 0]),
            "alyce" => ([0, 1, 3, 5, 7, 9, 10, 12, 15, 17, 19, 20], 21, [.. Span(22, 33), .. Span(0, 9).Reverse()]),
            "antena" => ([0, 1, 2, 3, 4, 5, 6, 7, 8], 9, [10, 11, 12, 13, 14]),
            "tora" => (Span(0, 18), 19, [20, 21, 22, 23, 15, 14, 13, 12, 3, 2, 1, 0]),
            "kikuchiyo" => ([0, 1, 2, 3], 4, [3, 2, 1, 0]),
            "medhico" => ([0, 1, 10, 11, 12, 13], 14, [15, 16, .. Span(19, 25), 13, 12, 11, 10, 1, 0]),
            "joker" => ([0, 1, 2, 3, 4, 5, 6, 7], 8, [9, 10, 11, 12, 13, 7, 5, 3, 1, 0]),
            "zappa" => ([0, 1, 2, 3, 4, 5], 6, [7, 8, 3, 2, 1, 0]),
            "kirakira" => ([0, 1, 2, 3, 4, 5], 6, [7, 5, 3, 1, 0]),
            "porno" => ([0, 24, 29, 33, 34, 35, 37, 38, 39], 40, Span(41, 59)),
            _ => throw new InvalidDataException("Missing cast edit: " + role)
        };
        var source = motion.GetProperty("frames").EnumerateArray().ToArray();
        int[] indices = [.. poses.Lead, poses.Release, .. ContinuousRecovery(poses.Tail)];
        var frames = new JsonElement[indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            var frame = JsonNode.Parse(source[indices[i]].GetRawText())!.AsObject();
            Normalize(frame, 0, role == "porno", grounded: role != "antena", station: role == "antena" ? 0 : null);
            frame.Remove("Damage");
            frame.Remove("Voice");
            if (i > poses.Lead.Length && indices[i] < poses.Release) { frame.Remove("Sound"); frame.Remove("Effects"); }
            FilterEffects(frame, role, cast: true);
            int ticks = source[indices[i]].GetProperty("Frame").GetInt32();
            frame["Frame"] = i < poses.Lead.Length ? Math.Max(1, ticks)
                : Math.Clamp((int)Math.Round(ticks * .85), 3, 15);
            frames[i] = JsonSerializer.SerializeToElement(frame);
        }
        // Restore the old held stance without cutting the intervening release
        // drawings after it. Submission still traverses these inside its gate.
        int ready = role switch { "alyce" => 11, "tora" or "porno" => 7, _ => poses.Lead.Length };
        return new MotionRecut(frames, poses.Lead.Length, seconds, ready);
    }

    private static void RebindSlash(JsonObject frame, string role)
    {
        if (role != "kikuchiyo" || frame["Effects"] is not JsonObject effects) return;
        foreach (var effect in effects.Select(p => p.Value!.AsObject()))
            // Table 33: the non-impact slash is a scar across the dash path,
            // NOT a point attached to the actor now standing beyond the foe.
            if (effect["FirstCgName"]!.GetValue<string>() == "res://DohnaDohna/images/original/e27555d03f45834bc0fe.png")
            {
                effect["anchorBinding"] = "impact";
                effect["PosX"] = 320;
                effect["PosY"] = -160;
                effect["ScalingPer"] = 100;
                effect["contactTrail"] = true;
            }
    }

    private static void FilterEffects(JsonObject frame, string role, bool cast)
    {
        if (frame["Effects"] is JsonObject effects)
            foreach (string key in effects.Where(p => p.Value!["impact"]!.GetValue<bool>()
                || cast && (p.Value!["AssignToTarget"]!.GetValue<int>() != 0
                    || p.Value!["IsGlobalPosition"]!.GetValue<int>() != 0
                    || IsTravelOrStrikeEffect(p.Value!["FirstCgName"]!.GetValue<string>())))
                .Select(p => p.Key).ToArray()) effects.Remove(key);
        // Persistent saucer owns its rig; its protective field is emitted only
        // by the actual native block hook, never by generic Cast/PowerUp.
        if (role == "antena" && frame["CgLayers"] is JsonObject layers)
            foreach (string key in layers.Where(p => p.Value!["IsGlobalPosition"]!.GetValue<int>() != 0)
                .Select(p => p.Key).ToArray()) layers.Remove(key);
    }

    // These source effects are actor-relative, but depict travel/landing or
    // an actual blow, not a generic stationary buff. Their original bindings
    // alone therefore cannot decide whether a cast should retain them. Body
    // trails, Antena's radio pulse and native-recipient protection stay intact.
    private static bool IsTravelOrStrikeEffect(string path) => path is
        "res://DohnaDohna/images/original/e03c168dd731dfc6b754.png" // sand cloud
        or "res://DohnaDohna/images/original/fcf0008ce22fb4924711.png" // travelling dust
        or "res://DohnaDohna/images/original/613dea6b988c8452933c.png" // ground shock ring
        or "res://DohnaDohna/images/original/0cb473500ad8d09f3e0b.png" // landing
        or "res://DohnaDohna/images/original/026d65ac45040ec1564f.png" // muzzle/strike flash
        or "res://DohnaDohna/images/original/2ce2ee42840aef3c6583.png"; // ground punch burst

    // Presentation reach is authored separately from card melee insertion.
    internal static bool IsMelee(string role, string cue) => (role, cue) switch
    {
        ("kuma" or "alyce" or "antena", _) => false,
        ("kirakira" or "joker", "special") => false,
        ("tora" or "kikuchiyo" or "medhico" or "zappa" or "porno" or "kirakira" or "joker", _) => true,
        _ => throw new InvalidDataException("Missing presentation reach: " + role + "/" + cue)
    };

    private static int[] ContinuousRecovery(int[] keys)
    {
        var result = new List<int>();
        foreach (int key in keys)
        {
            if (result.Count > 0 && Math.Abs(key - result[^1]) is > 1 and <= 4)
            {
                int step = Math.Sign(key - result[^1]);
                for (int i = result[^1] + step; i != key; i += step) result.Add(i);
            }
            result.Add(key);
        }
        return result.ToArray();
    }

    private static JsonElement HurtFrame(JsonElement source)
    {
        var frame = JsonNode.Parse(source.GetRawText())!.AsObject();
        // PosX/Y alone are not the actor's feet: these particular sheets also
        // bake knockback into transparent image padding. Import-time body/sole
        // registration removes that travel without reading pixels at runtime.
        var shift = source.GetProperty("compactHurtShift");
        float dx = shift[0].GetSingle(), dy = shift[1].GetSingle();
        foreach (var layer in frame["CgLayers"]!.AsObject().Select(p => p.Value!))
        {
            layer["PosX"] = layer["PosX"]!.GetValue<float>() + dx;
            layer["PosY"] = layer["PosY"]!.GetValue<float>() + dy;
        }
        foreach (var shadow in frame["shadows"]!.AsArray())
        {
            shadow!["anchorX"] = shadow["anchorX"]!.GetValue<float>() + dx;
            shadow["origin"]![0] = shadow["origin"]![0]!.GetValue<float>() + dx;
            shadow["elevation"] = 0;
        }
        frame["actionAnchorX"] = 0;
        frame["OffsetX"] = 0;
        frame["OffsetY"] = 0;
        frame.Remove("PreviousImages");
        frame.Remove("Effects");
        frame.Remove("Sound");
        return JsonSerializer.SerializeToElement(frame);
    }

    internal static MotionTimeline Hurt(JsonElement source)
    {
        // AIN33516 separately invokes the return jump after the hit clip.
        // Keep EVERY hit drawing and its original exposure in forward order,
        // including regaining balance; suppress travel/elevation, not poses.
        // The body-owned recoil below replaces that large authored translation.
        return new MotionTimeline(JsonSerializer.SerializeToElement(source.EnumerateArray().Select(HurtFrame).ToArray()));
    }

    internal static float HurtDisplacement(double time, double duration, float initial = 0)
    {
        const float recoil = -18; // Body space: only a few screen pixels at squad scale.
        const double retreat = .09, settle = .25;
        if (time < retreat)
            return initial + (recoil - initial) * MotionTimeline.Ease("EaseOutCubic", (float)(time / retreat));
        return recoil * (1 - MotionTimeline.Ease("EaseInOutSine", (float)((time - settle) / (duration - settle))));
    }

    internal static JsonElement JumpFrame(JsonElement source)
    {
        var frame = JsonNode.Parse(source.GetRawText())!.AsObject();
        float anchor = frame["actionAnchorX"]!.GetValue<float>();
        if (frame["CgLayers"] is JsonObject layers)
            foreach (var layer in layers.Select(p => p.Value!))
                layer["PosX"] = layer["PosX"]!.GetValue<float>() - anchor;
        foreach (var shadow in frame["shadows"]!.AsArray())
        {
            shadow!["anchorX"] = shadow["anchorX"]!.GetValue<float>() - anchor;
            shadow["origin"]![0] = shadow["origin"]![0]!.GetValue<float>() - anchor;
        }
        frame["actionAnchorX"] = 0;
        frame.Remove("Effects");
        frame.Remove("Voice");
        return JsonSerializer.SerializeToElement(frame);
    }

    private static void Normalize(JsonObject frame, float x, bool cutRope, bool grounded = false, bool bodyLast = false,
        float? station = null)
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
        // For a continuous stationary gesture its per-image registration is
        // meaningful. Remove the common travel station, not each drawing's X.
        if (station is { } fixedStation) anchor = fixedStation;
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

internal sealed record MotionPreparation(MotionTimeline Timeline, int JoinIndex, bool Loop = false)
{
    // A visual-only clock: sampling a held card must not dispatch events. Its
    // current drawing can always be handed to release without waiting a loop.
    internal double SourceTime(double time)
    {
        time = Math.Max(0, time);
        return Loop ? time % Timeline.Duration : Math.Min(time, Timeline.Duration - 1e-7);
    }
}
