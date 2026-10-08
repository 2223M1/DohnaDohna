using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DohnaDohna.Cards;

/// <summary>A transient native choice card, never added to a deck or reward pool.</summary>
[RegisterCard(typeof(TokenCardPool))]
public sealed class SquadReviveChoice() : ModCardTemplate(-1, CardType.Skill, CardRarity.Token, TargetType.None, false)
{
    public string? RoleId { get; private set; }
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    protected override bool IsPlayable => false;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Heal", 0), new DynamicVar("MaxHp", 25)];
    public override string Title => RoleId is { } id ? RoleDefinition.Get(id).GetDisplayName(LocManager.Instance.Language) : base.Title;

    public void SetMember(SquadMemberState member)
    {
        AssertMutable();
        if (member.Hp != 0) throw new ArgumentException("Revival choice requires a dead member.");
        RoleId = member.RoleId;
        var subject = new MegaCrit.Sts2.Core.Entities.Creatures.Creature(Owner, 0, member.MaxHp);
        DynamicVars["Heal"].BaseValue = MegaCrit.Sts2.Core.Hooks.Hook.ModifyRestSiteHealAmount(Owner.RunState, subject,
            MegaCrit.Sts2.Core.Entities.RestSite.HealRestSiteOption.GetBaseHealAmount(subject));
        DynamicVars["MaxHp"].BaseValue = member.MaxHp;
    }

    public override CardAssetProfile AssetProfile
    {
        get
        {
            if (RoleId == null) return SquadCardAssets.Shared;
            var role = RoleDefinition.Get(RoleId);
            var material = SquadCardAssets.ColorMaterial(new Color(role.Color));
            return new CardAssetProfile(FrameMaterial: material, BannerMaterial: material,
                PortraitBorderMaterial: material, PortraitPath: role.AssetRoot + "/card.png");
        }
    }
}
