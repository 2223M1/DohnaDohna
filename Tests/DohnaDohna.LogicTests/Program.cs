using DohnaDohna.Content;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using System.Text.Json;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
foreach (float zoom in new[] { 1f, 1.35f, 2f, 3f })
foreach (float extent in new[] { 720f, 900f, 1080f, 1920f, 2560f })
foreach (float request in new[] { -10000f, -200f, 0f, 200f, 10000f })
{
    float origin = MotionCameraBounds.ClampOrigin(request, 0, extent, zoom);
    Check(origin <= 0 && extent * zoom + origin >= extent - .001f, "Camera stays inside baseline scene, including extreme shake");
}
Check(RoleDefinition.All.Length == 10, "Ten roles");
Check(RoleDefinition.All.Select(r => r.Color).Distinct().Count() == 10, "Distinct colors");
foreach (var role in RoleDefinition.All) Check(RoleDefinition.Get(role.Id) == role, role.Id);
foreach (var role in RoleDefinition.All)
    Check(role.StrikeMotion == (role.Id switch
    {
        "antena" or "medhico" => "攻击２", "porno" => "攻击３", _ => "攻击１"
    }), "Default-first single-node strike selection: " + role.Id);
var squad = SquadRunState.Create(["kuma", "alyce", "antena", "tora"]);
foreach (var member in squad.Members) Check(member.MaxHp == RoleDefinition.Get(member.RoleId).StartingHp, "Role initial HP");
// The fixed succession fixture deliberately uses 25 HP, not role defaults.
foreach (var member in squad.Members) member.Hp = member.MaxHp = 25;
Check(squad.Front!.RoleId == "tora", "Rightmost is front");
squad.Swap("tora", "kuma");
Check(squad.Front!.RoleId == "kuma", "Direct swap");
Check(squad.Members[0].RoleId == "tora", "No queue rotation on swap");
squad.Front.Hp = 5;
// Exercise target selection between hits, not an alternative production damage pipeline.
foreach (var hit in new[] { 7, 7, 7 })
{
    var front = squad.Front!;
    front.Hp = Math.Max(0, front.Hp - hit);
}
Check(squad.Members.Single(m => m.RoleId == "kuma").Hp == 0, "First role dead");
Check(squad.Front!.RoleId == "antena" && squad.Front.Hp == 11, "No overflow; full remaining hits");
squad.ReviveAtRear("kuma");
Check(squad.Members[0].RoleId == "kuma" && squad.Members[0].Hp == 0, "Rear revival waits for native healing from zero");
Check(squad.Front!.RoleId == "antena", "Revival does not take front");
squad.Validate();
try { SquadRunState.Create(["kuma", "kuma", "antena", "tora"]); throw new Exception("Duplicate accepted"); }
catch (InvalidDataException) { assertions++; }
Check(MotionTimeline.Ease("Jump", .999f) == 0 && MotionTimeline.Ease("Jump", 1) == 1, "Original Jump is a step");
Check(MotionTimeline.Ease("EaseInQuad", .5f) == .0625f, "Original Quad is fourth power");
Check(MotionTimeline.Ease("EaseOutQuad", .5f) == .9375f, "Original reverse Quad");
Check(MotionTimeline.Ease("EaseInOutQuad", .25f) == .03125f &&
    MotionTimeline.Ease("EaseInOutQuad", .75f) == .96875f, "Original Merge of fourth-power easing");
using var motion = JsonDocument.Parse("""
    [{"Frame":6,"EnemyPosition":{"Enable":1,"PosX":0,"EasingX":"Linear"}},
     {"Frame":0},{"Frame":6,"EnemyPosition":{"Enable":1,"PosX":100,"EasingX":"EaseInQuad"}}]
    """);
var timeline = new MotionTimeline(motion.RootElement);
Check(timeline.Duration == .2 && timeline.IndexAt(.1) == 2, "Cumulative duration and zero-length keys");
Check(timeline.Sample("EnemyPosition", "PosX", "EasingX", .05) == 6.25f, "Next key easing");
Check(timeline.Sample("EnemyPosition", "PosX", "EasingX", .3) == 100, "Hold final key");
using var jumpData = JsonDocument.Parse("""
    [{"Frame":6},{"Frame":12,"MoveFrame":{"JumpStart":1}},
     {"Frame":6,"MoveFrame":{"JumpEnd":1}}]
    """);
var jump = new MotionTimeline(jumpData.RootElement);
Check(jump.JumpProgress(0) == 0 && jump.JumpProgress(.2) == .5f && jump.JumpProgress(.4) == 1, "Return only travels between original jump markers");
Check(MotionTimeline.ReturnDuration(0) == .4 && MotionTimeline.ReturnDuration(-500) == .8
    && MotionTimeline.ReturnDuration(1500) == 1.2, "Original return distance timing/clamp");
