// Adapted from STS2 0.111.0 Deflect; source SHA256 e8a6861b7493f848458bc45d5f8bfbd66bcd1a49f97ea4075153cd32c0dab828.
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
public sealed class SquadDeflect : SquadCatalogCard
{
	public override string? FixedRole => null;
	public override bool IsMelee => false;
	public override string Prototype => "Deflect";
	public override CardModel PrototypeCard => ModelDb.Card<Deflect>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(4m, ValueProp.Move)];

	public SquadDeflect()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
