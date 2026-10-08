using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.UI;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Settings;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static ToggleModSettingsEntryDefinition DeathPreference() => ModSettingsRegistry.GetPages()
        .Single(p => p.ModId == "DohnaDohna").Sections.Single(s => s.Id == "presentation")
        .Entries.OfType<ToggleModSettingsEntryDefinition>().Single(e => e.Id == "female_death_cut_in");

    private async Task VerifyWorldDeathPreference(Player owner, RunState run)
    {
        if (owner.Creature.CombatState != null) throw new Exception("World death fixture must be outside combat");
        var saved = SquadStore.Get(owner).Copy();
        var preference = DeathPreference().Binding;
        bool previous = preference.Read();
        var female = new HashSet<string> { "alyce", "antena", "kikuchiyo", "medhico", "kirakira", "porno" };
        try
        {
            foreach (bool enabled in new[] { false, true })
            {
                preference.Write(enabled);
                foreach (var role in RoleDefinition.All)
                {
                    var rear = RoleDefinition.All.Where(r => r.Id != role.Id).Take(3).Select(r => r.Id).ToArray();
                    var fixture = SquadRunState.Create(rear.Append(role.Id));
                    fixture.Front!.Hp = 1;
                    SquadStore.State.Set(run, owner.NetId, fixture);
                    owner.Creature.SetMaxHpInternal(25);
                    owner.Creature.SetCurrentHpInternal(1);
                    bool sawPoster = false;
                    // Exercise native out-of-combat loss/ShouldDie/AfterDeath, not the presentation callback.
                    var death = CreatureCmd.Damage(new BlockingPlayerChoiceContext(), owner.Creature, 2,
                        ValueProp.Unpowered | ValueProp.Unblockable, null, null, null);
                    while (!death.IsCompleted)
                    {
                        sawPoster |= GetTree().Root.GetChildrenRecursive<DeathCutIn>().Any();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    await death;
                    var state = SquadStore.Get(owner);
                    if (sawPoster != (enabled && female.Contains(role.Id)) || state.IsAlive(role.Id)
                        || state.Front?.RoleId != rear[^1] || owner.Creature.CurrentHp != 25
                        || state.Members.Where(m => m.RoleId != role.Id).Any(m => m.Hp != 25))
                        throw new Exception($"World death preference/succession mismatch: {role.Id}, setting={enabled}, poster={sawPoster}");
                    // The cut-in owns a deferred visual tail; it must not be mistaken for the next death.
                    for (int i = 0; i < 180 && GetTree().Root.GetChildrenRecursive<DeathCutIn>().Any(); i++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (GetTree().Root.GetChildrenRecursive<DeathCutIn>().Any()) throw new Exception("World death cut-in leaked");
                    GD.Print($"DOHNA_SMOKE_WORLD_DEATH_SETTING_PASS {role.Id} setting={enabled} poster={sawPoster} nextHp=25");
                    // Recording fixture only: isolate voices so waveform evidence cannot
                    // mistake a neighboring death for this one. No production timing change.
                    await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
                }
            }
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            if (System.Environment.GetEnvironmentVariable("DOHNA_AUDIO_TRACE") == "1")
            {
                if (AudioProbe.RoomVoiceHandles.Count != 20 || AudioProbe.RoomVoiceHandles.Any(h => !h.IsReleased))
                    throw new Exception("Native room exit did not release all world death voices");
                GD.Print("DOHNA_SMOKE_WORLD_VOICE_CLEANUP_PASS handles=20");
                AudioProbe.RoomVoiceHandles.Clear();
            }
        }
        finally
        {
            preference.Write(previous);
            SquadStore.State.Set(run, owner.NetId, saved);
            owner.Creature.SetMaxHpInternal(saved.Front!.MaxHp);
            owner.Creature.SetCurrentHpInternal(saved.Front.Hp);
        }
    }

    private async Task VerifyPresentationSettingsUi()
    {
        var entry = DeathPreference();
        if (entry.Binding.Read()) throw new Exception("Fresh profile enabled female defeat cut-ins by default");
        if (entry.Label.Resolve() != "女性角色特殊死亡演出") throw new Exception("Missing localized settings label");
        var page = ModSettingsRegistry.GetPages().Single(p => p.ModId == "DohnaDohna");
        var opened = await ModSettingsNavigator.OpenByIdsAsync("DohnaDohna", page.Id, "presentation", "female_death_cut_in", null);
        if (!opened.Success) throw new Exception("Native mod settings did not open: " + opened.Message);
        await ToSignal(GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
        await Screenshot("settings-death-default-off.png");
        foreach (bool expected in new[] { true, false })
        {
            var toggle = GetTree().Root.GetChildrenRecursive<ModSettingsToggleControl>().Single(c => c.IsVisibleInTree());
            var position = toggle.GetViewport().GetFinalTransform() * toggle.GetGlobalRect().GetCenter();
            // Godot's actual GUI input chain on an inactive desktop, not physical mouse QA.
            Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
            Input.FlushBufferedEvents();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                ButtonMask = MouseButtonMask.Left,
                Pressed = true
            });
            Input.FlushBufferedEvents();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseButton
            {
                Position = position,
                GlobalPosition = position,
                ButtonIndex = MouseButton.Left,
                Pressed = false
            });
            Input.FlushBufferedEvents();
            await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            if (entry.Binding.Read() != expected) throw new Exception("Settings GUI did not toggle to " + expected);
            await Screenshot("settings-death-" + (expected ? "on" : "off") + ".png");
            // Inspect only the test profile. Do not call Binding.Save to mask a UI autosave failure.
            var paths = System.IO.Directory.EnumerateFiles(System.Environment.GetEnvironmentVariable("APPDATA")!, "presentation.json", System.IO.SearchOption.AllDirectories)
                .Concat(System.IO.Directory.EnumerateFiles(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "presentation.json", System.IO.SearchOption.AllDirectories)).ToArray();
            if (paths.Length != 1) throw new Exception("Expected one isolated presentation settings file, got " + paths.Length);
            using var document = System.Text.Json.JsonDocument.Parse(await System.IO.File.ReadAllTextAsync(paths[0]));
            if (document.RootElement.GetProperty("FemaleDeathCutInEnabled").GetBoolean() != expected)
                throw new Exception("Settings GUI did not persist the current value");
        }
        // The framework navigator may use its overlay host, not the main-menu stack.
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        Input.FlushBufferedEvents();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        Input.FlushBufferedEvents();
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
        if (GetTree().Root.GetChildrenRecursive<RitsuModSettingsSubmenu>().Any(c => c.IsVisibleInTree()))
            throw new Exception("Native back input did not close the settings host");
        GD.Print("DOHNA_SMOKE_DEATH_SETTINGS_UI_PASS default=False toggled=True,False persisted=True input=Godot-not-physical");
    }
}
