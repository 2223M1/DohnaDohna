using DohnaDohna.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Powers;

public sealed class SquadTemporaryStrength : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<SquadSpecial>();
    public static void RegisterAssets() => STS2RitsuLib.Scaffolding.Content.Patches.ExternalAssetOverrideRegistry.RegisterPowerIconPathProvider(
        "DohnaDohna.SquadTemporaryStats", power => power switch
        {
            SquadTemporaryStrength or ToraSupportStrength => "res://images/atlases/power_atlas.sprites/flex_potion_power.tres",
            SquadTemporaryDexterity => "res://images/atlases/power_atlas.sprites/speed_potion_power.tres",
            _ => null
        });
}

// Native TemporaryStrengthPower requires a distinct model per source. Its
// BeforeApplied grants Strength BEFORE inserting the temporary-power instance;
// Tora can react inside that grant, so sharing the card's model re-enters Apply
// and inserts the same non-instanced power twice. Keep the relic's contribution
// separate and let native stacking/turn-end removal own both lifetimes.
public sealed class ToraSupportStrength : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Relic<DohnaDohna.Relics.ToraEmblem>();
}
