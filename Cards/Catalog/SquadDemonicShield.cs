// Adapted from STS2 0.111.0 DemonicShield; source SHA256 24cdfb10777ed71bacb91ac7cffa63f73b074740c75a9039d2ce51a32ab6e130.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadDemonicShield : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "DemonicShield";
	public override CardModel PrototypeCard => ModelDb.Card<DemonicShield>();


	public override bool GainsBlock => true;

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[4]
	{
		new CalculationBaseVar(0m),
		new HpLossVar(1m),
		new CalculationExtraVar(1m),
		new CalculatedBlockVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) => SquadCardModel.ResolveActor(card).Block)
	};

	public SquadDemonicShield()
		: base(0, CardType.Skill, CardRarity.Uncommon, SquadTargeting.OtherMember)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		VfxCmd.PlayOnCreatureCenter(Actor, "vfx/vfx_bloody_impact");
		await CreatureCmd.Damage(choiceContext, Actor, base.DynamicVars.HpLoss.BaseValue, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Actor, this
#if !DOHNADOHNA_STABLE
            , cardPlay
#endif
        );
		if (Actor.IsDead) return;
		await CreatureCmd.GainBlock(cardPlay.Target, base.DynamicVars.CalculatedBlock.Calculate(cardPlay.Target), base.DynamicVars.CalculatedBlock.Props, cardPlay);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
