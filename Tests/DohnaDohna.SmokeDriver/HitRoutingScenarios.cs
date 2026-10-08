using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static readonly List<(Creature Target, string Path)> HitAnchors = [];
    private static void TraceHit(Creature target, string path) => HitAnchors.Add((target, path));

    private async Task VerifyHitRouting(Player owner)
    {
        var fixture = SquadRunState.Create(["kuma", "alyce", "antena", "tora"]);
        foreach (var member in fixture.Members) member.Hp = member.MaxHp = 25;
        await SetCatalogRoster(owner, (RunState)owner.RunState, fixture);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var context = new BlockingPlayerChoiceContext();
        foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
        foreach (var member in squad.Living)
            foreach (var power in member.Powers.ToArray()) await PowerCmd.Remove(power);
        var target = combat.Enemies.First();
        var actor = squad.GetActor("antena");
        if (actor == squad.Front) await squad.Swap(squad.GetActor("tora"));
        var front = squad.Front;
        await CreatureCmd.GainBlock(front, 9, ValueProp.Unpowered, null);
        int frontHp = front.CurrentHp;
        await PowerCmd.Apply<ThornsPower>(context, target, 4, target, null);
        await CreatureCmd.GainBlock(actor, 2, ValueProp.Unpowered, null);
        int actorHp = actor.CurrentHp;
        await PlayRear();
        if (actor.CurrentHp != actorHp - 2 || actor.Block != 0) throw new Exception("Rear thorns did not use its own HP/block");
        await PowerCmd.Apply<BufferPower>(context, actor, 1, actor, null);
        actorHp = actor.CurrentHp;
        await PlayRear();
        if (actor.CurrentHp != actorHp || actor.HasPower<BufferPower>()) throw new Exception("Rear thorns ignored native Buffer");
        var hurt = typeof(RoleVisuals).GetField("_hurt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (hurt.GetValue(actor.GetCreatureNode()!.Visuals) != null) throw new Exception("SkipHurtAnim thorns started a body reaction");
        // AOE: native Thorns fires once for each struck enemy, not once per card.
        foreach (var enemy in combat.Enemies.Skip(1)) await PowerCmd.Apply<ThornsPower>(context, enemy, 4, enemy, null);
        actorHp = actor.CurrentHp;
        await PlayRear();
        if (actor.CurrentHp != actorHp - 4 * combat.Enemies.Count) throw new Exception("AOE thorns repeated or missed a target");
        foreach (var enemy in combat.Enemies) await PowerCmd.Remove<ThornsPower>(enemy);
        // Explicit enemy-origin non-attack loss remains assigned to its member.
        actorHp = actor.CurrentHp;
        await CreatureCmd.Damage(context, actor, 1, ValueProp.Unpowered | ValueProp.SkipHurtAnim, target);
        if (actor.CurrentHp != actorHp - 1) throw new Exception("Explicit damage owner lost");
        if (front.CurrentHp != frontHp || front.Block != 9) throw new Exception("Rear damage modified front");
        GD.Print("DOHNA_SMOKE_REAR_THORNS_PASS block,buffer,aoe,explicit-owner,skip-hurt frontUnchanged=True");

        await PowerCmd.Apply<ThornsPower>(context, target, 4, target, null);
        await CreatureCmd.SetCurrentHp(actor, 1);
        var fairy = await PotionCmd.TryToProcure<FairyInABottle>(owner);
        if (!fairy.success) throw new Exception("Could not procure native rear save fixture");
        await PlayRear();
        if (actor.CurrentHp != 7 || owner.Potions.Any(p => p is FairyInABottle)
            || front.CurrentHp != frontHp || front.Block != 9) throw new Exception("Rear thorns native save mismatch");
        GD.Print("DOHNA_SMOKE_REAR_THORNS_FAIRY_PASS hp=7 frontUnchanged=True");
        await CreatureCmd.SetCurrentHp(actor, 1);
        int handBeforeDeath = PileType.Hand.GetPile(owner).Cards.Count;
        await PlayRear();
        if (actor.IsAlive || front.CurrentHp != frontHp || front.Block != 9) throw new Exception("Rear reflected death changed front");
        if (PileType.Hand.GetPile(owner).Cards.Count != handBeforeDeath)
            throw new Exception("Sweeping Beam drew a card after its real actor died to reflection");
        await PowerCmd.Remove<ThornsPower>(target);
        GD.Print("DOHNA_SMOKE_REAR_THORNS_DEATH_PASS");

        var trace = new Harmony("DohnaDohna.SmokeDriver.HitRouting");
        var method = AccessTools.Method(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter), [typeof(Creature), typeof(string)]);
        trace.Patch(method, postfix: new HarmonyMethod(typeof(LoadProbe), nameof(TraceHit)));
        try
        {
            await CreatureCmd.LoseBlock(context, front, front.Block, null);
            await CreatureCmd.SetCurrentHp(front, 5);
            var next = squad.Living.TakeLast(2).First();
            await CreatureCmd.SetCurrentHp(next, 25);
            HitAnchors.Clear();
            await DamageCmd.Attack(7).WithHitCount(3).FromMonster(target.Monster!).WithNoAttackerAnim()
                .WithHitFx("vfx/vfx_attack_slash").Execute(context);
            if (front.IsAlive || next.CurrentHp != 11 || HitAnchors.Count != 3
                || HitAnchors[0].Target != front || HitAnchors.Skip(1).Any(h => h.Target != next))
                throw new Exception("7x3 target/VFX sequence mismatch");
            GD.Print("DOHNA_SMOKE_NATIVE_HIT_HANDOFF_PASS hp=11 vfxTargets=front,next,next");

            await CreatureCmd.SetCurrentHp(next, 25);
            HitAnchors.Clear();
            VfxCmd.PlayOnCreatureCenter(owner.Creature, "vfx/vfx_slime_impact");
            if (HitAnchors.Count != 1 || HitAnchors[0].Target != next) throw new Exception("Direct hidden-host visual target not resolved");
            await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            // Actual monster method, not an imitation of the native attack loop.
            var slime = combat.Enemies.Single(c => c.Monster is TwigSlimeS).Monster!;
            string attackSfx = (string)AccessTools.PropertyGetter(slime.GetType(), "AttackSfx").Invoke(slime, null)!;
            await NativeAudioWitness("baseline-attack", () => { SfxCmd.Play(attackSfx); return Task.CompletedTask; });
            await NativeAudioWitness("baseline-break", () => { SfxCmd.Play("event:/sfx/block_break"); return Task.CompletedTask; });
            await CreatureCmd.GainBlock(next, 1, ValueProp.Unpowered, null);
            HitAnchors.Clear();
            await NativeAudioWitness("native-tackle", async () =>
            {
                var task = (Task)AccessTools.Method(slime.GetType(), "TackleMove").Invoke(slime, [new Creature[] { owner.Creature }])!;
                int sample = 0;
                while (!task.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (sample++ % 6 == 0) await Screenshot($"native-tackle-{sample:000}.png");
                }
                await task;
                await Screenshot("native-tackle-hurt.png");
            });
            if (next.CurrentHp != 22 || HitAnchors.Count != 1 || HitAnchors[0].Target != next
                || HitAnchors[0].Path != "vfx/vfx_slime_impact") throw new Exception("Native monster feedback mismatch");
            GD.Print("DOHNA_SMOKE_REAL_MONSTER_FEEDBACK_PASS move=TwigSlimeS.TackleMove blockBroken=True vfx=slime_impact");

            // Recorded positive/negative control for native shield-break output.
            // SkipHurtAnim avoids role voice; the test-only one-shot gate excludes
            // unrelated native sounds. Never changes game/global volume settings.
            foreach (bool audible in new[] { true, false })
            {
                await CreatureCmd.GainBlock(next, 1, ValueProp.Unpowered, null);
                try
                {
                    await NativeAudioWitness(audible ? "isolated-break" : "silent-break-control", async () =>
                    {
                        AudioProbe.NativeWitnessGate = audible ? "event:/sfx/block_break" : "mute-all-witness";
                        await CreatureCmd.Damage(context, next, 2, ValueProp.Unpowered | ValueProp.SkipHurtAnim, target);
                    });
                }
                finally { AudioProbe.NativeWitnessGate = null; }
            }

            await FinishCombat(owner);
            await EnterBattle(owner, ModelDb.Encounter<AxebotsNormal>().ToMutable());
            squad = SquadCombatState.Get(owner);
            await CreatureCmd.SetCurrentHp(squad.Front, 25);
            var axe = owner.Creature.CombatState!.Enemies.First().Monster!;
            HitAnchors.Clear();
            await NativeAudioWitness("native-slash", async () =>
            {
                var task = (Task)AccessTools.Method(axe.GetType(), "OneTwoMove").Invoke(axe, [new Creature[] { owner.Creature }])!;
                int frame = 0;
                while (!task.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (frame++ % 5 == 0) await Screenshot($"native-slash-{frame:000}.png");
                }
                await task;
            });
            if (HitAnchors.Count != 2 || HitAnchors.Any(h => h.Target != squad.Front || h.Path != "vfx/vfx_attack_slash"))
                throw new Exception("Native Axebot multi-hit VFX target mismatch");
            GD.Print("DOHNA_SMOKE_REAL_SLASH_PASS move=Axebot.OneTwoMove hits=2");

            await PowerCmd.Remove<StockPower>(axe.Creature); // End the fixture without its separate respawn encounter.
            await FinishCombat(owner);
            await EnterBattle(owner, ModelDb.Encounter<SlimesNormal>().ToMutable());
            squad = SquadCombatState.Get(owner);
            var medium = owner.Creature.CombatState!.Enemies.Select(c => c.Monster).OfType<LeafSlimeM>().Single();
            int cardsBefore = owner.PlayerCombatState!.AllCards.Count();
            var spit = (Task)AccessTools.Method(typeof(LeafSlimeM), "StickyShotMove").Invoke(medium, [new Creature[] { owner.Creature }])!;
            var marker = medium.Creature.GetCreatureNode()!.GetSpecialNode<Node2D>("Visuals/SpitTarget")
                ?? throw new Exception("Native slime projectile marker missing");
            if (Math.Abs(marker.GlobalPosition.X - squad.Front.GetCreatureNode()!.GlobalPosition.X) > .01f)
                throw new Exception("Native projectile still aims at hidden gateway");
            await Screenshot("native-spit-target.png");
            await spit;
            if (owner.PlayerCombatState.AllCards.Count() != cardsBefore + 2) throw new Exception("Visual adapter changed native Slimed card recipient");
            var worm = NWormyImpactVfx.Create(owner.Creature) ?? throw new Exception("Direct worm visual missing");
            owner.Creature.GetVfxContainer()!.AddChild(worm);
            await Screenshot("native-worm-impact.png");
            worm.QueueFree();
            GD.Print("DOHNA_SMOKE_NATIVE_PROJECTILE_PASS move=LeafSlimeM.StickyShotMove statusCards=2 explicitFactory=NWormyImpactVfx");
        }
        finally { trace.UnpatchAll(trace.Id); }
        await FinishCombat(owner);

        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        var rear = squad.GetActor("kuma");
        if (squad.Front == rear) await squad.Swap(squad.GetActor("alyce"));
        var lastEnemies = owner.Creature.CombatState!.Enemies.ToArray();
        await VerifyCinematicTargetReturn((RoleVisuals)rear.GetCreatureNode()!.Visuals, lastEnemies[0], squad.Cancellation.Token);
        foreach (var other in lastEnemies.Skip(1)) await CreatureCmd.Kill(other, true);
        var last = lastEnemies[0];
        await CreatureCmd.SetCurrentHp(last, 1);
        await CreatureCmd.SetCurrentHp(rear, 1);
        await PowerCmd.Apply<ThornsPower>(context, last, 4, last, null);
        int survivorHp = squad.Front.CurrentHp;
        await CardCmd.AutoPlay(context, owner.Creature.CombatState.CreateCard<KumaSignature>(owner), last);
        if (rear.IsAlive || last.IsAlive || squad.Front.CurrentHp != survivorHp)
            throw new Exception("Last enemy and rear reflected death did not retain native outcome");
        GD.Print("DOHNA_SMOKE_REAR_THORNS_LAST_ENEMY_PASS bothDead=True frontUnchanged=True");

        async Task PlayRear() => await CardCmd.AutoPlay(context, combat.CreateCard<SquadSweepingBeam>(owner), null);
    }

    private async Task VerifyCinematicTargetReturn(RoleVisuals visuals, Creature target, CancellationToken token)
    {
        // Deliberately keep the target alive in a visual-only cinematic fixture.
        // Real card finisher eligibility is covered separately by Finisher.
        var body = target.GetCreatureNode()!.Visuals.GetCurrentBody();
        var initial = body.Transform;
        float maximum = 0;
        var task = visuals.Play("special", () => Task.CompletedTask, token, [target], cinematic: true);
        while (!task.IsCompleted)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            maximum = Math.Max(maximum, body.Transform.Origin.DistanceTo(initial.Origin));
            if (!body.Transform.IsEqualApprox(initial)) throw new Exception("Compact finisher took over native target body");
        }
        await task;
        if (!body.Transform.IsEqualApprox(initial)) throw new Exception("Compact finisher leaked target transform");
        GD.Print($"DOHNA_SMOKE_CINEMATIC_TARGET_UNTOUCHED_PASS maxOffset={maximum} (visual-only fixture)");
    }

    private async Task VerifyEmitterMatrix(Player owner)
    {
        var initialSize = GetWindow().Size;
        var sizes = new[] { new Vector2I(1600, 900), new Vector2I(1280, 960), new Vector2I(1920, 810) };
        try
        {
            for (int shape = 0; shape < sizes.Length; shape++)
            {
                GetWindow().Size = sizes[shape];
                EncounterModel encounter = shape switch
                {
                    1 => ModelDb.Encounter<VineShamblerNormal>().ToMutable(),
                    2 => ModelDb.Encounter<ScrollsOfBitingWeak>().ToMutable(),
                    _ => ModelDb.Encounter<SlimesWeak>().ToMutable()
                };
                await EnterBattle(owner, encounter);
                var squad = SquadCombatState.Get(owner);
                var actor = squad.GetActor("antena");
                var visuals = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
                var enemies = owner.Creature.CombatState!.Enemies.ToArray();
                foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
                var orbContext = new BlockingPlayerChoiceContext();
                await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.LightningOrb>(orbContext, owner);
                await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.FrostOrb>(orbContext, owner);
                await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.DarkOrb>(orbContext, owner);
                var manager = owner.Creature.GetCreatureNode()!.OrbManager!;
                if (manager.Scale != Vector2.One) throw new Exception("Orb manager changed native display scale");
                foreach (bool atFront in new[] { false, true })
                {
                    if ((squad.Front == actor) != atFront) await squad.Swap(atFront ? actor : squad.GetActor("kuma"));
                    foreach (string cue in new[] { "strike", "special" })
                    {
                        string clip = $"saucer-{shape}-{(atFront ? "front" : "rear")}-{cue}";
                        MotionMarker("antena", clip, "start");
                        int witnesses = 0, hits = 0;
                        var targets = cue == "strike" ? new[] { enemies[^1] } : enemies;
                        var home = visuals.SaucerAnchor!.Value;
                        var memberNode = actor.GetCreatureNode()!;
                        var memberTransform = (memberNode.Position, memberNode.Scale, memberNode.Rotation);
                        float flightDistance = 0;
                        var task = visuals.Play(cue, async () =>
                        {
                            hits++;
                            var targetPoint = targets.Aggregate(Vector2.Zero, (sum, target) =>
                                sum + target.GetCreatureNode()!.Visuals.VfxSpawnPosition.GlobalPosition) / targets.Length;
                            var expected = targetPoint + visuals.GlobalTransform.BasisXform(new Vector2(36 - 320, -360 + 160 - 38));
                            if (visuals.SaucerAnchor!.Value.DistanceTo(expected) > .02f)
                                throw new Exception($"Saucer did not reach actual target firing fixture: {visuals.SaucerAnchor} != {expected}");
                            await Screenshot($"saucer-contact-{shape}-{atFront}-{cue}.png");
                            await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), targets, 1, ValueProp.Move, actor);
                        }, squad.Cancellation.Token, targets);
                        while (!task.IsCompleted || visuals.IsPlaying)
                        {
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            flightDistance = Math.Max(flightDistance, home.DistanceTo(visuals.SaucerAnchor!.Value));
                            if ((memberNode.Position, memberNode.Scale, memberNode.Rotation) != memberTransform)
                                throw new Exception("Flying saucer moved the member/HUD root");
                            VerifyComposition(visuals);
                            int connected = VerifyAntenaEmitter(visuals, cue);
                            if (connected > 0 && witnesses == 0) await Screenshot($"emitter-{shape}-{atFront}-{cue}.png");
                            witnesses += connected;
                        }
                        await task;
                        MotionMarker("antena", clip, "end");
                        if (witnesses == 0 || hits != 1 || flightDistance < 25 || visuals.SaucerAnchor!.Value.DistanceTo(home) > .02f)
                            throw new Exception("Emitter matrix did not witness flight, one hit, connected beams and return");
                        GD.Print($"DOHNA_SMOKE_EMITTER_MATRIX_PASS shape={shape} size={sizes[shape]} front={atFront} cue={cue} samples={witnesses} flightDistance={flightDistance}");
                    }
                }
                await OrbCmd.AddSlots(owner, 3);
                await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
                var orbNodes = manager.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Orbs.NOrb>().ToArray();
                if (orbNodes.Length != 6 || orbNodes.Any(n => n.Scale.DistanceTo(Vector2.One * .85f) > .001f
                    || n.Position.Y >= 0)) throw new Exception("Expanded native orbs lost size or upper arc layout");
                await Screenshot($"saucer-expanded-{shape}.png");
                await FinishCombat(owner);
                await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            }
        }
        finally { GetWindow().Size = initialSize; }
    }

    private async Task NativeAudioWitness(string label, Func<Task> action)
    {
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        AudioProbe.Mark(label + "-begin");
        await action();
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        AudioProbe.Mark(label + "-end");
    }

    private static int VerifyComposition(RoleVisuals visuals)
    {
        var type = typeof(RoleVisuals);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var frame = (JsonElement)type.GetField("_primaryFrame", flags)!.GetValue(visuals)!;
        if (frame.ValueKind != JsonValueKind.Object || !frame.TryGetProperty("CgLayers", out var layers)) return 0;
        var sprites = (Dictionary<string, Sprite2D>)type.GetField("_sprites", flags)!.GetValue(visuals)!;
        var order = (Dictionary<Sprite2D, int>)type.GetField("_layerOrder", flags)!.GetValue(visuals)!;
        var drawn = order.OrderBy(p => p.Key.GetIndex()).ToArray();
        if (drawn.Any(p => p.Key.ZIndex != 0) || !drawn.Select(p => p.Value).SequenceEqual(drawn.Select(p => p.Value).Order()))
            throw new Exception("Original local draw order escaped native scene depth");
        int checks = 0;
        bool cinematic = (bool)type.GetField("_cinematic", flags)!.GetValue(visuals)!;
        var art = visuals.GetNode<Node2D>("%Visuals/OriginalMotion");
        foreach (var shadow in (List<Sprite2D>)type.GetField("_shadows", flags)!.GetValue(visuals)!)
            if (shadow.Visible && (shadow.GetParent() != art.GetParent() || shadow.ZIndex != 0
                || shadow.ZIndex > art.ZIndex || shadow.ZIndex == art.ZIndex && shadow.GetIndex() >= art.GetIndex()))
                throw new Exception("Ground shadow rose into the acting art depth");
        foreach (var group in layers.EnumerateObject().Where(p => (cinematic || !p.Value.GetProperty("impact").GetBoolean())
                         && p.Value.TryGetProperty("CgName", out var n) && !string.IsNullOrEmpty(n.GetString()))
                     .GroupBy(p => p.Value.GetProperty("IsGlobalPosition").GetInt32()))
        {
            Vector2? translation = null;
            foreach (var layer in group)
            {
                var position = new Vector2(layer.Value.GetProperty("PosX").GetSingle(), layer.Value.GetProperty("PosY").GetSingle());
                var delta = sprites[layer.Name].Position - position;
                if (translation is { } first && first.DistanceTo(delta) > .01f) throw new Exception("Original composition distorted between body/equipment layers");
                translation = delta;
                checks++;
            }
        }
        return checks;
    }

    private static int VerifyAntenaEmitter(RoleVisuals visuals, string cue)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(RoleVisuals);
        var frame = (JsonElement)type.GetField("_primaryFrame", flags)!.GetValue(visuals)!;
        if (frame.ValueKind != JsonValueKind.Object || !frame.TryGetProperty("CgLayers", out var layers)) return 0;
        var carrier = layers.EnumerateObject().FirstOrDefault(p => p.Value.GetProperty("IsGlobalPosition").GetInt32() != 0
            && p.Value.GetProperty("PosX").GetSingle() == 36 && p.Value.GetProperty("PosY").GetSingle() == -360);
        var sprites = (Dictionary<string, Sprite2D>)type.GetField("_sprites", flags)!.GetValue(visuals)!;
        var document = (JsonDocument)type.GetField("_document", flags)!.GetValue(visuals)!;
        var frames = document.RootElement.GetProperty("motions").GetProperty(cue).GetProperty("frames");
        var beamFrame = frames[cue == "strike" ? 39 : 40];
        var stage = (RoleMotionEffects?)type.GetField("_primaryEffects", flags)!.GetValue(visuals);
        if (stage == null) return 0;
        var sequences = (System.Collections.IEnumerable)typeof(RoleMotionEffects).GetField("_sequences", flags)!.GetValue(stage)!;
        var beamEffects = beamFrame.GetProperty("Effects").EnumerateObject().Where(e => !e.Value.GetProperty("impact").GetBoolean()).ToArray();
        bool hasBeam = sequences.Cast<object>().Any(sequence => beamEffects.Any(e => e.Value.GetProperty("images")[0].GetString()
            == ((Texture2D[])sequence.GetType().GetField("Frames")!.GetValue(sequence)!)[0].ResourcePath));
        if (carrier.Value.ValueKind != JsonValueKind.Object)
        {
            if (hasBeam) throw new Exception("Antena retracted its UFO while its beam was still firing");
            return 0;
        }
        int checks = 0;
        string positions = "";
        foreach (var value in beamEffects)
        {
            var effect = value.Value;
            var firstTexture = effect.GetProperty("images")[0].GetString();
            foreach (var sequence in sequences)
            {
                var textures = (Texture2D[])sequence.GetType().GetField("Frames")!.GetValue(sequence)!;
                if (textures[0].ResourcePath != firstTexture) continue;
                var sprite = (Sprite2D)sequence.GetType().GetField("Sprite")!.GetValue(sequence)!;
                // Two identical beams are distinguished by their authored offset.
                var expected = visuals.GlobalTransform.BasisXform(new Vector2(effect.GetProperty("PosX").GetSingle() - 36,
                    effect.GetProperty("PosY").GetSingle() + 360));
                var actual = sprite.GlobalPosition - sprites[carrier.Name].GlobalPosition;
                positions += $" actual={actual} expected={expected} body={sprites[carrier.Name].GlobalPosition} effect={sprite.GlobalPosition}";
                if (actual.DistanceTo(expected) < .02f) { checks++; break; }
            }
        }
        if (hasBeam && checks != beamEffects.Length)
            throw new Exception("Antena beam no longer shares the original UFO emitter composition:" + positions);
        return checks;
    }
}
