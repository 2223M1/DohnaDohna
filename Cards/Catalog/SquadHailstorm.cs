// Adapted from STS2 0.111.0 Hailstorm; source SHA256 f0e7fc03b8af26d3a43dd7252b621731ed37b92b7e63dd042607d86258c4fba7.
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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadHailstorm : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Hailstorm";
	public override CardModel PrototypeCard => ModelDb.Card<Hailstorm>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromOrb<FrostOrb>()];

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<HailstormPower>(6m)];

	public SquadHailstorm()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<HailstormPower>(choiceContext, Actor, base.DynamicVars["HailstormPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["HailstormPower"].UpgradeValueBy(2m);
	}
}
