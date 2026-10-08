// Adapted from STS2 0.111.0 Pillage; source SHA256 aafec0e3a461e000c3ba2e21329d29ce0b70fb7e629e9dd6174d7ce8388ff267.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadPillage : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "Pillage";
	public override CardModel PrototypeCard => ModelDb.Card<Pillage>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(6m, ValueProp.Move)];

	public SquadPillage()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		CardModel cardModel;
		do
		{
			cardModel = await CardPileCmd.Draw(choiceContext, base.Owner);
		if (Actor.IsDead) return;
		}
		while (cardModel != null && cardModel.Type == CardType.Attack && CardPile.GetCards(base.Owner, PileType.Hand).Count() < CardPile.MaxCardsInHand);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
