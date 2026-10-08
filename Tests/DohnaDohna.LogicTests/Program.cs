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
foreach (var role in RoleDefinition.All)
{
    using var poses = JsonDocument.Parse(File.ReadAllText($"DohnaDohna/roles/{role.Id}/motions.json"));
    foreach (string cue in new[] { "strike", "special" })
    {
        var original = poses.RootElement.GetProperty("motions").GetProperty(cue);
        var edit = MotionRecut.Create(role.Id, cue, original);
        Check(Math.Abs(edit.Contact - (role.Id == "antena" ? .25 : .15)) < 1e-8,
            "Short anticipation (Antena includes equipment flight): " + role.Id + cue);
        Check(edit.Timeline.Duration < 1 && edit.Timeline.Duration > edit.Contact, "Compact recovery: " + role.Id + cue);
        Check(edit.ResumeIndex < edit.ContactIndex, "Continuation preserves a release: " + role.Id + cue);
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
        if (role.Id == "antena")
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
    foreach (var frame in poses.RootElement.GetProperty("motions").GetProperty("hit").GetProperty("frames").EnumerateArray())
    {
        var edited = MotionRecut.StationaryHurt(frame);
        Check(edited.GetProperty("actionAnchorX").GetSingle() == 0 && edited.GetProperty("OffsetX").GetSingle() == 0,
            "Stationary hurt: " + role.Id);
    }
    var hurt = MotionRecut.Hurt(poses.RootElement.GetProperty("motions").GetProperty("hit").GetProperty("frames"),
        poses.RootElement.GetProperty("motions").GetProperty("idle").GetProperty("frames")[0]);
    Check(hurt.Duration == .25 && hurt.Frames.Length == 5, "Short reaction excludes embedded jump/landing: " + role.Id);
}
Console.WriteLine($"PASS {assertions} pure state/timeline assertions (not host combat verification).");
