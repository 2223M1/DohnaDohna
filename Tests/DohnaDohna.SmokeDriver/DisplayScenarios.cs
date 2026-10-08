using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Saves;
using System.Text.Json;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyDisplayModes()
    {
        if (DisplayServer.GetName() == "headless")
            throw new Exception("Display acceptance requires the real rendered host.");
        var evidence = new List<object>();
        var proofFile = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "display-proof.json");

        void Check(string phase, bool fullscreen)
        {
            var mode = DisplayServer.WindowGetMode();
            var borderless = DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Borderless);
            var unresizable = DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.ResizeDisabled);
            var screen = DisplayServer.WindowGetCurrentScreen();
            var screenPosition = DisplayServer.ScreenGetPosition(screen);
            var screenSize = DisplayServer.ScreenGetSize(screen);
            var position = DisplayServer.WindowGetPosition();
            var size = DisplayServer.WindowGetSize();
            var valid = SaveManager.Instance.SettingsSave.Fullscreen == fullscreen && (fullscreen
                ? mode == DisplayServer.WindowMode.Fullscreen && position == screenPosition && size == screenSize
                : mode == DisplayServer.WindowMode.Windowed && !borderless && !unresizable
                    && size == new Vector2I(1600, 900)
                    && position.X >= screenPosition.X && position.Y >= screenPosition.Y
                    && position.X + size.X <= screenPosition.X + screenSize.X
                    && position.Y + size.Y <= screenPosition.Y + screenSize.Y);
            var snapshot = new { phase, fullscreen, mode = mode.ToString(), borderless, unresizable,
                position = new[] { position.X, position.Y }, size = new[] { size.X, size.Y },
                screenPosition = new[] { screenPosition.X, screenPosition.Y }, screenSize = new[] { screenSize.X, screenSize.Y }, passed = valid };
            evidence.Add(snapshot);
            System.IO.File.WriteAllText(proofFile, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("DOHNA_SMOKE_DISPLAY_STATE " + JsonSerializer.Serialize(snapshot));
            if (!valid) throw new Exception("Native display mode/flags/geometry mismatch: " + phase);
        }

        Check("startup-windowed", false);
        await Screenshot("display-windowed.png");
        NFullscreenTickbox.SetFullscreen(true);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        Check("native-fullscreen", true);
        NFullscreenTickbox.SetFullscreen(false);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        Check("returned-windowed", false);
    }
}
