using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static readonly List<string> ImpactAudio = [];
    private static AttackCommand? _impactCommand;
    private static MegaCrit.Sts2.Core.Entities.Creatures.Creature[] _impactTargets = [];
    private static (Vector2 Center, Vector2 Floor)[] _impactAnchors = [];
    private static bool _impactObserving;
    private static int _originalImpactCount;
    private static void TraceImpactAudio(string reference) { if (_impactObserving) ImpactAudio.Add(reference); }
    private static void TraceImpactCommand(AttackCommand __instance)
    {
        if (_impactObserving && __instance.ModelSource is SquadCardModel)
        {
            _impactCommand = __instance;
            // Native hurt may move a body's marker before the next render.
            // Witness the actual spawn-time target, not its subsequent recoil.
            _impactAnchors = _impactTargets.Select(t => (t.GetCreatureNode()!.VfxSpawnPosition, t.GetCreatureNode()!.GetBottomOfHitbox())).ToArray();
        }
    }
    private static void InspectImpactFrame(RoleMotionEffects __instance, JsonElement frame, double time)
    {
        if (!_impactObserving || !frame.TryGetProperty("Effects", out var effects)) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        bool cinematic = (bool)typeof(RoleMotionEffects).GetField("_cinematic", flags)!.GetValue(__instance)!;
        bool recut = (bool)typeof(RoleMotionEffects).GetField("_recut", flags)!.GetValue(__instance)!;
        var targets = (Array)typeof(RoleMotionEffects).GetField("_targets", flags)!.GetValue(__instance)!;
        var sequences = (System.Collections.IEnumerable)typeof(RoleMotionEffects).GetField("_sequences", flags)!.GetValue(__instance)!;
        var actual = sequences.Cast<object>().Where(s => Math.Abs((double)s.GetType().GetField("Start")!.GetValue(s)! - time) < 1e-8)
            .Select(s => ((Texture2D[])s.GetType().GetField("Frames")!.GetValue(s)!)[0].ResourcePath).Order().ToArray();
        var expected = effects.EnumerateObject().Where(p => (cinematic && !recut) || !p.Value.GetProperty("impact").GetBoolean())
            .SelectMany(p => Enumerable.Repeat(p.Value.GetProperty("images")[0].GetString()!,
                p.Value.GetProperty("AssignToTarget").GetInt32() != 0 ? targets.Length : 1)).Order().ToArray();
        if (!actual.SequenceEqual(expected)) throw new Exception("Original impact emission policy differs from imported art classification");
        if (cinematic && !recut) _originalImpactCount += effects.EnumerateObject().Count(p => p.Value.GetProperty("impact").GetBoolean());
    }

    private async Task VerifyImpacts(Player owner, RunState run)
    {
        var harmony = new Harmony("DohnaDohna.SmokeDriver.ImpactPolicy");
        harmony.Patch(AccessTools.Method(typeof(SquadAudio), nameof(SquadAudio.Play)), prefix: new HarmonyMethod(typeof(LoadProbe), nameof(TraceImpactAudio)));
        harmony.Patch(AccessTools.Method(typeof(AttackCommand), nameof(AttackCommand.Execute)), prefix: new HarmonyMethod(typeof(LoadProbe), nameof(TraceImpactCommand)));
        harmony.Patch(AccessTools.Method(typeof(RoleMotionEffects), nameof(RoleMotionEffects.AddFrame)), postfix: new HarmonyMethod(typeof(LoadProbe), nameof(InspectImpactFrame)));
        try
        {
            // Independent recorded baselines for three native sample sounds and
            // the FMOD fire event. No role voice or movement sound in these windows.
            foreach (var sound in new[] { "blunt_attack.mp3", "slash_attack.mp3", "heavy_attack.mp3" })
                await NativeAudioWitness("impact-baseline-" + sound, () => { NDebugAudioManager.Instance!.Play(sound); return Task.CompletedTask; });
            await NativeAudioWitness("impact-baseline-fire", () => { SfxCmd.Play("event:/sfx/characters/attack_fire"); return Task.CompletedTask; });
            string[][] groups = [["kuma", "alyce", "antena", "tora"], ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "kuma", "alyce"]];
            var covered = new HashSet<string>();
            foreach (var group in groups)
            {
                SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
                await EnterBattle(owner);
                var squad = SquadCombatState.Get(owner);
                var combat = owner.Creature.CombatState!;
                var enemies = combat.Enemies.Where(e => e.IsAlive).ToArray();
                foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
                await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
                foreach (var role in group.Where(covered.Add))
                {
                    var actor = squad.GetActor(role);
                    if (squad.Front != actor) await squad.Swap(actor);
                    var visuals = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
                    using var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString($"res://DohnaDohna/roles/{role}/motions.json"));
                    foreach (var cue in new[] { "strike", "special" })
                    {
                        string label = $"impact-{role}-{cue}";
                        var frames = json.RootElement.GetProperty("motions").GetProperty(cue).GetProperty("frames");
                        var originalSounds = frames.EnumerateArray().Where(f => f.TryGetProperty("Sound", out var s) && s.GetProperty("Enable").GetInt32() != 0)
                            .SelectMany(f => Enumerable.Range(1, 3).Where(n => f.GetProperty("Sound").GetProperty("impact" + n).GetBoolean())
                                .Select(n => f.GetProperty("Sound").GetProperty("Sound" + n).GetString()!)).ToArray();
                        var voices = frames.EnumerateArray().Where(f => f.TryGetProperty("Voice", out var v) && v.GetProperty("Enable").GetInt32() != 0)
                            .SelectMany(f => Enumerable.Range(1, 3).Select(n => f.GetProperty("Voice").GetProperty("Voice" + n).GetString()))
                            .Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
                        _impactObserving = true; _impactCommand = null; _originalImpactCount = 0; ImpactAudio.Clear();
                        AudioProbe.Mark(label + "-begin");
                        var card = cue == "strike" ? (SquadAttackCard)combat.CreateCard<SquadStrike>(owner) : combat.CreateCard<SquadSpecial>(owner);
                        // Both common cards resolve to this current front. Auxiliary
                        // choices are native autoplay choices, not custom test effects.
                        string nativeScene = role switch {
                            "antena" => "vfx_sweeping_beam_impact", "tora" when cue == "strike" => "vfx_fire_burst",
                            "joker" when cue == "special" => "vfx_fire_burst", "kirakira" when cue == "special" => "vfx_poison_impact",
                            "medhico" or "zappa" => "vfx_heavy_blunt", "porno" when cue == "special" => "vfx_heavy_blunt",
                            "kikuchiyo" or "porno" => "vfx_attack_slash", _ => "vfx_attack_blunt" };
                        var target = card.TargetType == TargetType.AllEnemies ? null : enemies[^1];
                        var targets = target == null ? enemies : [target];
                        _impactTargets = targets;
                        var seen = new HashSet<ulong>();
                        var play = CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, target);
                        while (!play.IsCompleted)
                        {
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            VerifyComposition(visuals);
                            foreach (var node in NCombatRoom.Instance!.CombatVfxContainer.GetChildrenRecursive<Node2D>()
                                .Where(n => n.SceneFilePath == $"res://scenes/vfx/{nativeScene}.tscn"))
                            {
                                if (!seen.Add(node.GetInstanceId())) continue;
                                bool atBase = nativeScene is "vfx_heavy_blunt" or "vfx_fire_burst";
                                if (!_impactAnchors.Any(t => (atBase ? t.Floor : t.Center).DistanceTo(node.GlobalPosition) < 1))
                                    throw new Exception("Native contact VFX is not on a real target: " + label + " " + node.GlobalPosition
                                        + " expected=" + string.Join(';', _impactAnchors));
                                await Screenshot(label + "-native-" + seen.Count + ".png");
                            }
                        }
                        await play;
                        if (ImpactAudio.Intersect(voices).Any()) throw new Exception("Ordinary attack played a character voice: " + label);
                        if (_impactCommand == null || seen.Count != targets.Length || ImpactAudio.Intersect(originalSounds).Any())
                            throw new Exception($"Ordinary impact policy {label}: command={_impactCommand != null}, nodes={seen.Count}/{targets.Length}, originalAudio={string.Join(',', ImpactAudio.Intersect(originalSounds))}");
                        string expectedSound = nativeScene switch { "vfx_attack_slash" => "slash_attack.mp3", "vfx_heavy_blunt" => "heavy_attack.mp3",
                            "vfx_fire_burst" => "event:/sfx/characters/attack_fire", _ => "blunt_attack.mp3" };
                        if ((_impactCommand.HitSfx ?? _impactCommand.TmpHitSfx) != expectedSound) throw new Exception("Wrong native contact sound");
                        // Card logic now ends before the contact sound. Capture
                        // its real tail too; this is outside timing measurements.
                        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
                        AudioProbe.Mark(label + "-end");
                        _impactObserving = false;
                        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);

                        // Short cinematic player does not independently spawn
                        // contact or voice; real native clear-all is tested by Finisher.
                        _impactObserving = true; _originalImpactCount = 0; ImpactAudio.Clear(); _impactCommand = null;
                        AudioProbe.Mark(label + "-cinematic-begin");
                        await visuals.Play(cue, null, squad.Cancellation.Token, targets, cinematic: true);
                        if (ImpactAudio.Intersect(originalSounds).Any() || _impactCommand != null || _originalImpactCount != 0)
                            throw new Exception("Compact cinematic doubled contact feedback: " + label);
                        if (ImpactAudio.Intersect(voices).Any())
                            throw new Exception("Compact cinematic retained a long attack voice: " + label);
                        AudioProbe.Mark(label + "-cinematic-end");
                        _impactObserving = false;
                        GD.Print($"DOHNA_SMOKE_IMPACT_CASE_PASS {label} nativeTargets={seen.Count} originalCinematicEffects={_originalImpactCount} originalSounds={originalSounds.Length}");
                        await ToSignal(GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
                    }
                }
                await FinishCombat(owner);
            }
            if (covered.Count != 10) throw new Exception("Incomplete impact role coverage");
        }
        finally { _impactObserving = false; harmony.UnpatchAll(harmony.Id); }
    }
}
