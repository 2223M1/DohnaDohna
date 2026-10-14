using System.Reflection;
using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    // Actual native card commands and observed rendered nodes. This is not a
    // physical-input test; Preparation/Theater exercise the native input chain.
    private async Task VerifyMotionFlow(Player owner, RunState run)
    {
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        await SetCatalogRoster(owner, run, SquadRunState.Create(["kuma", "antena", "kikuchiyo", "zappa"]));
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var enemies = combat.HittableEnemies.ToArray();
        foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1500);
        var antenna = squad.GetActor("antena");
        var visual = (RoleVisuals)antenna.GetCreatureNode()!.Visuals;
        var context = new BlockingPlayerChoiceContext();
        async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Hold(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        Sprite2D[] Fields(Creature member) => member.GetCreatureNode()!.GetChildrenRecursive<Sprite2D>()
            .Where(s => s.Name == "DohnaProtection" && !s.IsQueuedForDeletion()).ToArray();
        int ShieldSounds() => AudioProbe.Starts.GetValueOrDefault("安缇娜_攻击３_048");

        await Hold(1.5);
        var saucer = (Sprite2D)typeof(RoleVisuals).GetField("_saucer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(visual)!;
        var roots = squad.Actors.ToDictionary(c => c, c => c.GetCreatureNode()!.Position);
        Vector2 initial = saucer.Position;
        float turn = saucer.Rotation;
        MotionMarker("antena", "hover", "start");
        await Hold(1.1);
        if (saucer.Position.DistanceTo(initial) < 1 || Math.Abs(saucer.Rotation - turn) < .001)
            throw new Exception("Persistent equipment stayed frozen");
        if (visual.SaucerMuzzle!.Value.DistanceTo(saucer.GetNode<Marker2D>("EmissionMuzzle").GlobalPosition) > .01
            || visual.SaucerAnchor!.Value.DistanceTo(saucer.GetNode<Marker2D>("OrbAnchor").GlobalPosition) > .01)
            throw new Exception("Muzzle is not the actual equipment transform");
        if (roots.Any(p => p.Key.GetCreatureNode()!.Position != p.Value)) throw new Exception("Hover moved a creature root");
        await Screenshot("motion-flow-hover.png");
        MotionMarker("antena", "hover", "end");

        foreach (bool group in new[] { false, true })
        {
            CardModel card = group ? combat.CreateCard<SquadRally>(owner) : combat.CreateCard<SquadGlacier>(owner);
            var before = squad.Living.ToDictionary(c => c, c => c.Block);
            int sounds = ShieldSounds();
            MotionMarker("antena", group ? "shield-team" : "shield-self", "start");
            await CardCmd.AutoPlay(context, card, null);
            await Hold(.12);
            foreach (var member in squad.Living)
            {
                bool expected = group || member == antenna;
                if (Fields(member).Length != 0 || (member.Block > before[member]) != expected)
                    throw new Exception("Removed shield changed native block or still appeared: " + squad.RoleOf(member));
            }
            if (ShieldSounds() != sounds) throw new Exception("Removed shield still emitted its original sound");
            await Screenshot(group ? "motion-flow-shield-team.png" : "motion-flow-shield-self.png");
            if (group)
            {
                CombatManager.Instance.Pause();
                await Frame();
                var pausedSaucer = saucer.Transform;
                await Hold(.2);
                if (saucer.Transform != pausedSaucer) throw new Exception("Hover ignored pause");
                CombatManager.Instance.Unpause();
            }
            await Hold(2.5);
            if (squad.Actors.Any(c => Fields(c).Length != 0)) throw new Exception("Protection failed to expire");
            MotionMarker("antena", group ? "shield-team" : "shield-self", "end");
        }
        int noShield = ShieldSounds();
        MotionMarker("antena", "nonprotective-casts", "start");
        foreach (var card in new CardModel[] { combat.CreateCard<SquadCoolheaded>(owner), combat.CreateCard<SquadDefragment>(owner) })
        {
            await CardCmd.AutoPlay(context, card, null);
            if (squad.Actors.Any(c => Fields(c).Length != 0) || ShieldSounds() != noShield)
                throw new Exception("Orb/draw/power cast falsely displayed protection");
        }
        await visual.PlayHurt(squad.Cancellation.Token);
        await Hold(1.8);
        MotionMarker("antena", "nonprotective-casts", "end");

        var kiku = squad.GetActor("kikuchiyo");
        foreach (bool front in new[] { false, true })
        {
            if (front) await squad.Swap(kiku);
            foreach (bool area in new[] { false, true })
            {
                CardModel card = area ? combat.CreateCard<SquadThunderclap>(owner) : combat.CreateCard<SquadStrike>(owner);
                // Public cards follow the actual front. For rear-position
                // coverage use the production visual with explicit targets;
                // front-position samples below are real card commands.
                var actor = (RoleVisuals)kiku.GetCreatureNode()!.Visuals;
                var targets = area ? enemies : [enemies[0]];
                string label = $"slash-{(front ? "front-card" : "rear-visual")}-{(area ? "area" : "single")}";
                MotionMarker("kikuchiyo", label, "start");
                bool captured = false;
                Task action = front ? CardCmd.AutoPlay(context, card, area ? null : enemies[0])
                    : actor.Play(area ? "special" : "strike", () => { actor.ContactEmissions(targets); return Task.CompletedTask; }, squad.Cancellation.Token, targets);
                for (int i = 0; i < 300 && (!action.IsCompleted || actor.IsPlaying); i++)
                {
                    await Frame();
                    var trails = NCombatRoom.Instance!.GetChildrenRecursive<Sprite2D>()
                        .Where(s => s.Name.ToString().StartsWith("DohnaContactTrail") && !s.IsQueuedForDeletion()).ToArray();
                    if (trails.Length != 0 || NCombatRoom.Instance.GetChildrenRecursive<RoleMotionEffects>().Any(e => e.HasEmissions))
                        throw new Exception("Ordinary slash still emitted imported VFX: " + label);
                    if (!captured && action.IsCompleted)
                    { await Screenshot("motion-flow-" + label + ".png"); captured = true; }
                }
                await action;
                if (!captured) throw new Exception("No completed slash sample: " + label);
                await Hold(2);
                MotionMarker("kikuchiyo", label, "end");
            }
        }
        // Cinematic method probe, not a claim of lethal damage. Keep the
        // imported path light there, while real lethal eligibility is covered
        // by Finisher. Sampling native contact also exercises the action lease.
        var finisherVisual = (RoleVisuals)kiku.GetCreatureNode()!.Visuals;
        MotionMarker("kikuchiyo", "finisher-source-slash", "start");
        var finish = finisherVisual.Play("strike", () =>
        {
            if (RoleFinisher.Current?.OwnsWindup(kiku) == true)
                throw new Exception("Released finisher still authorizes a new action's original effects");
            finisherVisual.ContactEmissions(enemies);
            return Task.CompletedTask;
        }, squad.Cancellation.Token, enemies, cinematic: true);
        var stage = RoleFinisher.Current ?? throw new Exception("No finisher windup to verify");
        if (!stage.OwnsWindup(kiku) || stage.OwnsWindup(antenna))
            throw new Exception("Finisher effect permission does not belong to its actor");
        var previousPlay = SquadPlaySelection.Current;
        try
        {
            SquadPlaySelection.Current = new(combat.CreateCard<SquadStrike>(owner), kiku, null);
            if (stage.OwnsWindup(kiku)) throw new Exception("Different play inherited finisher effect permission");
        }
        finally { SquadPlaySelection.Current = previousPlay; }
        bool finisherTrail = false;
        while (!finish.IsCompleted || finisherVisual.IsPlaying || RoleFinisher.Current != null)
        {
            await Frame();
            if (!finisherTrail && NCombatRoom.Instance!.GetChildrenRecursive<Sprite2D>().Any(s => s.Name == "DohnaContactTrail"))
            { finisherTrail = true; await Screenshot("motion-flow-finisher-source-slash.png"); }
        }
        await finish;
        if (!finisherTrail) throw new Exception("Finisher lost its retained original path slash");
        MotionMarker("kikuchiyo", "finisher-source-slash", "end");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadRally>(owner), null);
        await CreatureCmd.Kill(antenna, true);
        await Hold(.3);
        if (squad.Actors.Any(c => Fields(c).Length != 0)) throw new Exception("Source death retained protection");
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        await Frame(); await Frame();
        if (GetTree().Root.GetChildrenRecursive<RoleMotionEffects>().Any()
            || GetTree().Root.GetChildrenRecursive<Sprite2D>().Any(s => s.Name == "DohnaProtection"))
            throw new Exception("Room exit retained owned effect nodes");
        if (AudioProbe.RoomVoiceHandles.Any(h => h.IsValid && h.RawInstance!.Call("get_playback_state").AsInt32() != 2))
            throw new Exception("Room exit retained owned audio");
        GD.Print("DOHNA_MOTION_FLOW_CHECK native-block/no-source-shield/hover/anchors/pause/no-ordinary-slash/finisher-source-slash/death/exit");
    }
}
