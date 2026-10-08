using Godot;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2RitsuLib;
using STS2RitsuLib.RunData;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using DohnaDohna.Cards;
using DohnaDohna.Code.UI;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Factories;
using DohnaDohna.Code.Visuals;
using System.Text.Json;

namespace DohnaDohna.SmokeDriver;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public static void Initialize()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(Entry).Assembly);
        AudioProbe.Initialize();
        // Private, throwaway acceptance profile only, for command AND GUI fixtures.
        // No compatibility run may upload data or obscure evidence with consent UI.
        RitsuLibFramework.SetTelemetryApplicantConsent("NinjaSlayer", STS2RitsuLib.Telemetry.TelemetryConsentState.Denied, []);
        Node node = System.Environment.GetEnvironmentVariable("DOHNA_MENU_FLOW") == "1" ? new MenuFlowProbe() : new LoadProbe();
        tree.Root.CallDeferred(Node.MethodName.AddChild, node);
    }
}

public partial class LoadProbe : Node
{
    private bool _done;
    private double _time;
    private string? _respondChoice;
    private Task? _reviveInput;
    public override void _Process(double delta)
    {
        _time += delta;
        if (_respondChoice is { } response)
        {
            var panel = GetTree().Root.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen>().SingleOrDefault();
            var button = panel?.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NGridCardHolder>()
                .FirstOrDefault(b => b.CardModel is SquadReviveChoice option && option.RoleId == response);
            if (button != null) { _respondChoice = null; _reviveInput = ClickNativeChoice(button, "rest-revival"); }
        }
        if (_done || _time < 12 || NGame.Instance == null) return;
        _done = true;
        _ = RunProbe();
    }

