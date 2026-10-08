// Adapted from STS2 0.111.0 MasterOfStrategy; source SHA256 ef725d32991f06711aa635b46ef68695397434fcd6205ac0c1b9c088b44da567.
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
public sealed class SquadMasterOfStrategy : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "MasterOfStrategy";
	public override CardModel PrototypeCard => ModelDb.Card<MasterOfStrategy>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new CardsVar(3)];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadMasterOfStrategy()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Cards.UpgradeValueBy(1m);
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
	}
}
