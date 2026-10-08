// Adapted from STS2 0.111.0 SleightOfFlesh; source SHA256 1d3dc9bd2029f5875dad71f36e3d1fded97457d5176a8e7747e4ed391f3d66c6.
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
public sealed class SquadSleightOfFlesh : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => false;
	public override string Prototype => "SleightOfFlesh";
	public override CardModel PrototypeCard => ModelDb.Card<SleightOfFlesh>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<SleightOfFleshPower>(9m)];

	public SquadSleightOfFlesh()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<SleightOfFleshPower>(choiceContext, Actor, base.DynamicVars["SleightOfFleshPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["SleightOfFleshPower"].UpgradeValueBy(4m);
	}
}