// Audited against the installed package immediately before action-flow
// (20261011-165628-577/previous-mod), NOT git HEAD's much older edit.
int[] PreviousReady(string role, string cue)
{
    static int[] Range(int start, int end) => Enumerable.Range(start, end - start + 1).ToArray();
    if (cue == "cast") return role switch
    {
        "kuma" or "joker" => Range(0, 7),
        "alyce" => [0, 1, 3, 5, 7, 9, 10, 12, 15, 17, 19],
        "antena" => Range(0, 8), "tora" => Range(0, 6), "kikuchiyo" => Range(0, 3),
        "medhico" => [0, 1, 10, 11, 12, 13], "zappa" or "kirakira" => Range(0, 5),
        "porno" => [0, 24, 29, 33, 34, 35, 37], _ => throw new Exception(role)
    };
    return (role, cue) switch
    {
        ("kuma", _) => Range(14, 18), ("antena", _) => Range(0, 8),
        ("alyce", "strike") or ("tora", "strike") => Range(0, 6),
        ("alyce", _) => [0, 1, 2, .. Range(24, 31)], ("tora", _) => Range(0, 5),
        ("kikuchiyo", "strike") or ("kirakira", "strike") => Range(0, 4),
        ("kikuchiyo", _) => [0, 1, 2, .. Range(11, 14)],
        ("medhico", _) => [.. Range(0, 4), .. Range(8, 10)],
        ("joker", "strike") => [0, 1, 2, 3, .. Range(14, 17)], ("joker", _) => [0, 1, 2, 25],
        ("zappa", "strike") => Range(0, 5), // Deliberate exception: one complete, even revolution.
        ("zappa", _) => Range(0, 8), ("kirakira", _) => [0],
        ("porno", "strike") => [0, 1, 2], ("porno", _) => [0, .. Range(25, 29)],
        _ => throw new Exception(role + cue)
    };
}
foreach (var role in RoleDefinition.All)
{
    using var poses = JsonDocument.Parse(File.ReadAllText($"DohnaDohna/roles/{role.Id}/motions.json"));
    foreach (string cue in new[] { "strike", "special", "cast" })
    foreach (double seconds in cue == "cast" ? new[] { .25, .40, .50 } : new[] { 0d, .15, .20, .35 })
    {
        var original = poses.RootElement.GetProperty("motions").GetProperty(cue);
        var edit = MotionRecut.Create(role.Id, cue, original, seconds);
        var prep = MotionRecut.Preparation(role.Id, cue, original);
        var previousReady = PreviousReady(role.Id, cue);
        Check(prep.Timeline.Frames.Length == previousReady.Length, "Previous preparation length: " + role.Id + cue);
        for (int i = 0; i < previousReady.Length; i++)
        {
            var sourceImages = original.GetProperty("frames")[previousReady[i]].GetProperty("CgLayers").EnumerateObject()
                .Where(p => !p.Value.GetProperty("impact").GetBoolean() && p.Value.GetProperty("IsGlobalPosition").GetInt32() == 0)
                .Select(p => p.Value.GetProperty("CgName").GetString()).ToArray();
            var readyImages = prep.Timeline.Frames[i].GetProperty("CgLayers").EnumerateObject()
                .Select(p => p.Value.GetProperty("CgName").GetString()).ToArray();
            // Porno's special intentionally removes the old detached rope rig.
            Check(readyImages.All(sourceImages.Contains), "Previous authored ready drawings: " + role.Id + cue);
        }
        // A source may have only one genuine pre-release pose. Its transition
        // from idle is owned by RoleVisuals' pose bridge, not a frozen throw arc.
        Check(prep.Timeline.Frames.Length >= 1, "Every playable cue has preparation: " + role.Id + cue);
        foreach (var frame in prep.Timeline.Frames)
        {
            Check(!frame.TryGetProperty("Damage", out _) && !frame.TryGetProperty("Effects", out _)
                && !frame.TryGetProperty("Sound", out _) && !frame.TryGetProperty("Voice", out _), "Silent visual-only preparation");
            Check(frame.GetProperty("actionAnchorX").GetSingle() == 0, "Preparation cannot move formation");
        }
        foreach (double time in prep.Timeline.Starts.Concat(new[] { prep.Timeline.Duration + .13, 5.23, 60.21 }))
        {
            var joined = edit.FromPreparation(prep, time, seconds);
            Check(joined.Timeline.Frames[0].GetRawText() == prep.Timeline.Frames[prep.Timeline.IndexAt(prep.SourceTime(time))].GetRawText(),
                "Commit begins at exact held preparation pose");
            Check(Math.Abs(joined.Contact - seconds) < 1e-8, "Preparation never reduces the next native windup");
            Check(joined.Timeline.Frames.Skip(joined.ContactIndex).Select(f => f.GetRawText())
                .SequenceEqual(edit.Timeline.Frames.Skip(edit.ContactIndex).Select(f => f.GetRawText())), "Preparation retains release/recovery");
        }
        Check(Math.Abs(edit.Contact - seconds) < 1e-8, "Native release boundary: " + role.Id + cue);
        Check(edit.Timeline.Duration > edit.Contact && double.IsFinite(edit.Timeline.Duration), "Natural owned recovery: " + role.Id + cue);
        Check(edit.ResumeIndex >= 0 && edit.ResumeIndex < edit.ContactIndex, "Continuation preserves a release: " + role.Id + cue);
        if (cue == "cast")
        {
            var castEffects = edit.Timeline.Frames.Where(f => f.TryGetProperty("Effects", out _))
                .SelectMany(f => f.GetProperty("Effects").EnumerateObject()).Select(p => p.Value).ToArray();
            Check(castEffects.All(e => !e.GetProperty("impact").GetBoolean()
                && e.GetProperty("AssignToTarget").GetInt32() == 0 && e.GetProperty("IsGlobalPosition").GetInt32() == 0),
                "Stationary cast cannot replay source target hits: " + role.Id);
            Check(castEffects.All(e => e.GetProperty("FirstCgName").GetString() != "res://DohnaDohna/images/original/613dea6b988c8452933c.png"),
                "Stationary cast does not replay ground impact from removed travel: " + role.Id);
            if (role.Id == "antena") Check(castEffects.Any(e => e.GetProperty("FirstCgName").GetString()
                == "res://DohnaDohna/images/original/54634e77a5dbfc4c67d4.png"), "Radio equipment effect is retained, not blanket filtered");
        }
        Check(prep.JoinIndex <= edit.ContactIndex, "Preparation joins its own edit, not another gesture");
        var held = Enumerable.Range(0, 120).Select(n => prep.Timeline.IndexAt(prep.SourceTime(3 + n / 60d))).Distinct().ToArray();
        bool rotation = role.Id == "zappa" && cue == "strike";
        Check(prep.Loop == rotation, "Only the requested fist rotation loops: " + role.Id + cue);
        Check(rotation ? held.Length == 6 : held.Length == 1 && held[0] == prep.Timeline.Frames.Length - 1,
            "Previous ready-and-hold restored except Zappa's single rotation: " + role.Id + cue);
        if (rotation)
        {
            Check(prep.Timeline.Duration == .3 && prep.Timeline.Frames.All(f => f.GetProperty("Frame").GetInt32() == 3),
                "One 300ms revolution, equal exposure including the seam");
            Check(held.Select(i => prep.Timeline.Frames[i].GetProperty("CgLayers").GetRawText()).Distinct().Count() == 6,
                "Six distinct rotation drawings, not two differently timed copies");
            for (int turn = 0; turn < 100; turn++)
            for (int i = 0; i < 6; i++)
                Check(prep.Timeline.IndexAt(prep.SourceTime(turn * .3 + i * .05 + .025)) == i,
                    "Uniform forward loop with no doubled end hold or acceleration");
        }
        foreach (double t in new[] { prep.Timeline.Duration, 3.141, 100000.3 })
            Check(prep.SourceTime(t) >= 0 && prep.SourceTime(t) < prep.Timeline.Duration,
                "Held clock remains bounded and event-free");
        for (int i = 0; i < prep.JoinIndex; i++)
        {
            var readyImages = prep.Timeline.Frames[i].GetProperty("CgLayers").EnumerateObject()
                .Select(p => p.Value.GetProperty("CgName").GetString()).ToArray();
            var executedImages = edit.Timeline.Frames[i].GetProperty("CgLayers").EnumerateObject()
                .Select(p => p.Value.GetProperty("CgName").GetString()).ToArray();
            Check(readyImages.All(executedImages.Contains), "Preparation and release share registered source drawings");
        }
        foreach (var frame in edit.Timeline.Frames)
            Check(Math.Abs(frame.GetProperty("actionAnchorX").GetSingle()) <= 40 && frame.GetProperty("OffsetX").GetSingle() == 0,
                "Short authored step, no jump-home: " + role.Id + cue);
        if (cue == "special" && role.Id is "joker" or "kirakira" or "zappa" or "porno")
            foreach (var frame in edit.Timeline.Frames)
            {
                var body = frame.GetProperty("CgLayers").EnumerateObject().Select(p => p.Value).Last(l =>
                    l.GetProperty("IsShadow").GetInt32() != 0 && l.GetProperty("IsGlobalPosition").GetInt32() == 0
                    && !l.GetProperty("impact").GetBoolean());
                Check(Math.Abs(body.GetProperty("PosX").GetSingle() - frame.GetProperty("actionAnchorX").GetSingle()) < .001,
                    "Equipment cannot become the actor's horizontal anchor: " + role.Id);
                Check(body.GetProperty("PosY").GetSingle() is >= -45 and <= 15,
                    "Body stays above the floor, independently of airborne equipment: " + role.Id);
            }
        if (role.Id == "antena" && cue != "cast")
            for (int i = 0; i < edit.Timeline.Frames.Length; i++)
                if (edit.Timeline.Frames[i].TryGetProperty("Effects", out var emissions))
                    foreach (var emission in emissions.EnumerateObject().Select(p => p.Value).Where(e => !e.GetProperty("impact").GetBoolean()))
                    {
                        double end = edit.Timeline.Starts[i] + emission.GetProperty("images").GetArrayLength()
                            * (200d / 60) / emission.GetProperty("TimeScalingPer").GetDouble();
                        Check(end < edit.Timeline.Starts[9], "Beam finishes before UFO retracts: " + cue);
                        if (cue == "special" && i == 3)
                            Check(end <= edit.Contact + .051, "UFO charge ends with release, not recovery");
                    }
    }
    var originalHurt = poses.RootElement.GetProperty("motions").GetProperty("hit").GetProperty("frames");
    var hurt = MotionRecut.Hurt(originalHurt);
    Check(hurt.Frames.Length == originalHurt.GetArrayLength(), "Every original hit frame retained: " + role.Id);
    Check(hurt.Duration == new MotionTimeline(originalHurt).Duration, "Original hit exposure retained: " + role.Id);
    for (int i = 0; i < hurt.Frames.Length; i++)
    {
        var edited = hurt.Frames[i];
        Check(edited.GetProperty("actionAnchorX").GetSingle() == 0 && edited.GetProperty("OffsetX").GetSingle() == 0,
            "Authored long knockback/return removed: " + role.Id);
        Check(edited.GetProperty("Frame").GetInt32() == originalHurt[i].GetProperty("Frame").GetInt32(), "Hit is not retimed");
        Check(edited.GetProperty("CgLayers").EnumerateObject().Select(p => p.Value.GetProperty("CgName").GetString())
            .SequenceEqual(originalHurt[i].GetProperty("CgLayers").EnumerateObject().Select(p => p.Value.GetProperty("CgName").GetString())),
            "Full hit drawings in forward order, not five-frame ping-pong: " + role.Id);
    }
    Check(MotionRecut.HurtDisplacement(0, hurt.Duration) == 0
        && Math.Abs(MotionRecut.HurtDisplacement(hurt.Duration, hurt.Duration)) < .001, "Recoil begins/ends at own slot");
    foreach (float start in new[] { 0f, -9f, -18f })
    {
        Check(MotionRecut.HurtDisplacement(0, hurt.Duration, start) == start, "Consecutive hits preserve the existing small recoil");
        for (double t = 0; t <= hurt.Duration; t += .01)
            Check(MotionRecut.HurtDisplacement(t, hurt.Duration, start) is >= -18 and <= 0, "No accumulating retreat or return overshoot");
    }
    if (role.Id is "kuma" or "antena")
    {
        foreach (string cue in new[] { "strike", "special" })
        {
            var original = poses.RootElement.GetProperty("motions").GetProperty(cue);
            var prep = MotionRecut.Preparation(role.Id, cue, original);
            int first = role.Id == "kuma" ? 14 : 0;
            int count = role.Id == "kuma" ? 5 : 9;
            Check(prep.Timeline.Frames.Length == count, "Continuous focused preparation");
            for (int i = 0; i < count; i++)
                Check(prep.Timeline.Frames[i].GetProperty("CgLayers").GetRawText().Contains(
                    original.GetProperty("frames")[first + i].GetProperty("CgLayers").EnumerateObject().First().Value.GetProperty("CgName").GetString()!),
                    "Preparation retains every standing gun/turn drawing in order");
            Check(prep.Timeline.Starts[^1] <= (role.Id == "kuma" ? .21 : .32), "Focused preparation is locally sped up, not pose-thinned");
        }
    }
    var jumpFrames = poses.RootElement.GetProperty("motions").GetProperty("return").GetProperty("frames");
    foreach (var frame in jumpFrames.EnumerateArray())
    {
        var edited = MotionRecut.JumpFrame(frame);
        Check(edited.GetProperty("actionAnchorX").GetSingle() == 0, "Jump translation belongs to formation/return: " + role.Id);
        Check(!edited.TryGetProperty("Voice", out _), "Movement cannot replay a cast voice");
    }
}
Console.WriteLine($"PASS {assertions} pure state/timeline assertions (not host combat verification).");
