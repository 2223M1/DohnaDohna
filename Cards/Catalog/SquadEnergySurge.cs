// Adapted from STS2 0.111.0 EnergySurge; source SHA256 755a2ca0ea91f4e84b252bb9e18d89cae71d08325780e70034f6c6122b26b504.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadEnergySurge : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "EnergySurge";
	public override CardModel PrototypeCard => ModelDb.Card<EnergySurge>();


	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [base.EnergyHoverTip];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => [new EnergyVar(2)];

	public SquadEnergySurge()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PlayerCmd.GainEnergy(base.DynamicVars.Energy.IntValue, Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Energy.UpgradeValueBy(1m);
	}
}
