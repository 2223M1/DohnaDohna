using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Powers;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyNativeFlow(Player owner, RunState run)
    {
        SaveManager.Instance.PrefsSave.KeyboardMode = true;
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        // No opening Strength aura: the first cooperative attack must introduce
        // Strength while its own TemporaryStrength.BeforeApplied is still running.
        await SetCatalogRoster(owner, run, SquadRunState.Create(["alyce", "antena", "zappa", "tora"]));
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var tora = squad.GetActor("tora");
        var context = new ThrowingPlayerChoiceContext();
        foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
        var target = combat.Enemies.First();
        await PlayerCmd.GainEnergy(30, owner);
        await CardPileCmd.Add(owner.PlayerCombatState!.Hand.Cards.ToArray(), PileType.Draw);
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 0, "Tora first-strength reproduction precondition");

        async Task Play(CardModel card)
        {
            await CardPileCmd.Add(card, PileType.Hand);
            await PreparationUntil(() => NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Any(h => h.CardModel == card), "native-flow hand");
            await PreparationKey(Key.Up);
            NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == card).GrabFocus();
            await PreparationKey(Key.Space);
            target.GetCreatureNode()!.Hitbox.GrabFocus();
            await PreparationKey(Key.Space);
            await PreparationUntil(() => card.Pile?.Type == PileType.Discard
                && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "native discard " + card.Id);
            await PreparationUntil(() => NCard.FindOnTable(card) == null, "center card cleared " + card.Id);
            CatalogCheck(PileType.Play.GetPile(owner).IsEmpty, "native play pile and center view clear after " + card.Id);
        }

        var first = combat.CreateCard<SquadSpecial>(owner);
        await Play(first);
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 2 && tora.GetPowerAmount<SquadTemporaryStrength>() == 1,
            "first cooperative attack: card +1 and passive +1, distinct native sources");
        var upgraded = combat.CreateCard<SquadSpecial>(owner);
        upgraded.UpgradeInternal();
        await Play(upgraded);
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 4 && tora.GetPowerAmount<SquadTemporaryStrength>() == 3,
            "upgraded cooperative attack stacks +2, passive does not repeat Strength reward");
        int hp = target.CurrentHp;
        await Play(combat.CreateCard<SquadStrike>(owner));
        CatalogCheck(hp - target.CurrentHp == 10, "next native hand card resolves with correct Strength");
        await PowerCmd.Apply<ArtifactPower>(context, tora, 1, tora, null);
        await PowerCmd.Apply<ArtifactPower>(context, tora, 1, tora, null);
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 5, "another buff kind rewards once without recursive Strength");
        await PowerCmd.Remove<ArtifactPower>(tora);
        await Screenshot("native-flow-tora-discard.png");

        int turn = owner.PlayerCombatState.TurnNumber;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(owner, turn));
        await PreparationUntil(() => owner.PlayerCombatState.TurnNumber == turn + 1
            && owner.PlayerCombatState.Phase == PlayerTurnPhase.Play
            && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "turn after cooperative attack");
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 0 && !tora.Powers.OfType<TemporaryStrengthPower>().Any(),
            "native turn end removes both temporary Strength sources exactly once");
        await PowerCmd.Apply<StrengthPower>(context, tora, 2, tora, null);
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 3, "next turn re-arms Tora passive");
        await PlayerCmd.GainEnergy(10, owner);
        await Play(combat.CreateCard<SquadSpecial>(owner));
        CatalogCheck(tora.GetPowerAmount<StrengthPower>() == 4, "cooperative attack with existing passive stack also resolves");

        // Use the real empty-discard UI hotkey, not the button callback. This is
        // background Godot input, not a physical keyboard/mouse claim.
        await CardPileCmd.Add(PileType.Discard.GetPile(owner).Cards.ToArray(), PileType.Draw);
        async Task EmptyDiscard(string label)
        {
            Input.ParseInputEvent(new InputEventAction { Action = MegaInput.viewDiscardPile, Pressed = true });
            Input.FlushBufferedEvents();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventAction { Action = MegaInput.viewDiscardPile, Pressed = false });
            Input.FlushBufferedEvents();
            await PreparationUntil(() => NGame.Instance!.GetChildrenRecursive<NThoughtBubbleVfx>().Any(), "empty-discard bubble");
            var bubble = NGame.Instance!.GetChildrenRecursive<NThoughtBubbleVfx>().Single();
            var expected = NThoughtBubbleVfx.GetCreatureSpeechPosition(squad.Front);
            CatalogCheck(bubble.GlobalPosition.DistanceTo(expected) < .01f, "empty-discard bubble uses current front: " + label);
            await ToSignal(GetTree().CreateTimer(.45), SceneTreeTimer.SignalName.Timeout);
            await Screenshot("native-flow-bubble-" + label + ".png");
            await PreparationUntil(() => !GodotObject.IsInstanceValid(bubble), "native thought lifetime");
        }
        await EmptyDiscard("tora");
        await squad.Swap(squad.GetActor("alyce"));
        await DohnaDohna.Code.Visuals.SquadFormation.Ensure(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance!)
            .FinishMovement(squad.Cancellation.Token);
        await EmptyDiscard("alyce-after-swap");
        await CreatureCmd.Kill(squad.Front, true);
        await EmptyDiscard("after-front-death");

        async Task ExplicitSpeaker(Creature speaker, string label)
        {
            var expected = NThoughtBubbleVfx.GetCreatureSpeechPosition(speaker);
            var thought = NThoughtBubbleVfx.Create(label, speaker, 1)!;
            speaker.GetVfxContainer()!.AddChild(thought);
            var speech = NSpeechBubbleVfx.Create(label, speaker, 1, VfxColor.White)!;
            speaker.GetVfxContainer()!.AddChild(speech);
            CatalogCheck(thought.GlobalPosition.DistanceTo(expected) < .01f && speech.GlobalPosition.DistanceTo(expected) < .01f,
                "explicit speaker remains unchanged: " + label);
            await PreparationUntil(() => !GodotObject.IsInstanceValid(thought) && !GodotObject.IsInstanceValid(speech), "native dialogue lifetime");
        }
        await ExplicitSpeaker(squad.Living.First(), "rear ally");
        await ExplicitSpeaker(target, "enemy");
        var frontPosition = NThoughtBubbleVfx.GetCreatureSpeechPosition(squad.Front);
        var sharedSpeech = NSpeechBubbleVfx.Create("shared player speech", owner.Creature, 1)!;
        owner.Creature.GetVfxContainer()!.AddChild(sharedSpeech);
        CatalogCheck(sharedSpeech.GlobalPosition.DistanceTo(frontPosition) < .01f, "shared native speech also uses front");
        await PreparationUntil(() => !GodotObject.IsInstanceValid(sharedSpeech), "shared speech lifetime");
        await FinishCombat(owner);
        await VerifyVanillaPowerOwner();
    }
}
