// Adapted from STS2 0.111.0 Tempest; source SHA256 acc641ea29bf188362ec100b4e6bf234e65a50fb11313abafbf07449e4e7e8ee.
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
using MegaCrit.Sts2.Core.Models.Orbs;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadTempest : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Tempest";
	public override CardModel PrototypeCard => ModelDb.Card<Tempest>();
	protected override bool HasEnergyCostX => true;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.Static(StaticHoverTip.Channeling),
		HoverTipFactory.FromOrb<LightningOrb>()
	};

	public SquadTempest()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		int numOfOrbs = ResolveEnergyXValue();
		if (base.IsUpgraded)
		{
			numOfOrbs++;
		}
		for (int i = 0; i < numOfOrbs; i++)
		{
			await OrbCmd.Channel<LightningOrb>(choiceContext, base.Owner);
		if (Actor.IsDead) return;
		}
	}
}
