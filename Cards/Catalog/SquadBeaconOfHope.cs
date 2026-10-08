// Adapted from STS2 0.111.0 BeaconOfHope; source SHA256 ca1ebe7704d97206a92ef70687b1851bf38ad18f3735f482f8eec8aa824de390.
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
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadBeaconOfHope : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "BeaconOfHope";
	public override CardModel PrototypeCard => ModelDb.Card<BeaconOfHope>();


	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

	public SquadBeaconOfHope()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<BeaconOfHopePower>(choiceContext, Actor, 1m, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}
