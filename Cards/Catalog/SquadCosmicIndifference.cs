// Adapted from STS2 0.111.0 CosmicIndifference; source SHA256 c91ab32bb934ec6b2cdad70880edba1ecbe5cb4f935d214fbc5cb24783dfa933.
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
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadCosmicIndifference : SquadCatalogCard
{
	public override string? FixedRole => null;
	public override bool IsMelee => false;
	public override string Prototype => "CosmicIndifference";
	public override CardModel PrototypeCard => ModelDb.Card<CosmicIndifference>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(6m, ValueProp.Move)];

	public SquadCosmicIndifference()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		CardSelectorPrefs prefs = new CardSelectorPrefs(base.SelectionScreenPrompt, 1);
		PileType.Discard.GetPile(base.Owner);
		CardModel cardModel = (await CardSelectCmd.FromCombatPile(choiceContext, PileType.Discard.GetPile(base.Owner), base.Owner, prefs)).FirstOrDefault();
		if (Actor.IsDead) return;
		bool flag = cardModel != null;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3;
			switch (cardModel.Pile?.Type)
			{
			case PileType.Draw:
			case PileType.Discard:
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag2 = flag3;
		}
		if (flag2)
		{
			await CardPileCmd.Add(cardModel, PileType.Draw, CardPilePosition.Top);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
