using System.Text.Json;
using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyPreparationEdges(Player owner)
    {
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var visual = (RoleVisuals)squad.Front.GetCreatureNode()!.Visuals;
        var card = owner.Creature.CombatState!.CreateCard<SquadStrike>(owner);
        await CardPileCmd.Add(card, PileType.Hand);
        var lease = new Node(); AddChild(lease);
        visual.Prepare(lease, card);
        await visual.Wait(.2, squad.Cancellation.Token);
        await visual.PlayHurt(squad.Cancellation.Token);
        await visual.Wait(.7, squad.Cancellation.Token);
        if (!visual.IsPreparing) throw new Exception("Nonlethal reaction lost held preparation");
        visual.EndPreparation(lease, true);
        // Production cancellation entry point, invoked as a method probe (not
        // claimed as mouse input). Its prefix must also work when the native
        // queue item was already removed by another cancellation path.
        var node = MegaCrit.Sts2.Core.Nodes.Cards.NCard.Create(card);
        NCardPlayQueue.Instance!.RemoveCardFromQueueForCancellation(node, false);
        await PreparationUntil(() => !visual.IsPreparing, "dequeued preparation");
        node.QueueFree();
        await visual.PlayHurt(squad.Cancellation.Token);
        await visual.Wait(.12, squad.Cancellation.Token);
        var previousFront = squad.Front;
        await squad.Swap(squad.Living.First(c => c != previousFront));
        var hurtField = typeof(RoleVisuals).GetField("_hurt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        if (hurtField.GetValue(visual) != null) throw new Exception("Pure swap stayed hidden behind hit recovery");
        await DohnaDohna.Code.Visuals.SquadFormation.Ensure(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance!)
            .FinishMovement(squad.Cancellation.Token);
        GD.Print("DOHNA_HURT_TO_SWAP_PASS");
        visual.Prepare(lease, card);
        await visual.Wait(.2, squad.Cancellation.Token);
        visual.EndPreparation(lease, false);
        await visual.Wait(.03, squad.Cancellation.Token);
        visual.Prepare(lease, card); // Redrag during the cancellation return.
        await visual.Wait(.1, squad.Cancellation.Token);
        if (!visual.IsPreparing) throw new Exception("Redrag did not retake preparation");
        await MegaCrit.Sts2.Core.Runs.RunManager.Instance.EnterRoomDebug(
            MegaCrit.Sts2.Core.Rooms.RoomType.RestSite, showTransition: false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(visual) && (visual.IsPreparing || visual.IsPlaying))
            throw new Exception("Room exit retained a preparation or action");
        if (SquadCombatState.TryGet(owner) != null) throw new Exception("Room exit retained squad state");
        lease.QueueFree();
        GD.Print("DOHNA_PREPARATION_EDGE_CHECK hurt/dequeue/redrag/exit");
    }

    private async Task PreparationKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PreparationUntil(Func<bool> predicate, string name)
    {
        for (int i = 0; i < 600; i++)
        {
            if (predicate()) return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await Screenshot("preparation-failure.png");
        GD.Print("DOHNA_PREP_INPUT_DIAGNOSTIC plays=" + NGame.Instance!.GetChildrenRecursive<NCardPlay>().Count()
            + " focus=" + GetViewport().GuiGetFocusOwner()?.GetPath());
        throw new Exception("Preparation input timed out: " + name);
    }

    private async Task VerifyPreparation(Player owner, RunState run)
    {
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        SaveManager.Instance.PrefsSave.KeyboardMode = true;
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        var covered = new HashSet<string>();
        string[][] groups = [["kuma", "alyce", "antena", "tora"], ["kikuchiyo", "medhico", "joker", "zappa"], ["kirakira", "porno", "kuma", "alyce"]];
        foreach (var group in groups)
        {
            SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
            await EnterBattle(owner);
            var squad = SquadCombatState.Get(owner);
            var combat = owner.Creature.CombatState!;
            foreach (var enemy in combat.Enemies) await CreatureCmd.GainMaxHp(enemy, 2000);
            await PlayerCmd.GainEnergy(100, owner);
            await CardPileCmd.Add(owner.PlayerCombatState!.Hand.Cards.ToArray(), PileType.Discard);
            foreach (var role in group.Where(covered.Add))
            {
                var actor = squad.GetActor(role);
                if (squad.Front != actor) await squad.Swap(actor);
                var visual = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
                foreach (string cue in new[] { "strike", "special", "cast" })
                {
                    CardModel card = cue switch
                    {
                        "strike" => combat.CreateCard<SquadStrike>(owner),
                        "special" => combat.CreateCard<SquadSpecial>(owner),
                        _ => combat.CreateCard<Defragment>(owner)
                    };
                    await CardPileCmd.Add(card, PileType.Hand);
                    await visual.Wait(.2, squad.Cancellation.Token);
                    // Both cancel and submit enter through native focused hand
                    // input. These are Godot keyboard events, not physical input.
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        while (visual.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var order = squad.Order.ToArray();
                        int energy = owner.PlayerCombatState.Energy;
                        var hp = combat.Creatures.ToDictionary(c => c, c => (c.CurrentHp, c.Block));
                        var sounds = ActionSounds(visual);
                        int startedSounds = AudioProbe.Starts.Values.Sum();
                        var holder = NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == card);
                        MotionMarker(role, "prepare-" + cue + (attempt == 0 ? "-cancel" : "-commit"), "start");
                        await PreparationKey(Key.Up);
                        holder.GrabFocus();
                        await PreparationKey(Key.Space);
                        await PreparationUntil(() => visual.IsPreparing, role + "/" + cue + " start");
                        // Include multiple complete hold cycles, not just the
                        // entry prefix that used to mask the frozen last frame.
                        await visual.Wait(attempt == 0 ? 1.65 : 2.1, squad.Cancellation.Token);
                        if (owner.PlayerCombatState.Energy != energy || !order.SequenceEqual(squad.Order)
                            || hp.Any(p => (p.Key.CurrentHp, p.Key.Block) != p.Value) || ActionSounds(visual).Except(sounds).Any()
                            || AudioProbe.Starts.Values.Sum() != startedSounds)
                            throw new Exception("Preparation changed gameplay or played audio: " + role + cue);
                        if (attempt == 0)
                        {
                            await PreparationKey(Key.Escape);
                            await PreparationUntil(() => !visual.IsPreparing, "cancel returns pose");
                            if (card.Pile?.Type != PileType.Hand || owner.PlayerCombatState.Energy != energy)
                                throw new Exception("Canceled preparation spent the card");
                        }
                        else
                        {
                            await PreparationKey(Key.Space);
                            if (role == "medhico" && cue == "special")
                            {
                                await PreparationUntil(() => NGame.Instance.GetChildrenRecursive<DohnaDohna.Code.UI.SquadAuxiliaryTarget>().Any(), "medic auxiliary input");
                                squad.Front.GetCreatureNode()!.Hitbox.GrabFocus();
                                await PreparationKey(Key.Space);
                            }
                            await PreparationUntil(() => card.Pile?.Type != PileType.Hand && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null,
                                "native submitted card");
                            if (owner.PlayerCombatState.Energy != energy - 1) throw new Exception("Prepared play did not spend exactly one energy");
                        }
                        await PreparationUntil(() => !visual.IsPlaying, "visual tail finishes");
                        MotionMarker(role, "prepare-" + cue + (attempt == 0 ? "-cancel" : "-commit"), "end");
                    }
                    // Early and mid-preparation takeover is a production-method
                    // probe, separate from the native input cases above.
                    foreach (double age in new[] { 0d, .12 })
                    {
                        var lease = new Node(); AddChild(lease);
                        visual.Prepare(lease, card);
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        await visual.Wait(age, squad.Cancellation.Token);
                        int hits = 0;
                        var prior = SquadPlaySelection.Current;
                        SquadPlaySelection.Current = new(card, actor, null);
                        MotionMarker(role, $"prepare-{cue}-early-{age:F2}", "start");
                        try
                        {
                            await visual.Play(cue, () => { hits++; return Task.CompletedTask; }, squad.Cancellation.Token,
                                [combat.Enemies.Last()]);
                        }
                        finally { SquadPlaySelection.Current = prior; lease.QueueFree(); }
                        if (hits != 1 || visual.IsPreparing) throw new Exception("Preparation takeover lost release or retained lease");
                        await PreparationUntil(() => !visual.IsPlaying, "early tail");
                        MotionMarker(role, $"prepare-{cue}-early-{age:F2}", "end");
                    }
                    GD.Print("DOHNA_PREPARATION_NATIVE_INPUT_PASS " + role + "/" + cue);
                }
            }
            await FinishCombat(owner);
        }
    }
}
