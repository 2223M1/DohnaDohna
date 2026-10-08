using System.Text.Json;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using DohnaDohna.Code.UI;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifySelectionSizes(NCharacterSelectScreen screen, SquadSelectPanel panel)
    {
        var original = GetWindow().Size;
        try
        {
            foreach (var size in new[] { new Vector2I(1600, 900), new Vector2I(1280, 960), new Vector2I(1920, 810) })
            {
                GetWindow().Size = size;
                panel.ReturnToPrimary();
                await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
                await Screenshot($"select-primary-{size.X}x{size.Y}.png");
                AssertPosterBounds(screen);
                SquadSelectPanel.Open(screen);
                await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
                await Screenshot($"select-secondary-{size.X}x{size.Y}.png");
                AssertPosterBounds(screen);
                var bar = panel.GetChildrenRecursive<HBoxContainer>().Single(n => n.Name == "DohnaRoleBar");
                if (!screen.GetGlobalRect().Encloses(bar.GetGlobalRect())) throw new Exception("Role bar outside select screen: " + size);
                if (screen.DefaultFocusedControl is not SquadRoleButton { Slot: -1 })
                    throw new Exception("Native screen focus points at hidden primary buttons.");
            }
            GD.Print("DOHNA_SMOKE_SELECT_LAYOUT_CAPTURED ratios=16:9,4:3,64:27 (render fixture, not physical input)");
        }
        finally { GetWindow().Size = original; }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void AssertPosterBounds(NCharacterSelectScreen screen)
    {
        var background = screen.GetChildrenRecursive<SelectBackground>().Single();
        var figures = background.GetNode<Node2D>("Figures");
        if (figures.TopLevel) throw new Exception("Poster must retain native popup draw order.");
        foreach (var sprite in figures.GetChildrenRecursive<Sprite2D>())
        {
            var opaque = sprite.Texture.GetImage().GetUsedRect();
            var transform = screen.GetGlobalTransform().AffineInverse() * sprite.GetGlobalTransform();
            var bounds = transform * new Rect2(opaque.Position, opaque.Size);
            if (!new Rect2(Vector2.Zero, screen.Size).Encloses(bounds))
                throw new Exception($"Poster opaque art clipped: {bounds}, screen={screen.Size}");
        }
    }

    private async Task VerifyUsedMotions(Player owner, RunState run)
    {
        string[][] groups = [["kuma", "alyce", "antena", "tora"],
            ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "alyce", "kuma"]];
        var covered = new HashSet<string>();
        var records = new List<object>();
        foreach (var group in groups)
        {
            SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
            EncounterModel encounter = group[0] switch
            {
                "kikuchiyo" => ModelDb.Encounter<VineShamblerNormal>().ToMutable(),
                "kirakira" => ModelDb.Encounter<ScrollsOfBitingWeak>().ToMutable(),
                _ => ModelDb.Encounter<SlimesWeak>().ToMutable()
            };
            await EnterBattle(owner, encounter);
            var squad = SquadCombatState.Get(owner);
            var enemies = owner.Creature.CombatState!.Enemies.Where(c => c.IsAlive).ToArray();
            foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
            var rootTransforms = squad.Actors.Select(c => c.GetCreatureNode()!).ToDictionary(n => n, n => n.GetTransform());
            var hudPositions = rootTransforms.Keys.ToDictionary(n => n, n => n.GetNode<Control>("%HealthBar").GlobalPosition);
            var room = NCombatRoom.Instance!;
            var backgroundTransform = room.Background.GetTransform();
            foreach (var role in group.Where(covered.Add))
            {
                var actor = squad.GetActor(role);
                var visuals = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
                var body = visuals.GetCurrentBody();
                var originalBody = body.Transform;
                // Four simultaneous production idles; timing excludes PNG readback.
                var frameTimes = new List<double>();
                long lastFrame = System.Diagnostics.Stopwatch.GetTimestamp();
                MotionMarker(role, "idle", "start");
                for (int i = 0; i < 240; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    frameTimes.Add(System.Diagnostics.Stopwatch.GetElapsedTime(lastFrame, now).TotalMilliseconds);
                    lastFrame = now;
                }
                MotionMarker(role, "idle", "end");
                await Screenshot($"motion-{role}-idle.png");
                MenuFlowProbe.AssertAlignment(squad, "motion-idle-" + role);
                foreach (string cue in new[] { "strike", "special", "cast" })
                {
                    int hits = 0;
                    double sinceHit = -1;
                    double sinceStart = 0;
                    bool approachCaptured = false;
                    int sample = 0;
                    int emitterChecks = 0;
                    var targets = role == "antena" && cue == "special" ? enemies : [enemies[^1]];
                    MotionMarker(role, cue, "start");
                    var action = visuals.Play(cue, cue == "cast" ? null : Hit, squad.Cancellation.Token, targets);
                    // Observe outside Hit: awaiting a capture delay inside the native
                    // callback would freeze the presentation clock on its white flash.
                    while (!action.IsCompleted || visuals.IsPlaying)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        VerifyComposition(visuals);
                        if (role == "antena" && cue != "cast")
                        {
                            int connected = VerifyAntenaEmitter(visuals, cue);
                            if (connected > 0 && emitterChecks == 0) await Screenshot($"emitter-{role}-{cue}.png");
                            emitterChecks += connected;
                        }
                        foreach (var stage in GetTree().Root.GetChildrenRecursive<RoleMotionEffects>())
                        {
                            var bodies = (System.Collections.ICollection)typeof(RoleMotionEffects).GetField("_bodies", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(stage)!;
                            if (bodies.Count != 0) throw new Exception("Ordinary attack acquired native creature body control");
                        }
                        sinceStart += GetProcessDeltaTime();
                        if (!approachCaptured && sinceStart >= .3)
                        {
                            approachCaptured = true;
                            await Screenshot($"motion-{role}-{cue}-approach.png");
                        }
                        if (sinceHit < 0) continue;
                        sinceHit += GetProcessDeltaTime();
                        if (sample < 3 && sinceHit >= .12 + sample * .34)
                            await Screenshot($"motion-{role}-{cue}-after-{sample++}.png");
                    }
                    await action;
                    MotionMarker(role, cue, "end");
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (hits != (cue == "cast" ? 0 : 1)) throw new Exception($"{role}/{cue} hit count {hits}");
                    if (role == "antena" && cue != "cast" && emitterChecks == 0) throw new Exception("No live beam composition witnessed");
                    if (body.Transform != originalBody || room.Background.GetTransform() != backgroundTransform)
                        throw new Exception($"{role}/{cue} left presentation transforms");
                    if (rootTransforms.Any(pair => pair.Key.GetTransform() != pair.Value || pair.Key.GetNode<Control>("%HealthBar").GlobalPosition != hudPositions[pair.Key]))
                        throw new Exception($"{role}/{cue} moved formation/HUD");
                    if (GetTree().Root.GetChildrenRecursive<RoleMotionEffects>().Any()) throw new Exception("Leaked motion stage");
                    if (GetTree().Root.GetChildrenRecursive<Node2D>().Any(n => n.Name.ToString().StartsWith("DohnaMotionTrails")))
                        throw new Exception("Leaked original trail draw layer");
                    MenuFlowProbe.AssertAlignment(squad, "motion-restored-" + role + "-" + cue);
                    GD.Print($"DOHNA_SMOKE_MOTION_PASS role={role} cue={cue} targets={targets.Length} hits={hits} emitterChecks={emitterChecks} nativeBodiesUntouched=True");

                    async Task Hit()
                    {
                        hits++;
                        MotionMarker(role, cue, "hit");
                        // One native command, including AOE; visual fixture only,
                        // not a replacement for the card/effect integration tests.
                        // FromMonster fixes the target set to PlayerCreatures;
                        // this allied actor needs the same card-origin boundary as production.
                        var fixtureCard = actor.CombatState!.CreateCard<DohnaDohna.Cards.SquadStrike>(owner);
                        var command = new MegaCrit.Sts2.Core.Commands.Builders.AttackCommand(1).FromCard(fixtureCard, null).WithNoAttackerAnim();
                        HarmonyLib.AccessTools.PropertySetter(command.GetType(), "Attacker").Invoke(command, [actor]);
                        if (targets.Length == 1) command.Targeting(targets[0]);
                        else command.TargetingAllOpponents(actor.CombatState!);
                        typeof(SquadActions).GetMethod("ConfigureNativeImpact", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                            .Invoke(null, [command, role, cue == "strike"]);
                        await command.Execute(new BlockingPlayerChoiceContext());
                        sinceHit = 0;
                    }
                }
                MotionMarker(role, "hit", "start");
                await visuals.PlayHurt(squad.Cancellation.Token);
                while (visuals.IsPlaying)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                MotionMarker(role, "hit", "end");
                // The real death tests also cover posters and health. This
                // additional view leaves the body unobscured for source comparison.
                MotionMarker(role, "dead-body", "start");
                await visuals.Play("dead", null, squad.Cancellation.Token);
                MotionMarker(role, "dead-body", "end");
                frameTimes.Sort();
                records.Add(new { role, samples = frameTimes.Count, medianMs = frameTimes[frameTimes.Count/2], p95Ms = frameTimes[(int)(frameTimes.Count*.95)],
                    maxMs = frameTimes[^1], measurement = "four idles; Stopwatch process interval, excludes screenshots. With MovieWriter this is capture throughput, NOT runtime performance" });
            }
            await squad.Swap(squad.Living.First());
            MenuFlowProbe.AssertAlignment(squad, "motion-after-swap");
            await Screenshot($"motion-formation-after-swap-{group[0]}.png");
            await FinishCombat(owner);
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        }
        if (covered.Count != 10) throw new Exception("Incomplete role motion coverage");
        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "motion-timing.json"),
            JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void MotionMarker(string role, string cue, string phase) => GD.Print("DOHNA_MOVIE_MARK " +
        JsonSerializer.Serialize(new { role, cue, phase, drawnFrame = Engine.GetFramesDrawn(),
            processFrame = Engine.GetProcessFrames(), wallMs = Time.GetTicksMsec() }));
}
