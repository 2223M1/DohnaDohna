// Adapted from STS2 0.111.0 Capacitor; source SHA256 0b11188751cb4c31779afadf7d692eb8ad63ecf5d40239f1bc5febb064a1b3a3.
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
public sealed class SquadCapacitor : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Capacitor";
	public override CardModel PrototypeCard => ModelDb.Card<Capacitor>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new RepeatVar(2)];

	public SquadCapacitor()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await OrbCmd.AddSlots(base.Owner, base.DynamicVars.Repeat.IntValue);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Repeat.UpgradeValueBy(1m);
	}
}
