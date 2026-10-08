// Adapted from STS2 0.111.0 ShadowStep; source SHA256 c5997898c8af2c90b99a2a077bedf1db1f0487068f5aa379e1d043d3286ec9e2.
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
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadShadowStep : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "ShadowStep";
	public override CardModel PrototypeCard => ModelDb.Card<ShadowStep>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new CardsVar(3)];

	public SquadShadowStep()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardCmd.Discard(choiceContext, PileType.Hand.GetPile(base.Owner).Cards);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<ShadowStepPower>(choiceContext, Actor, 1m, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
