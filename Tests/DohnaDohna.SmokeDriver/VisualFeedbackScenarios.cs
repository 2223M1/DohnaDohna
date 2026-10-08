using System.Reflection;
using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyVisualFeedback(Player owner)
    {
        var failures = new List<string>();
        await EnterBattle(owner);
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        var squad = SquadCombatState.Get(owner);
        var room = NCombatRoom.Instance!;
        var nodes = room.CreatureNodes.ToArray();
        var roots = nodes.ToDictionary(n => n, n => n.GetTransform());
        var hitboxes = nodes.ToDictionary(n => n.Hitbox, n => n.Hitbox.GetGlobalTransform());
        var hud = nodes.SelectMany(n => new[] { n.GetNode<Control>("%HealthBar"), n.IntentContainer,
                n.GetChildrenRecursive<NHealthBar>().Single().HpBarContainer,
                n.GetChildrenRecursive<NPowerContainer>().Single() })
            .ToDictionary(c => c, c => c.GetGlobalTransform());
        var background = room.Background;
        var backgroundBase = background.GetGlobalTransform();
        var visuals = (RoleVisuals)squad.GetActor("alyce").GetCreatureNode()!.Visuals;
        int moved = 0, hits = 0;
        var action = visuals.Play("special", () => { hits++; return Task.CompletedTask; }, squad.Cancellation.Token,
            owner.Creature.CombatState!.Enemies.ToArray(), cinematic: true);
        while (!action.IsCompleted)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var camera = room.GetNodeOrNull<CanvasLayer>("DohnaMotionCamera");
            if (camera != null && !camera.IsQueuedForDeletion() && !camera.Transform.IsEqualApprox(Transform2D.Identity))
            {
                moved++;
                var viewport = GetViewport().GetVisibleRect();
                var visible = camera.Transform.AffineInverse() * viewport;
                if (!viewport.Grow(.001f).Encloses(visible)) throw new Exception("Cinematic camera escaped baseline scene");
                // Native layout stays untouched; one shared render canvas carries
                // the scene, including nested HP/block/power controls and hit VFX.
                var renderRoots = (List<CanvasItem>)camera.GetType().GetField("_roots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(camera)!;
                if (!renderRoots.Contains(room.SceneContainer) || !renderRoots.Contains(room.CombatVfxContainer))
                    throw new Exception("Camera omitted native scene or hit VFX");
                if (moved == 15) await Screenshot("feedback-camera-hud.png");
            }
            if (roots.Any(pair => pair.Key.GetTransform() != pair.Value)
                || hitboxes.Any(pair => !pair.Key.GetGlobalTransform().IsEqualApprox(pair.Value))
                || hud.Any(pair => !pair.Key.GetGlobalTransform().IsEqualApprox(pair.Value))
                || !background.GetGlobalTransform().IsEqualApprox(backgroundBase))
                throw new Exception("Render-only camera changed native formation, HUD or layout coordinates");
        }
        await action;
        if (moved == 0 || hits != 1) throw new Exception("Camera fixture did not exercise scene motion and one hit");
        if (hud.Any(pair => !pair.Key.GetGlobalTransform().IsEqualApprox(pair.Value)))
            failures.Add("camera HUD not restored");
        MenuFlowProbe.AssertAlignment(squad, "camera-complete");
        GD.Print($"DOHNA_SMOKE_CAMERA_HUD_RESULT moving={moved} contained=True nativeLayoutUnchanged=True");

        // The actual native damage path: Thorns runs before the outer HP loss.
        // Exercise both a surviving encounter and the final enemy's victory boundary.
        var context = new BlockingPlayerChoiceContext();
        var actor = squad.Front;
        visuals = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
        var hurtField = typeof(RoleVisuals).GetField("_hurt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var hurtTime = typeof(RoleVisuals).GetField("_hurtTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await PowerCmd.Apply<ThornsPower>(context, actor, 99, actor, null);
        var enemies = owner.Creature.CombatState!.Enemies.ToArray();
        if (enemies.Length < 2) throw new Exception("Thorns fixture requires at least two enemies");
        for (int i = 0; i < enemies.Length; i++)
        {
            await CreatureCmd.SetCurrentHp(actor, 25);
            int hp = actor.CurrentHp;
            await CreatureCmd.Damage(context, owner.Creature, 3, ValueProp.Move, enemies[i]);
            bool before = hurtField.GetValue(visuals) != null;
            bool ended = await CombatManager.Instance.CheckWinCondition();
            for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            bool after = hurtField.GetValue(visuals) != null;
            double elapsed = (double)hurtTime.GetValue(visuals)!;
            bool last = i == enemies.Length - 1;
            GD.Print($"DOHNA_SMOKE_THORNS_HURT_RESULT last={last} hp={actor.CurrentHp} enemyDead={enemies[i].IsDead} before={before} after={after} elapsed={elapsed} ended={ended}");
            if (actor.CurrentHp != hp - 3 || !enemies[i].IsDead || ended != last)
                throw new Exception("Thorns fixture altered native HP/death outcome");
            if (!before || (!after && elapsed < .25) || elapsed <= 0) failures.Add($"Thorns hurt cut short: last={last}");
            await Screenshot(last ? "feedback-thorns-victory.png" : "feedback-thorns-nonfinal.png");
            for (int frame = 0; frame < 300 && visuals.IsPlaying; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (visuals.IsPlaying) failures.Add("Hurt did not return to idle");
        }
        // A victory reaction may outlive combat, never the displayed creature.
        await CreatureCmd.TriggerAnim(actor, "Hit", 0);
        var exitSounds = ActionSounds(visuals);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        for (int frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(visuals) || exitSounds.Any(sound => !sound.IsReleased))
            failures.Add("Room exit retained reaction node or sounds");
        else GD.Print("DOHNA_SMOKE_HURT_ROOM_EXIT_PASS");
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
    }
}
