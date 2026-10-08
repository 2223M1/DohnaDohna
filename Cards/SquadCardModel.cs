using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace DohnaDohna.Cards;

/// <summary>Card-owned identity and the single substitution boundary. Never replaces a permanent card.</summary>
public abstract class SquadCardModel(int cost, CardType type, CardRarity rarity, TargetType target)
    : ModCardTemplate(cost, type, rarity, target)
{
    public virtual string? FixedRole => null;
    public virtual bool IsMelee => false;
    protected virtual IEnumerable<DynamicVar> OriginalVars => [];
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => OriginalVars.Concat([
        new FallbackDamageVar(), new FallbackBlockVar(), new FallbackCardsVar()]);
    public bool IsFallback => FixedRole is { } role && IsMutable && Owner?.Character is DohnaSquad
        && (SquadCombatState.TryGet(Owner) is { } squad ? !squad.IsAlive(role)
            : !SquadStore.Get(Owner).Members.Any(m => m.RoleId == role && m.Hp > 0));
    public Creature Actor => SquadPlaySelection.CurrentFor(this)?.Actor ?? ResolveActor(this);
    public static Creature ResolveActor(CardModel card) => SquadCombatState.TryGet(card.Owner) is { } squad
        ? squad.Actions.ActorFor(card) : card.Owner.Creature;
    protected virtual bool IsOriginalPlayable => true;
    protected sealed override bool IsPlayable => IsFallback || IsOriginalPlayable;
    public override TargetType TargetType => IsFallback
        ? Type == CardType.Attack ? TargetType.AnyEnemy : TargetType.Self : base.TargetType;

    protected sealed override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selection = SquadPlaySelection.CurrentFor(this);
        if (Actor.IsDead) return;
        if (selection?.Substituting == true || selection == null && IsFallback)
        {
            if (Type == CardType.Attack)
                await DamageCmd.Attack(IsUpgraded ? 9 : 6).FromSquadCard(this, play)
                    .Targeting(play.Target!).ExecuteSquad(context);
            else if (Type == CardType.Skill)
                await CreatureCmd.GainBlock(Actor, IsUpgraded ? 8 : 5, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play);
            if (Actor.IsAlive) await CardPileCmd.Draw(context, Type == CardType.Power ? IsUpgraded ? 3 : 2 : 1, Owner);
            return;
        }
        await OnSquadPlay(context, play);
    }

    protected abstract Task OnSquadPlay(PlayerChoiceContext context, CardPlay play);
}

internal sealed class FallbackDamageVar() : DamageVar("FallbackDamage", 6, ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    { BaseValue = card.IsUpgraded ? 9 : 6; base.UpdateCardPreview(card, previewMode, target, runGlobalHooks); }
}
internal sealed class FallbackBlockVar() : BlockVar("FallbackBlock", 5, ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    { BaseValue = card.IsUpgraded ? 8 : 5; base.UpdateCardPreview(card, previewMode, target, runGlobalHooks); }
}
internal sealed class FallbackCardsVar() : IntVar("FallbackCards", 2)
{
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    { BaseValue = card.IsUpgraded ? 3 : 2; base.UpdateCardPreview(card, previewMode, target, runGlobalHooks); }
}

/// <summary>Approved native prototypes keep their own command bodies, variables and upgrade behavior.</summary>
public abstract class SquadCatalogCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : SquadCardModel(cost, type, rarity, target)
{
    public abstract string Prototype { get; }
    public abstract CardModel PrototypeCard { get; }
    public override CardAssetProfile AssetProfile
    {
        get
        {
            var material = SquadCardAssets.ColorMaterial(new Color(FixedRole is { } role ? RoleDefinition.Get(role).Color : "#657F9A"));
            return new(FrameMaterial: material, BannerMaterial: material, PortraitBorderMaterial: material,
                PortraitPath: PrototypeCard.PortraitPath);
        }
    }
}
