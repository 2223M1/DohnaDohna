// Adapted from STS2 0.111.0 Expertise; source SHA256 5a20da04a7749b2e65a455d6492c7a4985808b32695854e6751efcb039818cbf.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadExpertise : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "Expertise";
	public override CardModel PrototypeCard => ModelDb.Card<Expertise>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new CardsVar(2)];

	public SquadExpertise()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (CardModel item in await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.IntValue, base.Owner))
		{
#if DOHNADOHNA_STABLE
            item.GiveSingleTurnRetain();
            MegaCrit.Sts2.Core.Nodes.Cards.NCard.FindOnTable(item)?.UpdateVisuals(item.Pile.Type, CardPreviewMode.Normal);
#else
			CardCmd.ApplySingleTurnRetain(item);
#endif
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Cards.UpgradeValueBy(1m);
	}
}