    private async Task RunProbe()
    {
        try
        {
            if (System.Environment.GetEnvironmentVariable("DOHNA_COMMAND_SCENARIO") == "Display")
            {
                await VerifyDisplayModes();
                GD.Print("DOHNA_SMOKE_DISPLAY_PASS");
                return;
            }
            var character = ModelDb.Character<DohnaSquad>();
            var deck = character.StartingDeck.ToArray();
            if (deck.Length != 10) throw new Exception($"Starter count {deck.Length}");
            var cards = character.CardPool.AllCards.ToArray();
            if (cards.Length < 17) throw new Exception($"Pool count {cards.Length}");
            foreach (var card in cards) _ = card.TitleLocString.GetFormattedText();
            GD.Print($"DOHNA_SMOKE_REGISTRATION_PASS starter={deck.Length} pool={cards.Length}");
            foreach (var role in RoleDefinition.All)
            {
                using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(role.AssetRoot + "/motions.json"));
                var motion = data.RootElement.GetProperty("motions").GetProperty("strike");
                var frames = motion.GetProperty("frames").EnumerateArray().ToArray();
                var hits = Enumerable.Range(0, frames.Length).Where(i =>
                    frames[i].TryGetProperty("Damage", out var damage)
                    && damage.GetProperty("Enable").GetInt32() == 1
                    && damage.GetProperty("Percent").GetDouble() > 0).ToArray();
                if (motion.GetProperty("sourceMotion").GetString() != role.StrikeMotion || hits.Length != 1
                    || !motion.GetProperty("positiveHits").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(hits))
                    throw new Exception("Packaged default-first strike does not have exactly one original hit: " + role.Id);
                GD.Print($"DOHNA_SMOKE_STRIKE_MOTION_PASS {role.Id} source={role.StrikeMotion} hits=1");
            }
            SaveManager.Instance.MarkFtueAsComplete("combat_rules_ftue");
            SaveManager.Instance.MarkFtueAsComplete("shuffle_ftue");
            SaveManager.Instance.MarkFtueAsComplete("obtain_potion_ftue");
            SaveManager.Instance.MarkFtueAsComplete("rest_site_ftue");
            var menu = NGame.Instance!.MainMenu ?? throw new Exception("Main menu not ready");
            if (System.Environment.GetEnvironmentVariable("DOHNA_COMMAND_SCENARIO") == "Presentation")
                await VerifyPresentationSettingsUi();
            var selection = menu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
            selection.InitializeSingleplayer();
            menu.SubmenuStack.Push(selection);
            selection.SelectCharacter(selection.GetChildrenRecursive<NCharacterSelectButton>().Single(b => b.Character == character), character);
            SquadSelectPanel.Open(selection); // Command fixture; native input is tested separately by MenuFlowProbe.
            var panel = SquadSelectPanel.Get(selection)!;
            if (System.Environment.GetEnvironmentVariable("DOHNA_COMMAND_SCENARIO") == "Motion")
                await VerifySelectionSizes(selection, panel);
            foreach (var role in RoleDefinition.All)
            {
                var button = panel.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == role.Id);
                if (!button.IsEnabled || !button.GetChildrenRecursive<TextureRect>().Any(p => p.Texture != null)) throw new Exception("Unavailable role " + role.Id);
                button.Activated!(button);
                button.Activated!(button);
            }
            foreach (var role in new[] { "kuma", "alyce", "antena", "tora" })
            {
                var button = panel.GetChildrenRecursive<SquadRoleButton>().Single(b => b.Name == role);
                button.Activated!(button);
            }
            await Screenshot("selection.png");
            if (!panel.Commit()) throw new Exception("Four-member fixture could not be committed");
            GD.Print("DOHNA_SMOKE_TEN_ROLE_SELECTION_PASS");
            var run = await NGame.Instance!.StartNewSingleplayerRun(character, true,
                [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], [], "DOHNA-SQUAD-FIXTURE", GameMode.Standard);
            GD.Print("DOHNA_SMOKE_RUN_STARTED");
            var scenario = System.Environment.GetEnvironmentVariable("DOHNA_COMMAND_SCENARIO") ?? "Full";
            if (scenario != "Full")
            {
                var testOwner = run.Players.Single();
                await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
                if (scenario == "Boundaries") await VerifyBoundaryCases(testOwner, run);
                else if (scenario == "Presentation")
                {
                    await VerifyAllRoleDeaths(testOwner, run);
                    await VerifyWorldDeathPreference(testOwner, run);
                }
                else if (scenario == "Recovery") { await VerifyRestChoices(testOwner, run); await VerifyAncientRecovery(testOwner, run); }
                else if (scenario == "Lifecycle") await VerifyVisualLifecycle(testOwner);
                else if (scenario == "Motion") await VerifyUsedMotions(testOwner, run);
                else if (scenario == "Feedback") await VerifyVisualFeedback(testOwner);
                else if (scenario == "Finisher") await VerifyFinishers(testOwner);
                else if (scenario == "Routing") await VerifyHitRouting(testOwner);
                else if (scenario == "Alignment") await VerifyEmitterMatrix(testOwner);
                else if (scenario == "Shadows") await VerifyShadowPixels(testOwner);
                else if (scenario == "Impacts") await VerifyImpacts(testOwner, run);
                else if (scenario == "Pacing") await VerifyPacing(testOwner, run);
                else if (scenario == "Scaling") await VerifyScaling(testOwner, run);
                else if (scenario == "Catalog") await VerifyCatalog(testOwner, run);
                else if (scenario == "CatalogCards") await VerifyAllPrototypes(testOwner, run);
                else if (scenario == "Relics") await VerifyRoleRelics(testOwner, run);
                else if (scenario == "CatalogRules") await VerifyCatalogRules(testOwner, run);
                else if (scenario == "Costs") await VerifyNativeSubstitutionCosts(testOwner, run);
                else if (scenario == "Core") await VerifyCoreSquadRules(testOwner, run);
                else throw new Exception("Unknown command scenario: " + scenario);
                GD.Print("DOHNA_SMOKE_" + scenario.ToUpperInvariant() + "_PASS");
                await NGame.Instance.ReturnToMainMenu();
                return;
            }
            var owner = run.Players.Single();
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            await VerifyCatalog(owner, run);
            await VerifyRoleRelics(owner, run);
            await VerifyBoundaryCases(owner, run);
            await VerifyRestChoices(owner, run);
            await VerifyAncientRecovery(owner, run);
            // Includes a real unload/reload. No caller may reuse this run afterwards.
            await VerifyCatalogRules(owner, run);
            GD.Print("DOHNA_SMOKE_COMBAT_PASS");
        }
        catch (Exception exception)
        {
            GD.PrintErr("DOHNA_SMOKE_REGISTRATION_FAIL " + exception);
        }
        finally { AudioProbe.Save(); GetTree().Quit(); }
    }

    private async Task FinishCombat(MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        await CreatureCmd.Kill(owner.Creature.CombatState!.Enemies.ToArray(), true);
        await CombatManager.Instance.CheckWinCondition();
        if (SquadCombatState.TryGet(owner) != null) throw new Exception("Combat state not released");
        // Debug room jumps bypass the player's reward/proceed interval. Let the
        // native exhaust VFX finish its deferred pool return before removing the room.
        for (int i = 0; i < 300 && GetTree().Root.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustVfx>().Any(); i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task VerifyRepeatedFormation(SquadCombatState squad)
    {
        string folder = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "formation-30");
        System.IO.Directory.CreateDirectory(folder);
        var originalFront = squad.Front;
        for (int iteration = 0; iteration < 30; iteration++)
        {
            var previous = squad.Front;
            // Each pair restores the fixture: later death checks depend on its original order.
            var target = iteration % 2 == 0 ? squad.Living.ElementAt((iteration / 2) % 3) : originalFront;
            var movement = squad.Swap(target);
            int frame = 0;
            while (!movement.IsCompleted)
            {
                if (frame++ % 4 == 0 && DisplayServer.GetName() != "headless")
                    await Screenshot($"formation-30/swap-{iteration:00}-{frame:00}.png");
                else await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await movement;
            if (squad.Front != target || squad.Front == previous) throw new Exception("Repeated swap did not exchange actors");
            MenuFlowProbe.AssertAlignment(squad, "repeat-" + iteration);
            var roots = squad.Living.Select(c => c.GetCreatureNode()!.Position.X).ToArray();
            if (roots.Zip(roots.Skip(1)).Any(pair => pair.Second - pair.First < 150))
                throw new Exception("Formation roots overlapped after swap " + iteration);
        }
        GD.Print("DOHNA_SMOKE_THIRTY_SWAPS_PASS");
    }

    private async Task VerifyAncientRecovery(MegaCrit.Sts2.Core.Entities.Players.Player owner, RunState run)
    {
        var saved = SquadStore.Get(owner).Copy();
        int ascension = run.AscensionLevel;
        var originalAscensionManager = RunManager.Instance.AscensionManager;
        foreach (int level in new[] { 0, 1, 2, 10 })
        {
            // Test fixture only: exercise the real ancient event with each native ascension rule.
            typeof(RunState).GetProperty(nameof(RunState.AscensionLevel))!.SetValue(run, level);
            typeof(RunManager).GetProperty(nameof(RunManager.AscensionManager))!.SetValue(RunManager.Instance,
                new MegaCrit.Sts2.Core.Entities.Ascension.AscensionManager(level));
            var fixture = SquadRunState.Create(["kuma", "alyce", "antena", "tora"]);
            foreach (var member in fixture.Members) member.Hp = member.MaxHp = 25;
            fixture.Members[0].Hp = 0;
            fixture.Members[1].Hp = 5;
            fixture.Members[2].Hp = 0;
            fixture.Members[3].Hp = 9;
            SquadStore.State.Set(run, owner.NetId, fixture);
            owner.Creature.SetMaxHpInternal(25);
            owner.Creature.SetCurrentHpInternal(9);
            await RunManager.Instance.EnterRoomDebug(RoomType.Event,
                model: ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.Orobas>(), showTransition: false);
            var healed = SquadStore.Get(owner);
            int deadHp = level < 2 ? 25 : 20;
            int livingHp = level < 2 ? 25 : 21;
            if (!healed.Members.Select(m => m.RoleId).SequenceEqual(new[] { "kuma", "antena", "alyce", "tora" })
                || healed.Members.Take(2).Any(m => m.Hp != deadHp)
                || healed.Members.Skip(2).Any(m => m.Hp != livingHp))
                throw new Exception("Ancient recovery was not zero-HP native healing at A" + level + ": " + JsonSerializer.Serialize(healed));
            GD.Print($"DOHNA_SMOKE_ANCIENT_ZERO_HP_PASS A{level} revived={deadHp} living={livingHp}");
        }
        typeof(RunState).GetProperty(nameof(RunState.AscensionLevel))!.SetValue(run, ascension);
        typeof(RunManager).GetProperty(nameof(RunManager.AscensionManager))!.SetValue(RunManager.Instance, originalAscensionManager);
        SquadStore.State.Set(run, owner.NetId, saved);
        owner.Creature.SetMaxHpInternal(saved.Front!.MaxHp);
        owner.Creature.SetCurrentHpInternal(saved.Front.Hp);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
    }

    private async Task EnterBattle(MegaCrit.Sts2.Core.Entities.Players.Player owner, EncounterModel? encounter = null)
    {
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: encounter ?? ModelDb.Encounter<SlimesWeak>().ToMutable(), showTransition: false);
        for (int i = 0; i < 900 && (SquadCombatState.TryGet(owner)?.Actions.OpeningDone != true || owner.PlayerCombatState?.Phase != PlayerTurnPhase.Play); i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (owner.PlayerCombatState?.Phase != PlayerTurnPhase.Play) throw new Exception("Battle did not reach play phase");
    }

    private async Task RoleBatch(MegaCrit.Sts2.Core.Entities.Players.Player owner, string[] roles)
    {
        var context = new BlockingPlayerChoiceContext();
        var squad = SquadCombatState.Get(owner);
        var enemy = owner.Creature.CombatState!.Enemies.OrderByDescending(c => c.MaxHp).First();
        await CreatureCmd.GainMaxHp(enemy, 1000);
        foreach (var role in roles)
        {
            var actor = squad.GetActor(role);
            if (actor.GetCreatureNode()?.Visuals is not RoleVisuals) throw new Exception("Missing actor visual " + role);
            if (actor != squad.Front) await squad.Swap(actor);
            var strike = owner.Creature.CombatState.CreateCard<SquadStrike>(owner);
            await CardCmd.AutoPlay(context, strike, enemy);
            var signature = owner.Creature.CombatState.CreateCard(ModelDb.Character<DohnaSquad>().CardPool.AllCards.OfType<SquadAttackCard>().Single(c => c.FixedRole == role), owner);
            await CardCmd.AutoPlay(context, signature, enemy);
            if (System.Environment.GetEnvironmentVariable("DOHNA_AUDIO_TRACE") == "1")
            {
                var beforeHurt = actor.CurrentHp;
                await CreatureCmd.Damage(context, actor, 1, ValueProp.Unpowered | ValueProp.Unblockable, enemy);
                if (actor.CurrentHp == beforeHurt) await CreatureCmd.Damage(context, actor, 1, ValueProp.Unpowered | ValueProp.Unblockable, enemy);
                if (actor.CurrentHp >= beforeHurt) throw new Exception("Audio fixture did not produce native HP damage: " + role);
                GD.Print("DOHNA_AUDIO_NATIVE_HURT_PASS " + role);
            }
            MenuFlowProbe.AssertAlignment(squad, "role-attack-" + role);
            if (actor != squad.Front) throw new Exception("Signature changed formation");
            var cloned = owner.Creature.CombatState.CloneCard(signature);
            cloned.UpgradeInternal();
            if (cloned is not SquadAttackCard { FixedRole: { } copyRole } || copyRole != role) throw new Exception("Copy/upgrade changed owner");
            GD.Print("DOHNA_SMOKE_ROLE_ACTION_PASS " + role);
            await Screenshot("role-" + role + ".png");
        }
    }

    private async Task Screenshot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, name));
    }
}
