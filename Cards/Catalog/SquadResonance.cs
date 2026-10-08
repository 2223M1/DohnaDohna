// Adapted from STS2 0.111.0 Resonance; source SHA256 95f791564a53a5eff794197b392ab60d2d9fa87c30cc8579cf301e389dceddc3.
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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadResonance : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Resonance";
	public override CardModel PrototypeCard => ModelDb.Card<Resonance>();
	public override int CanonicalStarCost => 2;

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<StrengthPower>(1m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

	public SquadResonance()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		int intValue = base.DynamicVars["StrengthPower"].IntValue;
		await PowerCmd.Apply<StrengthPower>(choiceContext, Actor, intValue, Actor, this);
		if (Actor.IsDead) return;
		foreach (Creature hittableEnemy in base.CombatState.HittableEnemies)
		{
			await PowerCmd.Apply<StrengthPower>(choiceContext, hittableEnemy, -1m, Actor, this);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["StrengthPower"].UpgradeValueBy(1m);
	}
}
