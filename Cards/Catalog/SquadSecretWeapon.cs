// Adapted from STS2 0.111.0 SecretWeapon; source SHA256 239b6344406eb9e3e1c8f38164042aa1d51ff684a063e712ac8e6242db5e080b.
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

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadSecretWeapon : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "SecretWeapon";
	public override CardModel PrototypeCard => ModelDb.Card<SecretWeapon>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadSecretWeapon()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel cardModel = (await CardSelectCmd.FromCombatPile(prefs: new CardSelectorPrefs(base.SelectionScreenPrompt, 1), context: choiceContext, pile: PileType.Draw.GetPile(base.Owner), player: base.Owner, filter: (CardModel c) => c.Type == CardType.Attack)).FirstOrDefault();
		if (Actor.IsDead) return;
		if (cardModel != null)
		{
			await CardPileCmd.Add(cardModel, PileType.Hand);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
