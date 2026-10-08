// Adapted from STS2 0.111.0 Lift; source SHA256 132c5456f78f281ffe5ecc28690c64705cf52e326a93f0db24880a5b4fbb276c.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadLift : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "Lift";
	public override CardModel PrototypeCard => ModelDb.Card<Lift>();


	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(11m, ValueProp.Move)];

	public override bool GainsBlock => true;

	public SquadLift()
		: base(1, CardType.Skill, CardRarity.Uncommon, SquadTargeting.OtherMember)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await CreatureCmd.GainBlock(cardPlay.Target, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(5m);
	}
}
