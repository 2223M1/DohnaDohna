using DohnaDohna.Scripts;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace DohnaDohna.Code.Visuals;

/// <summary>Local presentation preference; never part of squad or combat saves.</summary>
internal sealed class PresentationSettings
{
    public bool FemaleDeathCutInEnabled { get; set; } = false;

    private static readonly ModSettingsValueBinding<PresentationSettings, bool> DeathCutIn = new(
        Entry.ModId, "presentation", SaveScope.Global,
        s => s.FemaleDeathCutInEnabled, (s, value) => s.FemaleDeathCutInEnabled = value);

    internal static bool ShowFemaleDeathCutIn => DeathCutIn.Read();

    internal static void Register()
    {
        ModDataStore.For(Entry.ModId).Register<PresentationSettings>(
            key: "presentation", fileName: "presentation.json", scope: SaveScope.Global,
            defaultFactory: () => new PresentationSettings(), autoCreateIfMissing: true);
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(ModSettingsText.Literal("DohnaDohna"))
            .WithModDisplayName(ModSettingsText.Literal("DohnaDohna"))
            .AddSection("presentation", section => section
                .WithTitle(ModSettingsText.LocString("settings_ui", "DOHNA_PRESENTATION.title", "Presentation"))
                .AddToggle("female_death_cut_in",
                    ModSettingsText.LocString("settings_ui", "DOHNA_FEMALE_DEATH_CUT_IN.title", "Female character special defeat presentation"),
                    DeathCutIn,
                    ModSettingsText.LocString("settings_ui", "DOHNA_FEMALE_DEATH_CUT_IN.description",
                        "Off by default. When off, female characters use the same fall animation and death voice flow as male characters, without a defeat poster. Takes effect on the next death."))));
    }
}
