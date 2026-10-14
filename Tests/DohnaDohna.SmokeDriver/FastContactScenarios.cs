using System.Reflection;
using Godot;
using HarmonyLib;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Patches;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static readonly List<(Creature Target, bool Pink)> ContactSparks = [];
    private static int _tracerConnections;
    private static void WitnessCannonSpark(Creature target, NHitSparkVfx? __result)
    {
        if (!_impactObserving || __result == null) return;
        var bright = __result.GetNode<GpuParticles2D>("vfx_common_specks_bright");
        ContactSparks.Add((target, bright.SelfModulate.IsEqualApprox(new Color(.909804f, .0899267f, .12156871f))));
    }

    private static void WitnessTracer(RoleMotionEffects __instance, IReadOnlyList<Creature> targets)
    {
        if (!_impactObserving) return;
        var sequences = (System.Collections.IEnumerable)AccessTools.Field(typeof(RoleMotionEffects), "_sequences").GetValue(__instance)!;
        foreach (var entry in sequences)
        {
            var sprite = (Sprite2D)entry.GetType().GetField("Sprite")!.GetValue(entry)!;
            if (sprite.Name != "DohnaGunTracer") continue;
            if (sprite.Scale.Y > 1) throw new Exception("Kuma trace still expands into a field-sized flash");
            Vector2 end = sprite.ToGlobal(new(sprite.Texture.GetWidth() - 13, 0));
            var victim = targets.SingleOrDefault(t => t.GetCreatureNode()!.VfxSpawnPosition.DistanceTo(end) < .1f);
            if (victim == null) throw new Exception("Tracer misses its actual native contact anchor: " + end);
            _tracerConnections++;
            GD.Print($"DOHNA_TRACER_ENDPOINT_PASS start={sprite.GlobalPosition} end={end} target={victim.GetCreatureNode()!.VfxSpawnPosition}");
        }
    }

    private async Task VerifyFastContacts(Player owner)
    {
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var enemies = combat.HittableEnemies.ToArray();
        var context = new BlockingPlayerChoiceContext();
        var patch = new Harmony("DohnaDohna.SmokeDriver.FastContacts");
        patch.Patch(AccessTools.Method(typeof(AttackCommand), nameof(AttackCommand.Execute)),
            prefix: new HarmonyMethod(typeof(LoadProbe), nameof(TraceImpactCommand)));
        patch.Patch(AccessTools.Method(typeof(NHitSparkVfx), nameof(NHitSparkVfx.Create), [typeof(Creature), typeof(bool)]),
            postfix: new HarmonyMethod(typeof(LoadProbe), nameof(WitnessCannonSpark)));
        patch.Patch(AccessTools.Method(typeof(RoleMotionEffects), "ContactTargets"),
            postfix: new HarmonyMethod(typeof(LoadProbe), nameof(WitnessTracer)));
        try
        {
            await Use("antena", combat.CreateCard<SquadStrike>(owner), enemies[0], cannon: true);
            await Use("antena", combat.CreateCard<SquadBallLightning>(owner), enemies[0], cannon: true);
            await Use("antena", combat.CreateCard<SquadThunderclap>(owner), null, cannon: true);
            // Tora is absent: the fallback must use the real front's feedback.
            await Use("antena", combat.CreateCard<SquadFlechettes>(owner), enemies[0], cannon: true);
            await CreatureCmd.LoseBlock(context, squad.GetActor("antena"), squad.GetActor("antena").Block, null);
            await PowerCmd.Apply<ThornsPower>(context, enemies[0], 1, enemies[0], null);
            await Use("antena", combat.CreateCard<SquadStrike>(owner), enemies[0], cannon: true, reflection: true);
            await PowerCmd.Remove(enemies[0].GetPower<ThornsPower>()!);
            await Use("kikuchiyo", combat.CreateCard<SquadStrike>(owner), enemies[0]);
            await Use("kikuchiyo", combat.CreateCard<SquadThunderclap>(owner), null, areaSlash: true);
            await Use("kikuchiyo", combat.CreateCard<SquadPommelStrike>(owner), enemies[0]);
            foreach (var enemy in enemies)
                foreach (bool special in new[] { false, true })
                    await Use("kuma", special ? combat.CreateCard<SquadSpecial>(owner) : combat.CreateCard<SquadStrike>(owner), enemy, tracer: true);
            GD.Print("DOHNA_FAST_CONTACTS_PASS");
        }
        finally { _impactObserving = false; patch.UnpatchAll(patch.Id); }

        async Task Use(string role, CardModel card, Creature? target, bool cannon = false,
            bool areaSlash = false, bool reflection = false, bool tracer = false)
        {
            if (squad.Front != squad.GetActor(role)) await squad.Swap(squad.GetActor(role));
            await SquadFormation.Ensure(NCombatRoom.Instance!).FinishMovement(squad.Cancellation.Token);
            _impactObserving = true; _impactCommand = null; ContactSparks.Clear(); _tracerConnections = 0;
            _impactTargets = target == null ? enemies : [target];
            string label = $"contact-{role}-{card.Id.Entry}-{Array.IndexOf(enemies, target)}" + (reflection ? "-reflection" : "");
            var nativeNodes = new List<string>();
            void Added(Node node) { if (node is Node2D) nativeNodes.Add(node.SceneFilePath); }
            NCombatRoom.Instance!.CombatVfxContainer.ChildEnteredTree += Added;
            MotionMarker(role, label, "start");
            bool captured = false;
            try
            {
                var play = CardCmd.AutoPlay(context, card, target);
                while (!play.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (!captured && ContactSparks.Any(t => t.Target.Side == CombatSide.Enemy))
                    { await Screenshot(label + ".png"); captured = true; }
                }
                await play;
                if (_impactCommand == null || !captured) throw new Exception("No real contact: " + label);
                if (cannon)
                {
                    if (_impactCommand.HitSfx != SquadCannonImpactPatch.HitSound
                        || ContactSparks.Count(s => s.Target.Side == CombatSide.Enemy && s.Pink) != _impactTargets.Length
                        || nativeNodes.Count(n => n.EndsWith("/vfx_fire_smoke_puff.tscn")) != _impactTargets.Length)
                        throw new Exception("Cannon contact count/palette mismatch: " + label);
                    if (ContactSparks.Any(s => s.Target.Side == CombatSide.Player && s.Pink))
                        throw new Exception("Cannon palette contaminated reflection");
                    if (reflection && !ContactSparks.Any(s => s.Target == squad.GetActor(role)))
                        throw new Exception("Reflection did not retain its true attacker target");
                }
                if (areaSlash && (_impactCommand.HitVfx != VfxCmd.giantHorizontalSlashPath
                    || _impactCommand.HitSfx != "event:/sfx/enemy/enemy_attacks/vantom/vantom_dismember"
                    || nativeNodes.Count(n => n.EndsWith("/vfx_giant_horizontal_slash.tscn")) != 1))
                    throw new Exception("Area slash was not exactly one native giant slash: " + string.Join(',', nativeNodes));
                if (role == "kikuchiyo" && !areaSlash && _impactCommand.TmpHitSfx != "slash_attack.mp3")
                    throw new Exception("Single sword contact not native slash");
                if (tracer && _tracerConnections != 0) throw new Exception("Ordinary Kuma shot restored imported tracer VFX: " + label);
                GD.Print($"DOHNA_FAST_CONTACT_PASS {label} cannon={cannon} areaSlash={areaSlash} sparks={ContactSparks.Count} tracer={_tracerConnections}");
                await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
                MotionMarker(role, label, "end");
            }
            finally { NCombatRoom.Instance!.CombatVfxContainer.ChildEnteredTree -= Added; _impactObserving = false; }
        }
    }
}
