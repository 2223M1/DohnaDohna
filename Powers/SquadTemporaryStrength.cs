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
            SquadTemporaryStrength => "res://images/atlases/power_atlas.sprites/flex_potion_power.tres",
            SquadTemporaryDexterity => "res://images/atlases/power_atlas.sprites/speed_potion_power.tres",
            _ => null
        });
}
