// Adapted from STS2 0.111.0 ThinkingAhead; source SHA256 5fc8e7dc2da50d9b140bfa37bc90f01bc69662107f9c4d1e1c949bb1e287c87c.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadThinkingAhead : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "ThinkingAhead";
	public override CardModel PrototypeCard => ModelDb.Card<ThinkingAhead>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => [new CardsVar(2)];

	public SquadThinkingAhead()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
		CardModel cardModel = (await CardSelectCmd.FromHand(prefs: new CardSelectorPrefs(base.SelectionScreenPrompt, 1), context: choiceContext, player: base.Owner, filter: null, source: this)).FirstOrDefault();
		if (Actor.IsDead) return;
		if (cardModel != null)
		{
			await CardPileCmd.Add(cardModel, PileType.Draw, CardPilePosition.Top);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
