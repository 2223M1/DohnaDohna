// Adapted from STS2 0.111.0 DaggerThrow; source SHA256 54a54192279e83173070a13902aa6e7de641912631bd79d9b1eb81c62d4db528.
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
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadDaggerThrow : SquadCatalogCard
{
	public override string? FixedRole => null;
	public override bool IsMelee => false;
	public override string Prototype => "DaggerThrow";
	public override CardModel PrototypeCard => ModelDb.Card<DaggerThrow>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(9m, ValueProp.Move)];

	public SquadDaggerThrow()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithAttackerFx(() => NDaggerSprayFlurryVfx.Create(Actor, new Color("#b1ccca"), goingRight: true))
			.WithHitVfxNode((Creature t) => NDaggerSprayImpactVfx.Create(t, new Color("#b1ccca"), goingRight: true))
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await CardPileCmd.Draw(choiceContext, 1m, base.Owner);
		if (Actor.IsDead) return;
		CardModel cardModel = (await CardSelectCmd.FromHandForDiscard(choiceContext, base.Owner, new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, this)).FirstOrDefault();
		if (Actor.IsDead) return;
		if (cardModel != null)
		{
			await CardCmd.Discard(choiceContext, cardModel);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
