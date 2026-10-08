// Adapted from STS2 0.111.0 Glimmer; source SHA256 a7783eb2abf42cc1ccf0b2bb4c1cc0eaa6d0abcfb0a702fd5bf757e303b1c9a8.
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
public sealed class SquadGlimmer : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "Glimmer";
	public override CardModel PrototypeCard => ModelDb.Card<Glimmer>();
	private const string _putBackKey = "PutBack";

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new CardsVar(3),
		new DynamicVar("PutBack", 1m)
	};

	public SquadGlimmer()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
		CardModel[] array = (await CardSelectCmd.FromHand(prefs: new CardSelectorPrefs(base.SelectionScreenPrompt, base.DynamicVars["PutBack"].IntValue), context: choiceContext, player: base.Owner, filter: null, source: this)).ToArray();
		if (Actor.IsDead) return;
		if (array.Length != 0)
		{
			await CardPileCmd.Add(array, PileType.Draw, CardPilePosition.Top);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Cards.UpgradeValueBy(1m);
	}
}
