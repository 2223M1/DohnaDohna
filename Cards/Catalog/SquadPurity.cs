// Adapted from STS2 0.111.0 Purity; source SHA256 bbecb9e6be82ea50bc8b36454b34007bdb29d0c42c564de889d234645bcd09e5.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadPurity : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "Purity";
	public override CardModel PrototypeCard => ModelDb.Card<Purity>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<DynamicVar> OriginalVars => [new CardsVar(3)];

	public SquadPurity()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (CardModel item in await CardSelectCmd.FromHand(prefs: new CardSelectorPrefs(base.SelectionScreenPrompt, 0, base.DynamicVars.Cards.IntValue), context: choiceContext, player: base.Owner, filter: null, source: this))
		{
			await CardCmd.Exhaust(choiceContext, item);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Cards.UpgradeValueBy(2m);
	}
}
