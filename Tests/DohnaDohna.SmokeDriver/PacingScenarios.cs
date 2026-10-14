using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static long _pacingDamage;
    private static bool _pacingWatching;
    private static int _pacingRoomVoices;
    private static bool _pacingGateTrace;
    private static long _pacingWaitStart, _pacingWaitEnd;
    private static void TracePacingWait(ref Task __result)
    {
        if (!_pacingGateTrace) return;
        _pacingWaitStart = Stopwatch.GetTimestamp();
        __result = ObservePacingWait(__result);
    }
    private static async Task ObservePacingWait(Task wait)
    {
        await wait;
        _pacingWaitEnd = Stopwatch.GetTimestamp();
    }
    private static void CountPacingVoice() => _pacingRoomVoices++;
    private static void TracePacingDamage() { if (_pacingWatching && _pacingDamage == 0) _pacingDamage = Stopwatch.GetTimestamp(); }

    private async Task VerifyPacing(Player owner, RunState run, string? onlyRole = null)
    {
        var trace = new Harmony("DohnaDohna.SmokeDriver.Pacing");
        var damage = AccessTools.GetDeclaredMethods(typeof(CreatureCmd)).Single(m => m.Name == "Damage"
            && m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>)
            && m.GetParameters()[2].ParameterType == typeof(decimal));
        trace.Patch(damage, prefix: new HarmonyMethod(typeof(LoadProbe), nameof(TracePacingDamage)));
        trace.Patch(AccessTools.Method(typeof(SquadAudio), "PlayInRoom"),
            prefix: new HarmonyMethod(typeof(LoadProbe), nameof(CountPacingVoice)));
        trace.Patch(AccessTools.Method(typeof(Cmd), nameof(Cmd.CustomScaledWait)),
            postfix: new HarmonyMethod(typeof(LoadProbe), nameof(TracePacingWait)));
        var mode = SaveManager.Instance.PrefsSave.FastMode;
        var records = new List<object>();
        try
        {
            string[][] groups = [["kuma", "alyce", "antena", "tora"], ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "kuma", "alyce"]];
            var covered = new HashSet<string>();
            foreach (var group in groups)
            {
                if (onlyRole != null && !group.Contains(onlyRole)) continue;
                SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
                SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
                await EnterBattle(owner);
                var squad = SquadCombatState.Get(owner);
                var combat = owner.Creature.CombatState!;
                var enemies = combat.Enemies.ToArray();
                foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
                var context = new BlockingPlayerChoiceContext();
                foreach (var role in group.Where(r => onlyRole == null || r == onlyRole).Where(covered.Add))
                {
                    var actor = squad.GetActor(role);
                    if (squad.Front != actor) await squad.Swap(actor);
                    await SquadFormation.Ensure(NCombatRoom.Instance!).FinishMovement(squad.Cancellation.Token);
                    var visuals = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
                    MotionMarker(role, "pacing-chain", "start");
                    foreach (var cue in new[] { "strike", "special", "special", "strike" })
                    {
                        var card = cue == "strike" ? (SquadAttackCard)combat.CreateCard<SquadStrike>(owner) : combat.CreateCard<SquadSpecial>(owner);
                        var target = card.TargetType == TargetType.AllEnemies ? null : enemies[^1];
                        var roots = squad.Actors.Select(c => c.GetCreatureNode()!).ToDictionary(n => n, n => n.Position);
                        var begin = Stopwatch.GetTimestamp();
                        _pacingDamage = 0; _pacingWatching = true;
                        var action = CardCmd.AutoPlay(context, card, target);
                        while (!action.IsCompleted)
                        {
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            VerifyComposition(visuals);
                            if (NCombatRoom.Instance!.GetChildrenRecursive<RoleMotionEffects>().Any(e => e.HasEmissions))
                                throw new Exception("Ordinary action emitted original VFX: " + role + cue);
                            if (roots.Any(p => p.Key.Position != p.Value)) throw new Exception("Pacing moved formation roots");
                            if (NCombatRoom.Instance!.GetNodeOrNull<CanvasLayer>("DohnaMotionCamera") != null)
                                throw new Exception("Nonlethal pacing opened a camera");
                        }
                        await action;
                        _pacingWatching = false;
                        if (_pacingDamage == 0) throw new Exception("Edited card missed its native damage: " + role + cue);
                        var hitMs = Stopwatch.GetElapsedTime(begin, _pacingDamage).TotalMilliseconds;
                        var endMs = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                        records.Add(new { role, cue, speed = SaveManager.Instance.PrefsSave.FastMode.ToString(), hitMs, endMs, tail = visuals.IsPlaying });
                        GD.Print($"DOHNA_PACING_CARD {role}/{cue} hitMs={hitMs:F1} completeMs={endMs:F1} tail={visuals.IsPlaying}");
                    }
                    while (visuals.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    MotionMarker(role, "pacing-chain", "end");
                    await Screenshot("pacing-" + role + "-settled.png");
                    await VerifyFullHurt(role, visuals, squad, records);

                    await VerifyNativeWindups(role, visuals, squad, owner, enemies[^1], records);

                    // Production short cinematic; real eligibility is exercised by Finisher.
                    int hits = 0;
                    MotionMarker(role, "compact-finisher", "start");
                    await visuals.Play("special", () => { hits++; return Task.CompletedTask; }, squad.Cancellation.Token, [enemies[^1]], cinematic: true);
                    if (hits != 1 || !visuals.IsPlaying) throw new Exception("Finisher gate must return with an owned tail");
                    while (visuals.IsPlaying || RoleFinisher.Current != null) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    MotionMarker(role, "compact-finisher", "end");
                    if (hits != 1 || visuals.IsPlaying) throw new Exception("Finisher logic/tail ownership");
                }
                if (onlyRole == null && group[0] == "kuma")
                {
                    // Native command baseline using the five vanilla characters'
                    // .15 s attack delay, on a real native creature/animation.
                    foreach (var speed in new[] { FastModeType.Fast, FastModeType.Normal, FastModeType.Instant })
                    {
                        SaveManager.Instance.PrefsSave.FastMode = speed;
                        _pacingDamage = 0; _pacingWatching = true;
                        long start = Stopwatch.GetTimestamp();
                        await new AttackCommand(1).FromMonster(enemies[0].Monster!)
                            .WithAttackerAnim("Attack", .15f).Execute(context);
                        _pacingWatching = false;
                        records.Add(new { nativeCommand = true, speed = speed.ToString(),
                            hitMs = Stopwatch.GetElapsedTime(start, _pacingDamage).TotalMilliseconds,
                            endMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds });
                        var visual = (RoleVisuals)squad.Front.GetCreatureNode()!.Visuals;
                        int hits = 0;
                        start = Stopwatch.GetTimestamp();
                        await visual.Play("strike", () => { hits++; return Task.CompletedTask; }, squad.Cancellation.Token, [enemies[^1]]);
                        if (hits != 1) throw new Exception("Speed mode lost or duplicated damage");
                        GD.Print($"DOHNA_PACING_SPEED {speed} gateMs={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F1}");
                        while (visual.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
                    // Exercise a genuine replacement while the prior visual tail
                    // still exists. AutoPlay has its own card-display delay and
                    // cannot by itself prove immediate same-actor continuation.
                    var linked = (RoleVisuals)squad.GetActor("antena").GetCreatureNode()!.Visuals;
                    int linkedHits = 0;
                    Task LinkedHit() { linkedHits++; return Task.CompletedTask; }
                    await linked.Play("strike", LinkedHit, squad.Cancellation.Token, [enemies[0]]);
                    var continuation = linked.Play("special", LinkedHit, squad.Cancellation.Token, [enemies[0]]);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var cutField = typeof(RoleVisuals).GetField("_cut", flags)!;
                    var continuingState = cutField.GetValue(linked)!;
                    if ((double)continuingState.GetType().GetField("EntryTime")!.GetValue(continuingState)! <= 0)
                        throw new Exception("Compatible follow-up repeated the full summon");
                    await continuation;
                    var retargeted = linked.Play("strike", LinkedHit, squad.Cancellation.Token, [enemies[^1]]);
                    var newState = cutField.GetValue(linked)!;
                    if ((double)newState.GetType().GetField("EntryTime")!.GetValue(newState)! != 0)
                        throw new Exception("Retargeted attack reused the wrong emission placement");
                    await retargeted;
                    while (linked.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (linkedHits != 3) throw new Exception("Continuation lost or duplicated a contact");
                    GD.Print("DOHNA_PACING_CONTINUATION_RETARGET_PASS");
                    var first = (RoleVisuals)squad.GetActor("kuma").GetCreatureNode()!.Visuals;
                    var second = (RoleVisuals)squad.GetActor("antena").GetCreatureNode()!.Visuals;
                    int hitCount = 0;
                    await first.Play("strike", () => { hitCount++; return Task.CompletedTask; }, squad.Cancellation.Token, [enemies[0]]);
                    if (!first.IsPlaying) throw new Exception("No owned visual tail after damage gate");
                    await second.Play("special", () => { hitCount++; return Task.CompletedTask; }, squad.Cancellation.Token, [enemies[^1]]);
                    if (hitCount != 2) throw new Exception("Independent actor logic");
                    using var cancel = new CancellationTokenSource();
                    var canceled = first.Play("special", () => { hitCount++; return Task.CompletedTask; }, cancel.Token, [enemies[0]]);
                    cancel.Cancel();
                    try { await canceled; throw new Exception("Canceled lead completed"); }
                    catch (OperationCanceledException) { }
                    if (hitCount != 2) throw new Exception("Canceled lead dealt damage");
                    while (second.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    GD.Print("DOHNA_PACING_INDEPENDENT_CANCEL_PASS");
                }
                await FinishCombat(owner);
            }
            if (covered.Count != (onlyRole == null ? 10 : 1)) throw new Exception("Missing pacing roles");
        }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "pacing.json"),
                JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            _pacingGateTrace = false; _pacingWatching = false;
            trace.UnpatchAll(trace.Id); SaveManager.Instance.PrefsSave.FastMode = mode;
        }
    }

    private async Task VerifyFullHurt(string role, RoleVisuals visuals, SquadCombatState squad, List<object> records)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var hurtField = typeof(RoleVisuals).GetField("_hurt", flags)!;
        var offsetField = typeof(RoleVisuals).GetField("_hurtOffset", flags)!;
        var jumpField = typeof(RoleVisuals).GetField("_jump", flags)!;
        var nodes = squad.Actors.Select(c => c.GetCreatureNode()!).ToDictionary(n => n, n => n.GetTransform());
        var external = visuals.Transform;
        var source = new MotionTimeline(visuals.SourceMotion("hit").GetProperty("frames"));
        Vector2 Offset() => (Vector2)offsetField.GetValue(visuals)!;
        MotionMarker(role, "full-hurt", "start");
        var gate = visuals.PlayHurt(squad.Cancellation.Token);
        if (!gate.IsCompletedSuccessfully) throw new Exception("Hit animation blocked a damage command");
        var timeline = (MotionTimeline)hurtField.GetValue(visuals)!;
        if (timeline.Duration != source.Duration || timeline.Frames.Length != source.Frames.Length)
            throw new Exception("Hit no longer retains the complete source clip");
        float peak = 0;
        double elapsed = 0;
        while (hurtField.GetValue(visuals) != null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
            var offset = Offset();
            peak = Math.Min(peak, offset.X);
            if (offset.X is < -18.001f or > .001f || offset.Y != 0 || jumpField.GetValue(visuals) != null
                || nodes.Any(p => p.Key.GetTransform() != p.Value) || visuals.Transform != external)
                throw new Exception("Hit changed formation/scale, jumped, or exceeded mild recoil");
            if (elapsed > source.Duration + 1) throw new Exception("Hit did not finish its original timeline");
        }
        if (peak > -10 || Offset() != Vector2.Zero) throw new Exception("Missing small recoil or residual position");
        await visuals.Wait(.25, squad.Cancellation.Token); // Observe idle, not a production wait.
        MotionMarker(role, "full-hurt", "end");
        records.Add(new { role, cue = "full-hurt", sourceFrames = source.Frames.Length, source.Duration, peak, elapsed });
        if (role is "kuma" or "antena")
        {
            MotionMarker(role, "hurt-replace", "start");
            await visuals.PlayHurt(squad.Cancellation.Token);
            await visuals.Wait(.12, squad.Cancellation.Token);
            var before = Offset();
            await visuals.PlayHurt(squad.Cancellation.Token);
            if (Offset() != before) throw new Exception("Consecutive hit snapped the recoil back to zero");
            await visuals.Wait(.12, squad.Cancellation.Token);
            using var cancel = new CancellationTokenSource();
            await visuals.PlayHurt(cancel.Token);
            cancel.Cancel();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (hurtField.GetValue(visuals) != null || Offset() != Vector2.Zero) throw new Exception("Canceled hit retained body displacement");
            await visuals.PlayHurt(squad.Cancellation.Token);
            await visuals.Wait(.12, squad.Cancellation.Token);
            await visuals.PlayNative("Attack", .15f, squad.Cancellation.Token);
            if (hurtField.GetValue(visuals) != null) throw new Exception("New attack hidden behind the old hit recovery");
            while (visuals.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await visuals.Wait(.15, squad.Cancellation.Token);
            MotionMarker(role, "hurt-replace", "end");
        }
        GD.Print($"DOHNA_FULL_HURT_PASS {role} sourceFrames={source.Frames.Length} duration={source.Duration:F3} peak={peak:F2}");
    }

    private async Task VerifyNativeWindups(string role, RoleVisuals visuals, SquadCombatState squad,
        Player owner, Creature target, List<object> records)
    {
        int voicesBefore = _pacingRoomVoices;
        foreach (var speed in new[] { FastModeType.Fast, FastModeType.Normal, FastModeType.Instant })
        {
            SaveManager.Instance.PrefsSave.FastMode = speed;
            foreach (var cue in new[] { "strike", "special", "cast" })
            foreach (float seconds in cue == "cast" ? new[] { .25f, .40f } : new[] { .15f, .20f })
            {
                CardModel card = cue == "strike" ? owner.Creature.CombatState!.CreateCard<SquadStrike>(owner)
                    : cue == "special" ? owner.Creature.CombatState!.CreateCard<SquadSpecial>(owner)
                    : owner.Creature.CombatState!.CreateCard<SquadDefend>(owner);
                var previous = SquadPlaySelection.Current;
                SquadPlaySelection.Current = new(card, squad.GetActor(role), null) { AnimationTargets = [target] };
                MotionMarker(role, $"{cue}-{seconds:F2}-{speed}", "start");
                var previousEffects = NCombatRoom.Instance!.GetChildrenRecursive<RoleMotionEffects>()
                    .Select(e => e.GetInstanceId()).ToHashSet();
                bool tail;
                ulong start = Engine.GetProcessFrames();
                long clockStart = Stopwatch.GetTimestamp();
                double longestFrame = 0;
                double syncSeconds = 0;
                var frameTimes = new List<object>();
                _pacingWaitStart = _pacingWaitEnd = 0;
                try
                {
                    _pacingGateTrace = true;
                    Task gate;
                    try { gate = CreatureCmd.TriggerAnim(squad.GetActor(role), cue == "cast" ? "Cast" : "Attack", seconds); }
                    finally { _pacingGateTrace = false; }
                    syncSeconds = Stopwatch.GetElapsedTime(clockStart).TotalSeconds;
                    long previousFrame = Stopwatch.GetTimestamp();
                    while (!gate.IsCompleted)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        longestFrame = Math.Max(longestFrame, GetProcessDeltaTime());
                        long now = Stopwatch.GetTimestamp();
                        frameTimes.Add(new { engineDelta = GetProcessDeltaTime(), wallDelta = Stopwatch.GetElapsedTime(previousFrame, now).TotalSeconds });
                        previousFrame = now;
                    }
                    await gate;
                    tail = visuals.IsPlaying;
                }
                finally { SquadPlaySelection.Current = previous; }
                ulong actualFrames = Engine.GetProcessFrames() - start;
                double actualSeconds = Stopwatch.GetElapsedTime(clockStart).TotalSeconds;
                if (speed == FastModeType.Instant && NCombatRoom.Instance!.GetChildrenRecursive<RoleMotionEffects>()
                    .Any(e => !e.IsQueuedForDeletion() && !previousEffects.Contains(e.GetInstanceId())))
                    throw new Exception($"Instant retained a new effect tail: {role}/{cue}/{seconds}");
                double setupSeconds = Stopwatch.GetElapsedTime(clockStart, _pacingWaitStart).TotalSeconds;
                double waitSeconds = Stopwatch.GetElapsedTime(_pacingWaitStart, _pacingWaitEnd).TotalSeconds;
                double contactSeconds = actualSeconds - setupSeconds - waitSeconds;
                long tailStart = Stopwatch.GetTimestamp();
                double tailTimeline = 0;
                if (speed != FastModeType.Instant)
                {
                    var cut = typeof(RoleVisuals).GetField("_cut", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(visuals)!;
                    var edit = (MotionRecut)cut.GetType().GetField("Edit")!.GetValue(cut)!;
                    tailTimeline = edit.Timeline.Duration - (double)cut.GetType().GetField("Time")!.GetValue(cut)!;
                }
                while (visuals.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                double recoverySeconds = Stopwatch.GetElapsedTime(tailStart).TotalSeconds;
                // Compare to the exact native gate in this same rendering mode;
                // MovieWriter wall-clock speed is deliberately not a timing proof.
                start = Engine.GetProcessFrames();
                clockStart = Stopwatch.GetTimestamp();
                await Cmd.CustomScaledWait(Math.Min(seconds * .5f, .25f), seconds);
                ulong referenceFrames = Engine.GetProcessFrames() - start;
                double referenceSeconds = Stopwatch.GetElapsedTime(clockStart).TotalSeconds;
                if (System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_MOVIE") == "1")
                {
                    if (Math.Abs((long)actualFrames - (long)referenceFrames) > 2)
                        throw new Exception($"Fixed-delta windup differs from native: {role}/{cue}/{seconds}/{speed}: {actualFrames}/{referenceFrames}");
                }
                else
                {
                    // A real-time run may render 5 long frames then 10 short
                    // frames for the same .15 seconds. Check the clock against
                    // the native requested timer; report paired frame counts,
                    // but never require them to match across different loads.
                    double expectedSeconds = speed == FastModeType.Instant ? 0
                        : speed == FastModeType.Fast ? Math.Min(seconds * .5f, .25f) : seconds;
                    // Godot timers tick after process callbacks and can count
                    // the current frame's delta even when created mid-frame.
                    // Instant must complete without yielding a frame. CPU work
                    // (texture/effect setup, GC, other process contention) is
                    // measured below, not mislabeled as an animation timer.
                    if (speed == FastModeType.Instant ? actualFrames != 0
                        : actualSeconds < expectedSeconds - longestFrame - .002
                          || actualSeconds > expectedSeconds + 2 * longestFrame + .025)
                        throw new Exception($"Wall-clock windup outside native timer/frame bounds: {role}/{cue}/{seconds}/{speed}: {actualSeconds:F4}, expected={expectedSeconds:F4}, longestFrame={longestFrame:F4}, setup={setupSeconds:F4}, wait={waitSeconds:F4}, contact={contactSeconds:F4}, reference={referenceSeconds:F4}");
                }
                if (speed != FastModeType.Instant && !tail) throw new Exception("Visual recovery blocked the release gate");
                records.Add(new { role, cue, seconds, speed = speed.ToString(), actualFrames, referenceFrames,
                    actualSeconds, referenceSeconds, longestFrame, tail, syncSeconds, setupSeconds, waitSeconds, contactSeconds, frameTimes,
                    tailTimeline, recoverySeconds });
                if (speed != FastModeType.Instant && Math.Abs(recoverySeconds - tailTimeline) > .12 + 2 * GetProcessDeltaTime())
                    throw new Exception($"Recovery is not authored real time: {role}/{cue}/{speed} tail={tailTimeline:F4} actual={recoverySeconds:F4}");
                MotionMarker(role, $"{cue}-{seconds:F2}-{speed}", "end");
            }
        }
        if (_pacingRoomVoices - voicesBefore != 1) throw new Exception("Cast/PowerUp must share one first-use voice per role/combat: " + role);
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        GD.Print("DOHNA_NATIVE_WINDUPS_PASS " + role + " samples=18 castVoice=1");
    }
}
