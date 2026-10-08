// Adapted from STS2 0.111.0 Impervious; source SHA256 9a97b3091857c8f7ed35281b9ca1fa20af707bc56c17df651fec464e9bb79552.
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
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadImpervious : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "Impervious";
	public override CardModel PrototypeCard => ModelDb.Card<Impervious>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(30m, ValueProp.Move)];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadImpervious()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(10m);
	}
}
