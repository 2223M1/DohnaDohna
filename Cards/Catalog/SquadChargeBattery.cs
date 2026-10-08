// Adapted from STS2 0.111.0 ChargeBattery; source SHA256 cfe5c04e8f6e0958f696a62d1b3dcf57d8d4770e73b294c0f551a3035f966875.
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadChargeBattery : SquadCatalogCard
{
	public override string? FixedRole => null;
	public override bool IsMelee => false;
	public override string Prototype => "ChargeBattery";
	public override CardModel PrototypeCard => ModelDb.Card<ChargeBattery>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(7m, ValueProp.Move),
		new EnergyVar(1)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [base.EnergyHoverTip];

	public SquadChargeBattery()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, base.DynamicVars.Energy.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
