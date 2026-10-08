using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Localization;

namespace DohnaDohna.Cards;

public abstract class SquadAttackCard : SquadCardModel
{
    public virtual bool IsStrike => false;
    public virtual int EffectMultiplier => 1;
    protected SquadAttackCard(CardRarity rarity) : base(1, CardType.Attack, rarity, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar((IsStrike ? 6 : 7) * EffectMultiplier, ValueProp.Move)];
    protected override bool IsOriginalPlayable => SquadCombatState.TryGet(Owner)?.Living.Any() == true;
    public override CardAssetProfile AssetProfile
    {
        get
        {
            var role = FixedRole == null ? null : RoleDefinition.Get(FixedRole);
            var color = new Color(role?.Color ?? "#657F9A");
            var material = SquadCardAssets.ColorMaterial(color);
            return new CardAssetProfile(FrameMaterial: material, BannerMaterial: material,
                PortraitBorderMaterial: material, PortraitPath: role == null
                    ? "res://DohnaDohna/images/cards/squad.png" : role.AssetRoot + "/card.png");
        }
    }
    protected override Task OnSquadPlay(PlayerChoiceContext context, CardPlay play) =>
        SquadCombatState.Get(Owner).Actions.Attack(context, this, play);
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (IsStrike || FixedRole != null) return;
        var role = IsMutable ? SquadCombatState.TryGet(Owner)?.Living.LastOrDefault() : null;
        var roleId = role == null ? null : ((DohnaDohna.Monsters.SquadMember)role.Monster!).RoleId;
        description.Add("RoleEffect", roleId == null ? new LocString("cards", "DOHNA_SQUAD.dynamic_effect_unknown")
            : new LocString("cards", "DOHNA_SQUAD.effect_" + roleId + (IsUpgraded ? "_upgraded" : "") + (EffectMultiplier == 2 ? "_ancient" : "")));
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3 * EffectMultiplier);
}

[RegisterCard(typeof(SquadCardPool))]
[RegisterCharacterStarterCard(typeof(DohnaSquad), 4, Order = 0)]
public sealed class SquadStrike() : SquadAttackCard(CardRarity.Basic)
{
    public override bool IsStrike => true;
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
}

[RegisterCard(typeof(SquadCardPool))]
[RegisterCharacterStarterCard(typeof(DohnaSquad), 4, Order = 10)]
public sealed class SquadDefend() : ModCardTemplate(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override bool IsPlayable => SquadCombatState.TryGet(Owner)?.Living.Any() == true;
    public override CardAssetProfile AssetProfile => SquadCardAssets.Shared;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        CreatureCmd.GainBlock(SquadCombatState.Get(Owner).Front, DynamicVars.Block, play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

[RegisterCard(typeof(SquadCardPool))]
[RegisterCharacterStarterCard(typeof(DohnaSquad), 1, Order = 20)]
public sealed class SquadSwap() : ModCardTemplate(0, CardType.Skill, CardRarity.Basic, SquadTargeting.Swap)
{
    public override CardAssetProfile AssetProfile => SquadCardAssets.Shared;
    protected override bool IsPlayable => SquadCombatState.TryGet(Owner)?.Living.Count() > 1;
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var squad = SquadCombatState.Get(Owner);
        if (play.Target is { } target) await squad.Swap(target);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

public static class SquadCardAssets
{
    public static ShaderMaterial ColorMaterial(Color color)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://DohnaDohna/shaders/card_color.gdshader") };
        material.SetShaderParameter("role_color", color);
        return material;
    }
    public static CardAssetProfile Shared
    {
        get
        {
            var color = new Color("#657F9A");
            var material = ColorMaterial(color);
            return new CardAssetProfile(FrameMaterial: material, BannerMaterial: material,
                PortraitBorderMaterial: material, PortraitPath: "res://DohnaDohna/images/cards/squad.png");
        }
    }
}

[RegisterCard(typeof(SquadCardPool))]
[RegisterCharacterStarterCard(typeof(DohnaSquad), 1, Order = 30)]
[RegisterArchaicToothTranscendence(typeof(SquadTranscendentSpecial))]
public sealed class SquadSpecial() : SquadAttackCard(CardRarity.Basic);

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadTranscendentSpecial() : SquadAttackCard(CardRarity.Ancient)
{
    public override int EffectMultiplier => 2;
}

[RegisterCard(typeof(SquadCardPool))] public sealed class KumaSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "kuma"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class AlyceSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "alyce"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class AntenaSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "antena"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class ToraSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "tora"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class KikuchiyoSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "kikuchiyo"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class MedhicoSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "medhico"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class JokerSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "joker"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class ZappaSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "zappa"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class KirakiraSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "kirakira"; }
[RegisterCard(typeof(SquadCardPool))] public sealed class PornoSignature() : SquadAttackCard(CardRarity.Common) { public override string FixedRole => "porno"; }
