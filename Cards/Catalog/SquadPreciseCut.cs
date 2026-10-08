// Adapted from STS2 0.111.0 PreciseCut; source SHA256 4a266f816872f40f8948f9dac965ec8526a1f91dafc5cfb2ebd79ee84d463ccc.
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
public sealed class SquadPreciseCut : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => true;
	public override string Prototype => "PreciseCut";
	public override CardModel PrototypeCard => ModelDb.Card<PreciseCut>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[3]
	{
		new CalculationBaseVar(13m),
		new ExtraDamageVar(2m),
		new CalculatedDamageVar(ValueProp.Move).WithMultiplier(delegate(CardModel card, Creature? _)
		{
			int num = PileType.Hand.GetPile(card.Owner).Cards.Count;
			CardPile? pile = card.Pile;
			if (pile != null && pile.Type == PileType.Hand)
			{
				num--;
			}
			return -num;
		})
	};

	public SquadPreciseCut()
		: base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.CalculatedDamage).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash", null, "dagger_throw.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.CalculationBase.UpgradeValueBy(3m);
	}
}
