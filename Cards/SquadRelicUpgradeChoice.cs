using DohnaDohna.Content;
using DohnaDohna.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DohnaDohna.Cards;

/// <summary>Transient native grid option; its referenced relic remains in the native inventory.</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class SquadRelicUpgradeChoice() : ModCardTemplate(-1, CardType.Skill, CardRarity.Token, TargetType.None, false)
{
    public SquadRoleRelic? Original { get; private set; }
    private RelicModel? _upgrade;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    protected override bool IsPlayable => false;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new StringVar("Effect")];
    public override string Title => _upgrade?.Title.GetFormattedText() ?? base.Title;
    public void SetRelic(SquadRoleRelic original, RelicModel upgrade)
    {
        AssertMutable();
        Original = original;
        _upgrade = upgrade;
        ((StringVar)DynamicVars["Effect"]).StringValue = upgrade.DynamicDescription.GetFormattedText();
    }
    public override CardAssetProfile AssetProfile
    {
        get
        {
            if (Original == null) return SquadCardAssets.Shared;
            var role = RoleDefinition.Get(Original.Role);
            var material = SquadCardAssets.ColorMaterial(new Color(role.Color));
            return new(FrameMaterial: material, BannerMaterial: material, PortraitBorderMaterial: material,
                PortraitPath: role.AssetRoot + "/card.png");
        }
    }
}
