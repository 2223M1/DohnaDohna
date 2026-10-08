using Godot;
using DohnaDohna.Content;
using DohnaDohna.Code.UI;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Entities.Cards;
using DohnaDohna.Cards;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace DohnaDohna.SmokeDriver;

/// <summary>Rendered, isolated GUI acceptance. Never calls SelectCharacter,
/// button signals, lobby.SetReady, or StartNewSingleplayerRun to reach the run.</summary>
public partial class MenuFlowProbe : Node
{
    private bool _started;
    private double _elapsed;
    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (!_started && _elapsed > 10 && NGame.Instance?.MainMenu != null)
        {
            _started = true;
            _ = Run();
        }
    }

    private async Task Run()
    {
        try
        {
            var roster = System.Environment.GetEnvironmentVariable("DOHNA_MENU_ROSTER")?.Split(',') ?? ["kuma", "alyce", "antena", "tora"];
            if (roster.Length != 4 || roster.Distinct().Count() != 4) throw new Exception("GUI fixture must specify four unique roles");
            foreach (var role in roster) _ = RoleDefinition.Get(role);
            // Test-profile-only first-time tutorial choice is not the feature under test.
            SaveManager.Instance.MarkFtueAsComplete("accept_tutorials_ftue");
            SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
            SaveManager.Instance.MarkFtueAsComplete("map_select_ftue");
            SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
            SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");
            var menu = NGame.Instance!.MainMenu!;
            // The inactive Win32 desktop cannot report an OS cursor position;
            // RitsuLib's main-menu scroller checks that position, not event.Position.
            // Use the host's keyboard navigation for this menu only. Selection,
            // native back/confirm and Neow still receive mouse events. Card play
            // uses the host's keyboard targeting, which also needs no OS cursor.
            SaveManager.Instance.PrefsSave.KeyboardMode = true;
            await KeyInput(Key.Up);
            menu.GetNode<Control>("MainMenuTextButtons/SingleplayerButton").GrabFocus();
            await KeyInput(Key.Space);
            if (menu.SubmenuStack.Peek() is NSingleplayerSubmenu submenu)
                await Click(submenu.GetNode<Control>("StandardButton"));
            await Until(() => menu.SubmenuStack.Peek() is NCharacterSelectScreen, "character selection");
            var screen = (NCharacterSelectScreen)menu.SubmenuStack.Peek()!;
            var gateway = screen.GetChildrenRecursive<NCharacterSelectButton>().Single(b => b.Character is DohnaSquad);
            await Click(gateway);
            if (SquadSelectPanel.Get(screen)?.IsSecondary == true) throw new Exception("Selecting gateway prematurely opened secondary selection");
            await Screenshot("level-one-dohna.png");
            await Click(screen.GetNode<Control>("ConfirmButton"));
            await Until(() => SquadSelectPanel.Get(screen)?.IsSecondary == true, "Dohna selection level");
            var selector = SquadSelectPanel.Get(screen)!;
            if (screen.GetNode<Control>("CharSelectButtons").Visible) throw new Exception("Primary character bar was not replaced");
            if (selector.GetChildrenRecursive<SquadRoleButton>().Count(b => b.Slot < 0) != 10)
                throw new Exception("Expected ten role buttons");
            if (screen.GetNode<NConfirmButton>("ConfirmButton").IsEnabled) throw new Exception("Confirm enabled before four selections");
            await Screenshot("level-two-empty.png");
            await Click(screen.GetNode<Control>("BackButton"));
            await Until(() => SquadSelectPanel.Get(screen)?.IsSecondary == false, "return to primary level");
            if (menu.SubmenuStack.Peek() != screen || !screen.GetNode<Control>("CharSelectButtons").Visible)
                throw new Exception("Back left character selection instead of returning one level");
            var vanilla = screen.GetChildrenRecursive<NCharacterSelectButton>().First(b => b.Character is not DohnaSquad && b.IsEnabled);
            await Click(vanilla);
            if (!screen.GetNode<NConfirmButton>("ConfirmButton").IsEnabled || SquadSelectPanel.Get(screen)?.IsSecondary == true)
                throw new Exception("Returning from Dohna broke vanilla selection");
            await Click(gateway);
            await Click(screen.GetNode<Control>("ConfirmButton"));
            await Until(() => SquadSelectPanel.Get(screen)?.IsSecondary == true, "re-enter Dohna selection");
            selector = SquadSelectPanel.Get(screen)!;
            foreach (var role in RoleDefinition.All)
            {
                var button = selector.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == role.Id);
                await Click(button);
                if (!button.IsSelected) throw new Exception("Role was not selectable: " + role.Id);
                await Click(button);
            }
            GD.Print("DOHNA_MENU_ALL_TEN_SELECTABLE_PASS");
            foreach (var role in roster)
                await Click(selector.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == role));
            if (!screen.GetNode<NConfirmButton>("ConfirmButton").IsEnabled) throw new Exception("Native right confirm did not enable");
            var fifth = RoleDefinition.All.First(r => !roster.Contains(r.Id)).Id;
            await Click(selector.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == fifth));
            if (selector.GetChildrenRecursive<SquadRoleButton>().Count(b => b.Slot < 0 && b.IsSelected) != 4) throw new Exception("Fifth member was accepted");
            await Click(selector.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == roster[3]));
            if (screen.GetNode<NConfirmButton>("ConfirmButton").IsEnabled) throw new Exception("Confirm remained enabled with three members");
            await Click(screen.GetNode<Control>("ConfirmButton"));
            if (NRun.Instance != null) throw new Exception("Three members could start a run");
            await Click(selector.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == roster[3]));
            GD.Print("DOHNA_MENU_FOUR_LIMIT_DESELECT_PASS");
            var slots = selector.GetChildrenRecursive<SquadRoleButton>().Where(b => b.Slot >= 0).OrderBy(b => b.Slot).ToArray();
            await Drag(slots[0], slots[3]);
            if (!slots.Select(b => b.Role!.Id).SequenceEqual(roster.Skip(1).Append(roster[0])))
                throw new Exception("Native drag did not reorder the four selected members");
            await Screenshot("level-two-reordered.png");
            await Drag(slots[3], slots[0]);
            if (!slots.Select(b => b.Role!.Id).SequenceEqual(roster)) throw new Exception("Reverse drag did not restore order");
            GD.Print("DOHNA_MENU_DRAG_REORDER_PASS");
            await Screenshot("level-two-four.png");
            GD.Print("DOHNA_MENU_SELECT_BACK_RESELECT_PASS");
            await Click(screen.GetNode<Control>("ConfirmButton"));
            await Until(() => NRun.Instance != null && RunManager.Instance.DebugOnlyGetState()?.CurrentRoom != null, "native embark", 30);
            var run = RunManager.Instance.DebugOnlyGetState()!;
            var owner = run.Players.Single();
            if (owner.Character is not DohnaSquad || !SquadStore.Get(owner).Members.Select(m => m.RoleId).SequenceEqual(roster))
                throw new Exception("Native embark did not preserve the selected four");
            await Screenshot("native-run-start.png");
            GD.Print("DOHNA_MENU_NATIVE_EMBARK_PASS");
            await ReachFirstBattle();
            var squad = SquadCombatState.Get(owner);
            if (squad.Living.Count() != 4 || owner.Creature.CombatState!.Players.Count != 1)
                throw new Exception("Native menu run did not create four members and one Player");
            await Screenshot("native-first-battle.png");
            AssertAlignment(squad, "opening");
            GD.Print("DOHNA_MENU_FIRST_BATTLE_PASS");
            var card = owner.PlayerCombatState!.Hand.Cards.FirstOrDefault(c => c is SquadDefend)
                ?? owner.PlayerCombatState.Hand.Cards.First(c => c is SquadStrike);
            var energy = owner.PlayerCombatState.Energy;
            var front = squad.Front;
            var beforeBlock = front.Block;
            var enemyHp = owner.Creature.CombatState.Enemies.Sum(e => e.CurrentHp);
            var holder = NGame.Instance.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == card);
            await KeyInput(Key.Up);
            holder.GrabFocus();
            await KeyInput(Key.Space);
            await Until(() => NGame.Instance.GetChildrenRecursive<NControllerCardPlay>().Any(), "native card target selection");
            await KeyInput(Key.Space);
            await Until(() => card.Pile?.Type != PileType.Hand && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "native card resolution", 20);
            if (owner.PlayerCombatState.Energy != energy - 1) throw new Exception("Native card input did not spend one energy");
            if (card is SquadDefend && front.Block <= beforeBlock) throw new Exception("Native defend did not block the front");
            if (card is SquadStrike && owner.Creature.CombatState.Enemies.Sum(e => e.CurrentHp) >= enemyHp) throw new Exception("Native strike did not damage an enemy");
            GD.Print("DOHNA_MENU_NATIVE_CARD_PASS " + card.Id);
            AssertAlignment(squad, "after-card");
            var swapped = await TrySwapThroughUi(owner, squad);
            var turn = owner.PlayerCombatState.TurnNumber;
            await Click(NGame.Instance.GetChildrenRecursive<NEndTurnButton>().Single(b => b.IsVisibleInTree() && b.IsEnabled));
            await Until(() => owner.PlayerCombatState.TurnNumber > turn && owner.PlayerCombatState.Phase == PlayerTurnPhase.Play
                && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "next player turn", 30);
            await Screenshot("native-next-turn.png");
            AssertAlignment(squad, "after-enemy-turn");
            GD.Print("DOHNA_MENU_NATIVE_END_TURN_PASS");
            if (!swapped && !await TrySwapThroughUi(owner, squad)) throw new Exception("Swap was not available across the first two native hands");
            await Screenshot("native-after-swap.png");
            await TestPotionArrows(owner, squad);
            if (squad.IsAlive("joker")) await TestDualTargets(owner, squad);
            GD.Print("DOHNA_MENU_FLOW_PASS");
        }
        catch (Exception exception)
        {
            await Screenshot("menu-failure.png");
            GD.PrintErr("DOHNA_MENU_FLOW_FAIL " + exception);
            DumpControls(NGame.Instance!, "FAILED_GUI");
        }
        finally { AudioProbe.Save(); GetTree().Quit(); }
    }

    internal static void AssertAlignment(SquadCombatState squad, string stage)
    {
        foreach (var actor in squad.Living)
        {
            var node = actor.GetCreatureNode()!;
            var visuals = (RoleVisuals)node.Visuals;
            var motionBody = visuals.GetNode<Node2D>("%Visuals/OriginalMotion");
            using var roleData = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(
                RoleDefinition.Get(squad.RoleOf(actor)).AssetRoot + "/motions.json"));
            var ground = roleData.RootElement.GetProperty("groundContact");
            if (ground[0].GetSingle() != 0 || motionBody.Position.X != 0)
                throw new Exception("Authored horizontal station was recentered: " + squad.RoleOf(actor));
            var station = motionBody.ToGlobal(new Vector2(ground[0].GetSingle(), ground[1].GetSingle()));
            Rect2? body = null;
            foreach (var sprite in motionBody.GetChildrenRecursive<Sprite2D>().Where(s => s.IsVisibleInTree() && s.Texture != null))
            {
                var used = (Rect2)sprite.Texture.GetImage().GetUsedRect();
                if (used.Size == Vector2.Zero) continue;
                if (sprite.FlipH) used.Position = new Vector2(sprite.Texture.GetWidth() - used.End.X, used.Position.Y);
                used.Position += sprite.Offset;
                var global = sprite.GetGlobalTransform() * used;
                body = body is { } previous ? previous.Merge(global) : global;
            }
            if (body is not { } drawn) throw new Exception("No drawn member body: " + squad.RoleOf(actor));
            var hp = node.GetChildrenRecursive<NHealthBar>().Single().HpBarContainer.GetGlobalRect();
            var power = node.GetChildrenRecursive<NPowerContainer>().Single().GetGlobalRect();
            var hitbox = node.Hitbox.GetGlobalRect();
            // Validate the original horizontal station, not a frame's bounding-box
            // center: Antena deliberately steps/leans past that point. The native
            // interaction region must still overlap her displayed silhouette.
            if (!hitbox.Intersects(drawn) || Math.Abs(hp.GetCenter().X - station.X) > 2
                || Math.Abs(hp.GetCenter().X - hitbox.GetCenter().X) > 2
                || Math.Abs(power.Position.X - hitbox.Position.X) > 2 || hp.Position.Y < station.Y - 2)
                throw new Exception($"Misaligned {squad.RoleOf(actor)} {stage}: body={drawn}, hp={hp}, powers={power}, hitbox={hitbox}");
            GD.Print($"DOHNA_MENU_ALIGNMENT_PASS {squad.RoleOf(actor)} {stage} bodyCenter={drawn.GetCenter().X} hpCenter={hp.GetCenter().X}");
        }
    }

    private async Task<bool> TrySwapThroughUi(MegaCrit.Sts2.Core.Entities.Players.Player owner, SquadCombatState squad)
    {
        var swap = owner.PlayerCombatState!.Hand.Cards.OfType<SquadSwap>().FirstOrDefault();
        if (swap == null) return false;
        var target = squad.Living.First();
        var oldFront = squad.Front;
        var energy = owner.PlayerCombatState.Energy;
        // Position-dependent auras/vigor and Tora's response to a new buff are
        // meant to change on a swap. Keep all other native powers on their owner;
        // dedicated Core/CatalogRules scenarios verify exact aura contributions.
        var snapshots = squad.Living.ToDictionary(c => c, c => (c.CurrentHp, c.Block,
            Powers: c.Powers.Where(p => p is not StrengthPower and not ThornsPower and not VigorPower and not DohnaDohna.Powers.SquadTemporaryStrength)
                .ToDictionary(p => p, p => p.Amount)));
        var holder = NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == swap);
        await KeyInput(Key.Up);
        holder.GrabFocus();
        await KeyInput(Key.Space);
        await Until(() => NGame.Instance!.GetChildrenRecursive<NControllerCardPlay>().Any(), "swap input target selection");
        target.GetCreatureNode()!.Hitbox.GrabFocus();
        await KeyInput(Key.Space);
        await Until(() => swap.Pile?.Type != PileType.Hand && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "swap resolution", 20);
        if (squad.Front != target || squad.Living.First() != oldFront || owner.PlayerCombatState.Energy != energy)
            throw new Exception("Native swap did not exchange the front and selected rear at zero cost");
        foreach (var (actor, before) in snapshots)
            if (actor.CurrentHp != before.CurrentHp || actor.Block != before.Block
                || before.Powers.Any(p => p.Key.Owner != actor || !actor.Powers.Contains(p.Key) || p.Key.Amount != p.Value))
                throw new Exception("Native swap transferred member state");
        AssertAlignment(squad, "after-swap");
        GD.Print("DOHNA_MENU_NATIVE_SWAP_PASS");
        return true;
    }

    private IEnumerable<T> VisibleEnabled<T>() where T : NClickableControl =>
        NGame.Instance!.GetChildrenRecursive<T>().Where(c => c.IsVisibleInTree() && c.IsEnabled
            && GetViewport().GetVisibleRect().HasPoint(c.GetGlobalRect().GetCenter()));

    private async Task TestPotionArrows(Player owner, SquadCombatState squad)
    {
        var game = NGame.Instance!;
        // Controlled inventory fixture, then native popup, arrow, action queue, and native effect.
        foreach (var actor in squad.Living.ToArray())
        {
            var result = await PotionCmd.TryToProcure<BlockPotion>(owner);
            if (!result.success) throw new Exception("Cannot prepare arrow fixture potion");
            int before = actor.Block;
            var holder = NGame.Instance!.GetChildrenRecursive<NPotionHolder>().Single(h => h.Potion?.Model == result.potion);
            await KeyInput(Key.Up);
            holder.GrabFocus();
            await KeyInput(Key.Space);
            await Until(() => game.GetChildrenRecursive<NPotionPopup>().Any(p => !p.IsMarkedForRemoval), "potion popup");
            var popup = game.GetChildrenRecursive<NPotionPopup>().Single(p => !p.IsMarkedForRemoval);
            popup.GetNode<Control>("%UseButton").GrabFocus();
            await KeyInput(Key.Space);
            await Until(() => NTargetManager.Instance.IsInSelection, "native member potion arrow");
            actor.GetCreatureNode()!.Hitbox.GrabFocus();
            await Screenshot("potion-arrow-" + squad.RoleOf(actor) + ".png");
            await KeyInput(Key.Space);
            await Until(() => !owner.Potions.Contains(result.potion) && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null, "potion resolution");
            if (actor.Block != before + 12) throw new Exception("Potion arrow did not deliver to " + squad.RoleOf(actor));
        }
        GD.Print("DOHNA_MENU_FOUR_POTION_ARROWS_PASS");
    }

    private async Task TestDualTargets(Player owner, SquadCombatState squad)
    {
        var game = NGame.Instance!;
        var context = new BlockingPlayerChoiceContext();
        var enemy = owner.Creature.CombatState!.Enemies.First();
        await CreatureCmd.GainMaxHp(enemy, 100);
        await PlayerCmd.GainEnergy(2, owner);
        var card = owner.Creature.CombatState.CreateCard<JokerSignature>(owner);
        await CardPileCmd.Add(card, PileType.Hand);
        await Wait(.3);
        var ally = squad.Living.First(c => c != squad.GetActor("joker"));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt == 2)
            {
                await CardPileCmd.Add(card, PileType.Hand);
                await PlayerCmd.GainEnergy(1, owner);
                await PowerCmd.Apply<DuplicationPower>(context, owner.Creature, 1, owner.Creature, null);
                await Wait(.3);
            }
            int energy = owner.PlayerCombatState!.Energy;
            int vigor = ally.GetPowerAmount<VigorPower>();
            var holder = NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == card);
            await KeyInput(Key.Up);
            holder.GrabFocus();
            await KeyInput(Key.Space);
            await Until(() => NTargetManager.Instance.IsInSelection, "dual enemy arrow");
            enemy.GetCreatureNode()!.Hitbox.GrabFocus();
            await KeyInput(Key.Space);
            await Until(() => game.GetChildrenRecursive<SquadAuxiliaryTarget>().Any() && NTargetManager.Instance.IsInSelection,
                "dual auxiliary arrow");
            await Screenshot("dual-arrow-" + attempt + ".png");
            if (attempt == 0)
            {
                await KeyInput(Key.Escape);
                await Until(() => !game.GetChildrenRecursive<SquadAuxiliaryTarget>().Any(), "dual selection cancellation");
                if (owner.PlayerCombatState.Energy != energy || card.Pile?.Type != PileType.Hand)
                    throw new Exception("Auxiliary cancellation spent card resources");
                GD.Print("DOHNA_MENU_DUAL_CANCEL_PASS");
            }
            else
            {
                ally.GetCreatureNode()!.Hitbox.GrabFocus();
                await KeyInput(Key.Space);
                await Until(() => card.Pile?.Type != PileType.Hand && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null,
                    "dual card resolution", 20);
                if (ally.GetPowerAmount<VigorPower>() != vigor + (attempt == 2 ? 4 : 2) || owner.PlayerCombatState.Energy != energy - 1)
                    throw new Exception("Dual selection did not preserve auxiliary target");
                GD.Print(attempt == 2 ? "DOHNA_MENU_DUAL_REPLAY_PASS" : "DOHNA_MENU_DUAL_TARGET_PASS");
            }
        }
    }

    private async Task ReachFirstBattle()
    {
        for (int step = 0; step < 12; step++)
        {
            await Until(() => !NGame.Instance!.Transition.InTransition, "native transition", 30);
            await Until(() => (CombatManager.Instance.IsInProgress
                    && RunManager.Instance.DebugOnlyGetState()!.Players.Single().PlayerCombatState?.Phase == PlayerTurnPhase.Play)
                || VisibleEnabled<NEventOptionButton>().Any(b => !b.Option.IsLocked)
                || VisibleEnabled<NProceedButton>().Any() || VisibleEnabled<NMapPoint>().Any()
                || VisibleEnabled<NClickableControl>().Any(b => b.Name == "DialogueHitbox"), "native playable room input", 30);
            await Wait(0.5);
            if (CombatManager.Instance.IsInProgress && RunManager.Instance.DebugOnlyGetState()!.Players.Single().PlayerCombatState?.Phase == PlayerTurnPhase.Play) return;
            var option = VisibleEnabled<NEventOptionButton>().FirstOrDefault(b => !b.Option.IsLocked);
            if (option != null) { await Click(option); continue; }
            var dialogue = VisibleEnabled<NClickableControl>().FirstOrDefault(b => b.Name == "DialogueHitbox");
            if (dialogue != null) { await Click(dialogue); continue; }
            var proceed = VisibleEnabled<NProceedButton>().FirstOrDefault();
            if (proceed != null) { await Click(proceed); continue; }
            var point = VisibleEnabled<NMapPoint>().OrderBy(p => p.Point.PointType == MapPointType.Monster ? 0 : 1).FirstOrDefault();
            if (point != null)
            {
                await KeyInput(Key.Up);
                point.GrabFocus();
                await KeyInput(Key.Space);
                continue;
            }
            DumpControls(NGame.Instance!, "RUN_GUI");
            throw new Exception("No native event/proceed/map input to reach the first battle");
        }
        throw new Exception("First battle required too many GUI steps");
    }

    private async Task Click(Control control)
    {
        await Wait(0.5);
        if (!control.IsVisibleInTree()) throw new Exception("Cannot click hidden control: " + control.GetPath());
        var point = control.GetGlobalRect().GetCenter();
        var viewport = control.GetViewport();
        GD.Print($"DOHNA_MENU_CLICK {control.GetPath()} rect={control.GetGlobalRect()} point={point} enabled={(control as NClickableControl)?.IsEnabled} mouse={control.MouseFilter} type={control.GetType().FullName}");
        var windowPoint = viewport.GetFinalTransform() * point;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = windowPoint, GlobalPosition = windowPoint, Relative = new Vector2(20, 0), Velocity = new Vector2(100, 0) });
        Input.FlushBufferedEvents();
        await Frames(3);
        GD.Print("DOHNA_MENU_HOVER " + viewport.GuiGetHoveredControl()?.GetPath());
        Input.ParseInputEvent(new InputEventMouseButton { Position = windowPoint, GlobalPosition = windowPoint, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        Input.FlushBufferedEvents();
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = windowPoint, GlobalPosition = windowPoint, ButtonIndex = MouseButton.Left, Pressed = false });
        Input.FlushBufferedEvents();
        await Wait(0.6);
    }

    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Drag(Control from, Control to)
    {
        var transform = GetViewport().GetFinalTransform();
        var start = transform * from.GetGlobalRect().GetCenter();
        var end = transform * to.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = start, GlobalPosition = start });
        Input.FlushBufferedEvents();
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start,
            ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        Input.FlushBufferedEvents();
        for (int i = 1; i <= 20; i++)
        {
            var point = start.Lerp(end, i / 20f);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point,
                Relative = (end - start) / 20, ButtonMask = MouseButtonMask.Left });
            Input.FlushBufferedEvents();
            await Frames(2);
        }
        if (!GetViewport().GuiIsDragging()) throw new Exception("Godot native drag did not start");
        Input.ParseInputEvent(new InputEventMouseButton { Position = end, GlobalPosition = end,
            ButtonIndex = MouseButton.Left, Pressed = false });
        Input.FlushBufferedEvents();
        await Wait(.5);
    }
    private async Task KeyInput(Key key)
    {
        GD.Print("DOHNA_MENU_KEYBOARD " + key);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        Input.FlushBufferedEvents();
        await Wait(0.6);
    }
    private async Task Wait(double seconds)
    {
        var until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < until) await Frames(1);
    }
    private async Task Until(Func<bool> condition, string label, double seconds = 8)
    {
        var until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!condition() && Time.GetTicksMsec() < until) await Frames(1);
        if (!condition()) throw new Exception("Timeout: " + label);
    }
    private async Task Screenshot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, name));
    }
    private static void DumpControls(Node root, string label)
    {
        foreach (var control in root.GetChildrenRecursive<Control>().Where(c => c.IsVisibleInTree() && c is NClickableControl))
            GD.Print($"DOHNA_{label} {control.GetPath()} rect={control.GetGlobalRect()} enabled={((NClickableControl)control).IsEnabled}");
    }
}
