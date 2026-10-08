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
    private static void TracePacingDamage() { if (_pacingWatching && _pacingDamage == 0) _pacingDamage = Stopwatch.GetTimestamp(); }

    private async Task VerifyPacing(Player owner, RunState run)
    {
        var trace = new Harmony("DohnaDohna.SmokeDriver.Pacing");
        var damage = AccessTools.GetDeclaredMethods(typeof(CreatureCmd)).Single(m => m.Name == "Damage"
            && m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>)
            && m.GetParameters()[2].ParameterType == typeof(decimal));
        trace.Patch(damage, prefix: new HarmonyMethod(typeof(LoadProbe), nameof(TracePacingDamage)));
        var mode = SaveManager.Instance.PrefsSave.FastMode;
        var records = new List<object>();
        try
        {
            string[][] groups = [["kuma", "alyce", "antena", "tora"], ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "kuma", "alyce"]];
            var covered = new HashSet<string>();
            foreach (var group in groups)
            {
                SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;
                SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
                await EnterBattle(owner);
                var squad = SquadCombatState.Get(owner);
                var combat = owner.Creature.CombatState!;
                var enemies = combat.Enemies.ToArray();
                foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
                var context = new BlockingPlayerChoiceContext();
                foreach (var role in group.Where(covered.Add))
                {
                    var actor = squad.GetActor(role);
                    if (squad.Front != actor) await squad.Swap(actor);
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
                            if (roots.Any(p => p.Key.Position != p.Value)) throw new Exception("Pacing moved formation roots");
                            if (NCombatRoom.Instance!.GetNodeOrNull<CanvasLayer>("DohnaMotionCamera") != null)
                                throw new Exception("Nonlethal pacing opened a camera");
                        }
                        await action;
                        _pacingWatching = false;
                        if (_pacingDamage == 0) throw new Exception("Edited card missed its native damage: " + role + cue);
                        var hitMs = Stopwatch.GetElapsedTime(begin, _pacingDamage).TotalMilliseconds;
                        var endMs = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
                        records.Add(new { role, cue, hitMs, endMs, tail = visuals.IsPlaying });
                        GD.Print($"DOHNA_PACING_CARD {role}/{cue} hitMs={hitMs:F1} completeMs={endMs:F1} tail={visuals.IsPlaying}");
                    }
                    while (visuals.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    MotionMarker(role, "pacing-chain", "end");
                    await Screenshot("pacing-" + role + "-settled.png");
                    MotionMarker(role, "stationary-hurt", "start");
                    await visuals.PlayHurt(squad.Cancellation.Token);
                    while (visuals.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    MotionMarker(role, "stationary-hurt", "end");

                    // Production short cinematic; real eligibility is exercised by Finisher.
                    int hits = 0;
                    MotionMarker(role, "compact-finisher", "start");
                    await visuals.Play("special", () => { hits++; return Task.CompletedTask; }, squad.Cancellation.Token, [enemies[^1]], cinematic: true);
                    MotionMarker(role, "compact-finisher", "end");
                    if (hits != 1 || visuals.IsPlaying) throw new Exception("Finisher logic/tail ownership");
                }
                if (group[0] == "kuma")
                {
                    // Native command baseline using the five vanilla characters'
                    // .15 s attack delay, on a real native creature/animation.
                    foreach (var speed in new[] { FastModeType.Normal, FastModeType.Fast, FastModeType.Instant })
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
                    SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;
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
            if (covered.Count != 10) throw new Exception("Missing pacing roles");
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "pacing.json"),
                JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { _pacingWatching = false; trace.UnpatchAll(trace.Id); SaveManager.Instance.PrefsSave.FastMode = mode; }
    }
}
