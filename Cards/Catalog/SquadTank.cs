// Adapted from STS2 0.111.0 Tank; source SHA256 8e062d3c0fdc471a6ffd76a07acbe80a065fece034ab7438b8a4f981995a9920.
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
public sealed class SquadTank : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "Tank";
	public override CardModel PrototypeCard => ModelDb.Card<Tank>();
	private const string _damageIncreaseKey = "DamageIncrease";

	private const string _damageDecreaseKey = "DamageDecrease";



	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DynamicVar("DamageIncrease", 1.5m),
		new DynamicVar("DamageDecrease", 0.5m)
	};

	public SquadTank()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<TankPower>(choiceContext, Actor, 1m, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
