using DohnaDohna.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace DohnaDohna.Relics;

[RegisterRelic(typeof(SquadRelicPool))]
public sealed class SquadStandard : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override RelicAssetProfile AssetProfile => new(IconPath: "res://DohnaDohna/images/icons/squad.png",
        IconOutlinePath: "res://DohnaDohna/images/icons/squad.png", BigIconPath: "res://DohnaDohna/images/icons/squad.png");
}
