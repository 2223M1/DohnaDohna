using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static void AssertMemberScale(Creature actor, float factor, string label)
    {
        var node = actor.GetCreatureNode() ?? throw new Exception("Missing scaled member node");
        var visuals = node.Visuals;
        var expected = Vector2.One * visuals.DefaultScale * factor;
        if (!visuals.Scale.IsEqualApprox(expected) || node.Scale != Vector2.One
            || node.GetNode<Control>("%HealthBar").Scale != Vector2.One)
            throw new Exception($"Scale/HUD mismatch {label}: visual={visuals.Scale}, expected={expected}, root={node.Scale}");
        var body = visuals.GetNode<Node2D>("%Visuals/OriginalMotion");
        var shadows = ((IEnumerable<Sprite2D>)typeof(RoleVisuals)
            .GetField("_shadows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(visuals)!).Where(s => s.Visible).ToArray();
        if (!body.GlobalScale.IsEqualApprox(visuals.GlobalScale) || shadows.Length == 0
            || shadows.Any(s => !visuals.IsAncestorOf(s)
                || !s.GlobalScale.IsEqualApprox(visuals.GlobalScale * s.Scale)))
            throw new Exception("Body/shadow failed to inherit native scale: " + label);
    }

    private async Task VerifyScaling(Player owner, RunState run)
    {
        var context = new BlockingPlayerChoiceContext();
        await EnterBattle(owner, ModelDb.Encounter<ShrinkerBeetleWeak>().ToMutable());
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var beetle = combat.Enemies.Single();
        foreach (var actor in squad.Living)
            foreach (var power in actor.Powers.ToArray()) await PowerCmd.Remove(power);
        var shrunk = squad.Front;
        var positions = squad.Actors.ToDictionary(c => c, c => c.GetCreatureNode()!.Position);
        await Screenshot("scale-before-beetle.png");

        // Let the real monster take its actual first turn; no stand-in attack or power callback.
        int turn = owner.PlayerCombatState!.TurnNumber;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(owner, turn));
        for (int i = 0; i < 1800; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (owner.PlayerCombatState.TurnNumber > turn && owner.PlayerCombatState.Phase == PlayerTurnPhase.Play
                && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null) break;
        }
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        squad.Layout(); // Native reflow must not incorporate temporary body scale.
        if (!shrunk.HasPower<ShrinkPower>() || owner.Creature.HasPower<ShrinkPower>()
            || squad.Living.Where(c => c != shrunk).Any(c => c.HasPower<ShrinkPower>()))
            throw new Exception("Real beetle shrink did not belong exclusively to its original front target");
        foreach (var actor in squad.Living)
        {
            AssertMemberScale(actor, actor == shrunk ? .5f : 1, "beetle");
            if (actor.GetCreatureNode()!.Position != positions[actor]) throw new Exception("Shrink moved a formation root");
        }
        await Screenshot("scale-after-beetle.png");
        int hp = beetle.CurrentHp;
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), beetle);
        if (hp - beetle.CurrentHp != 4) throw new Exception("Native 30% shrink damage reduction not applied to 6 damage strike");
        var healthy = squad.GetActor("tora");
        await squad.Swap(healthy);
        AssertMemberScale(shrunk, .5f, "after-swap");
        AssertMemberScale(healthy, 1, "after-swap-other");
        hp = beetle.CurrentHp;
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), beetle);
        if (hp - beetle.CurrentHp != 6) throw new Exception("Shrunk member affected healthy front attack");
        hp = beetle.CurrentHp;
        var signature = combat.CreateCard(ModelDb.Card<AntenaSignature>(), owner);
        if (squad.RoleOf(shrunk) != "antena") throw new Exception("Unexpected opening formation for shrink fixture");
        await CardCmd.AutoPlay(context, signature, beetle);
        if (hp - beetle.CurrentHp != 6) throw new Exception("Rear signature did not retain native shrink damage reduction");
        AssertMemberScale(shrunk, .5f, "after-rear-attack");
        GD.Print("DOHNA_SMOKE_BEETLE_SCALE_DAMAGE_PASS realEnemyTurn=True strike=4 healthy=6 rearSignature=6");

        // The actual monster move also exercises native Artifact before the power's AfterApplied.
        await PowerCmd.Apply<ArtifactPower>(context, healthy, 1, healthy, null);
        var shrinkMove = typeof(ShrinkerBeetle).GetMethod("ShrinkMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await (Task)shrinkMove.Invoke(beetle.Monster, [new Creature[] { owner.Creature }])!;
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        if (healthy.HasPower<ArtifactPower>() || healthy.HasPower<ShrinkPower>()) throw new Exception("Artifact failed native shrink blocking");
        AssertMemberScale(healthy, 1, "artifact");
        await FinishCombat(owner);
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        if (shrunk.HasPower<ShrinkPower>()) throw new Exception("Killing beetle did not remove shrink");
        AssertMemberScale(shrunk, 1, "beetle-death");
        GD.Print("DOHNA_SMOKE_SHRINK_ARTIFACT_REMOVAL_PASS");
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);

        // Native event-room obtain: existing rule distributes +20 as four +5s.
        var beforeHp = SquadStore.Get(owner).Members.ToDictionary(m => m.RoleId, m => m.MaxHp);
        var mushroom = await RelicCmd.Obtain<BigMushroom>(owner);
        if (SquadStore.Get(owner).Members.Any(m => m.MaxHp != beforeHp[m.RoleId] + 5))
            throw new Exception("Big Mushroom changed established per-member max HP allocation");
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        foreach (var actor in squad.Actors) AssertMemberScale(actor, 1.5f, "mushroom-after-room");
        foreach (var actor in squad.Actors)
        {
            var hud = actor.GetCreatureNode()!.GetNode<Control>("%HealthBar");
            if (hud.GetGlobalRect().Position.X < 0 || hud.GetGlobalRect().End.X > GetViewport().GetVisibleRect().End.X)
                throw new Exception("Big Mushroom pushed a member health bar outside the viewport");
        }
        if (mushroom.ModifyHandDraw(owner, 5) != 3) throw new Exception("Mushroom draw penalty changed");
        await Screenshot("scale-big-mushroom.png");
        GD.Print("DOHNA_SMOKE_MUSHROOM_ENTER_PASS members=4 scale=1.5 maxHpGain=5 draw=3");

        // Native scale replacement, NOT multiplication: 1.5 -> .5 -> 1.
        // Next room reapplies the relic's 1.5, exactly as for vanilla characters.
        var subject = squad.Front;
        var largePositions = squad.Actors.ToDictionary(c => c, c => c.GetCreatureNode()!.Position);
        await PowerCmd.Remove<ArtifactPower>(subject);
        var enemy = owner.Creature.CombatState!.Enemies.First();
        await PowerCmd.Apply<ShrinkPower>(context, owner.Creature, -1, enemy, null);
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        AssertMemberScale(subject, .5f, "mushroom-plus-shrink");
        squad.Layout();
        if (squad.Actors.Any(c => c.GetCreatureNode()!.Position != largePositions[c]))
            throw new Exception("Shrink reflow changed the original formation slots");
        await PowerCmd.Remove<ShrinkPower>(subject);
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        AssertMemberScale(subject, 1, "mushroom-shrink-removed");
        squad.Layout();
        if (squad.Actors.Any(c => c.GetCreatureNode()!.Position != largePositions[c]))
            throw new Exception("Shrink removal reflow changed the original formation slots");
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);

        // All remaining character art inherits the same host transform. No per-role scale algorithm.
        string[][] groups = [["kuma", "alyce", "antena", "tora"],
            ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "kuma", "tora"]];
        foreach (var group in groups)
        {
            SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
            await EnterBattle(owner);
            squad = SquadCombatState.Get(owner);
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            foreach (var actor in squad.Actors) AssertMemberScale(actor, 1.5f, "all-role-grow");
            foreach (var actor in squad.Actors)
            {
                await PowerCmd.Remove<ArtifactPower>(actor);
                await PowerCmd.Apply<ShrinkPower>(context, actor, 1, null, null);
            }
            await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            foreach (var actor in squad.Actors) AssertMemberScale(actor, .5f, "all-role-shrink");
            await Screenshot("scale-group-" + group[0] + ".png");
            foreach (var actor in squad.Actors) await PowerCmd.Remove<ShrinkPower>(actor);
            await FinishCombat(owner);
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        }

        // Obtaining the relic after members already exist exercises its precise Grow patch.
        await RelicCmd.Remove(mushroom);
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        foreach (var actor in squad.Actors) AssertMemberScale(actor, 1, "without-relic");
        var maxBefore = squad.Actors.ToDictionary(c => c, c => c.MaxHp);
        var rootsBefore = squad.Actors.ToDictionary(c => c, c => c.GetCreatureNode()!.Position);
        var hudBefore = squad.Actors.ToDictionary(c => c, c => c.GetCreatureNode()!.GetNode<Control>("%HealthBar").GlobalPosition);
        await RelicCmd.Obtain<BigMushroom>(owner);
        await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        squad.Layout();
        foreach (var actor in squad.Actors)
        {
            if (actor.MaxHp != maxBefore[actor] + 5) throw new Exception("Mid-combat mushroom HP allocation changed");
            AssertMemberScale(actor, 1.5f, "mid-combat-obtain");
            if (actor.GetCreatureNode()!.Position != rootsBefore[actor]
                || actor.GetCreatureNode()!.GetNode<Control>("%HealthBar").GlobalPosition != hudBefore[actor])
                throw new Exception("Mushroom changed original member/HUD position");
        }
        await squad.Swap(squad.Living.First());
        foreach (var actor in squad.Actors) AssertMemberScale(actor, 1.5f, "large-swap");
        await Screenshot("scale-midcombat-obtain-swap.png");
        await FinishCombat(owner);
        GD.Print("DOHNA_SMOKE_MUSHROOM_ALL_ROLES_PASS growth,shrink,removal,room-entry,midcombat,swap fixedFormation=True");
    }
}
