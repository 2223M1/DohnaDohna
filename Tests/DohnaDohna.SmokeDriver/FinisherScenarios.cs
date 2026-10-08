using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using MegaCrit.Sts2.Core.Models;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyFinishers(Player owner)
    {
        var impactTrace = new HarmonyLib.Harmony("DohnaDohna.SmokeDriver.FinisherImpact");
        impactTrace.Patch(HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand), "Execute"),
            prefix: new HarmonyLib.HarmonyMethod(typeof(LoadProbe), nameof(TraceImpactCommand)));
        try
        {
        var context = new BlockingPlayerChoiceContext();
        await EnterBattle(owner);
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        var combat = owner.Creature.CombatState!;
        var squad = SquadCombatState.Get(owner);
        var alyce = squad.GetActor("alyce");
        var enemies = combat.Enemies.ToArray();
        await CreatureCmd.GainMaxHp(enemies[0], 100);
        await Observe(combat.CreateCard<AlyceSignature>(owner), enemies[0], false, "nonlethal");

        await CreatureCmd.SetCurrentHp(enemies[0], 1);
        await Observe(combat.CreateCard<AlyceSignature>(owner), enemies[0], false, "partial-kill");
        if (enemies[0].IsAlive || CombatManager.Instance.IsOverOrEnding) throw new Exception("Partial kill fixture failed");
        foreach (var enemy in enemies.Skip(1).SkipLast(1)) await CreatureCmd.Kill(enemy, true);
        var last = enemies[^1];
        await CreatureCmd.SetCurrentHp(last, 1);
        await PowerCmd.Apply<BufferPower>(context, last, 1, last, null);
        await Observe(combat.CreateCard<AlyceSignature>(owner), last, false, "buffer");
        if (last.CurrentHp != 1 || last.HasPower<BufferPower>()) throw new Exception("Forecast consumed buffer or changed damage");

        await CreatureCmd.GainBlock(last, 100, ValueProp.Unpowered, null);
        await Observe(combat.CreateCard<SquadStrike>(owner), last, false, "blocked");
        if (last.CurrentHp != 1) throw new Exception("Blocked attack lost HP");

        // Preserve Vigor during forecasting. Native PowerCmd refuses amount changes
        // once IsEnding, so the lethal attack retains 2 until combat cleanup.
        // Nonlethal consumption is checked by the Full boundary scenario.
        await PowerCmd.Apply<VigorPower>(context, alyce, 2, alyce, null);
        await Observe(combat.CreateCard<AlyceSignature>(owner), last, true, "last-enemy");
        if (last.IsAlive || alyce.GetPowerAmount<VigorPower>() != 2)
            throw new Exception($"Finisher changed native lethal outcome: hp={last.CurrentHp}, vigor={alyce.GetPowerAmount<VigorPower>()}");
        await CombatManager.Instance.CheckWinCondition();

        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        combat = owner.Creature.CombatState!;
        foreach (var enemy in combat.Enemies) await CreatureCmd.SetCurrentHp(enemy, 1);
        // A real native phase/death-summon prevention hook must suppress prediction.
        var blocker = await PowerCmd.Apply<InfestedPower>(context, combat.Enemies.First(), 1, null, null);
        var prediction = typeof(SquadActions).Assembly.GetType("DohnaDohna.Code.Squad.SquadFinisher")!
            .GetMethod("CanFinish", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var card = combat.CreateCard<SquadThunderclap>(owner);
        // Full production attack below covers clear-all. The blocker case is checked
        // without killing it, to avoid substituting a test summon/death algorithm.
        var play = new MegaCrit.Sts2.Core.Entities.Cards.CardPlay { Card = card, Player = owner, Target = null,
            ResultPile = MegaCrit.Sts2.Core.Entities.Cards.PileType.Discard,
            Resources = new() { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = true, PlayIndex = 0, PlayCount = 1 };
        var command = new MegaCrit.Sts2.Core.Commands.Builders.AttackCommand(9).FromCard(card, play).WithNoAttackerAnim().TargetingAllOpponents(combat);
        if ((bool)prediction.Invoke(null, [command, card, play, combat.Enemies.ToArray()])!)
            throw new Exception("Phase/summon blocker allowed a finisher");
        await PowerCmd.Remove(blocker!);
        GD.Print("DOHNA_SMOKE_FINISHER_PHASE_SUPPRESSION_PASS");
        await Observe(card, null, true, "aoe-clear");
        if (combat.Enemies.Any(c => c.IsAlive)) throw new Exception("AOE finisher did not clear enemies");
        await CombatManager.Instance.CheckWinCondition();

        async Task Observe(CardModel card, Creature? target, bool expected, string label)
        {
            if (expected)
            {
                GD.Print("DOHNA_SMOKE_FINISHER_EXTERNAL " + string.Join(",", owner.RunState.IterateHookListeners(combat)
                    .Where(m => m.GetType().Assembly != typeof(MegaCrit.Sts2.Core.Models.CardModel).Assembly
                        && m.GetType().Assembly != typeof(SquadActions).Assembly).Select(m => m.GetType().FullName)));
                GD.Print("DOHNA_SMOKE_FINISHER_PRIMARY " + string.Join(",", combat.Enemies.Select(e => $"{e.LogName}:{e.CurrentHp}:{e.Block}:primary={e.IsPrimaryEnemy}"))
                    + " stop=" + MegaCrit.Sts2.Core.Hooks.Hook.ShouldStopCombatFromEnding(combat));
            }
            bool cameraSeen = false, lightingSeen = false;
            var allies = squad.Living.Select(c => c.GetCreatureNode()!.Visuals.GetCurrentBody()).ToArray();
            var colors = allies.ToDictionary(n => n, n => n.Modulate);
            var roleVisuals = (RoleVisuals)squad.Actions.ActorFor(card).GetCreatureNode()!.Visuals;
            _impactCommand = null; _impactObserving = true; _impactTargets = target == null ? combat.Enemies.ToArray() : [target];
            var attack = CardCmd.AutoPlay(context, card, target);
            int frames = 0, motionFrames = 0;
            while (!attack.IsCompleted)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var camera = NCombatRoom.Instance!.GetNodeOrNull<CanvasLayer>("DohnaMotionCamera");
                if (camera != null && !camera.IsQueuedForDeletion())
                {
                    cameraSeen = true;
                    var viewport = GetViewport().GetVisibleRect();
                    if (!viewport.Grow(.001f).Encloses(camera.Transform.AffineInverse() * viewport))
                        throw new Exception("Finisher camera escaped baseline scene: " + label);
                }
                lightingSeen |= GetTree().Root.GetChildrenRecursive<ColorRect>().Any(n => n.Name == "DohnaActionLighting");
                if (!expected && (cameraSeen || lightingSeen || colors.Any(pair => pair.Key.Modulate != pair.Value)))
                    throw new Exception("Ordinary attack used cinematic effects: " + label);
                frames++;
                if (roleVisuals.IsPlaying && ++motionFrames == 45)
                {
                    if (label == "last-enemy" && alyce.GetPowerAmount<VigorPower>() != 2)
                        throw new Exception("Forecast consumed Vigor before impact");
                    await Screenshot("finisher-" + label + ".png");
                }
            }
            await attack;
            _impactObserving = false;
            if (_impactCommand == null || card is SquadAttackCard && (_impactCommand.HitSfx == null && _impactCommand.TmpHitSfx == null))
                throw new Exception("Finisher/native contact feedback double-play or omission: " + label);
            if (card is SquadThunderclap && (_impactCommand.HitVfx != "vfx/vfx_attack_slash"
                || _impactCommand.HitSfx != null || _impactCommand.TmpHitSfx != null))
                throw new Exception("Prototype Thunderclap no longer preserves its exact native contact configuration");
            var customHits = (System.Collections.ICollection)HarmonyLib.AccessTools.Field(_impactCommand.GetType(), "_customHitVfxNodes").GetValue(_impactCommand)!;
            if (_impactCommand.HitVfx == null && customHits.Count == 0) throw new Exception("Missing native finisher/contact VFX");
            if (cameraSeen != expected) throw new Exception($"Finisher eligibility {label}: expected={expected}, observed={cameraSeen}");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            MenuFlowProbe.AssertAlignment(squad, "finisher-" + label);
            if (GetTree().Root.GetChildrenRecursive<CanvasLayer>().Any(n => n.Name == "DohnaMotionCamera"))
                throw new Exception("Camera retained after action: " + label);
            GD.Print($"DOHNA_SMOKE_FINISHER_CASE_PASS {label} cinematic={cameraSeen} lighting={lightingSeen} frames={frames}");
        }
        }
        finally { _impactObserving = false; impactTrace.UnpatchAll(impactTrace.Id); }
    }
}
