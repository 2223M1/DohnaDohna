// Adapted from STS2 0.111.0 Rend; source SHA256 e23adb19be757889477688cc4a26a7c017503cdf8cbcf3d3b35723c9ef807554.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadRend : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => true;
	public override string Prototype => "Rend";
	public override CardModel PrototypeCard => ModelDb.Card<Rend>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[3]
	{
		new CalculationBaseVar(10m),
		new ExtraDamageVar(5m),
		new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) => target?.Powers.Count(ShouldCountPower) ?? 0)
	};

	public SquadRend()
		: base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.CalculatedDamage).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.ExtraDamage.UpgradeValueBy(3m);
		base.DynamicVars.CalculationBase.UpgradeValueBy(2m);
	}

	private static bool ShouldCountPower(PowerModel power)
	{
		if (power.TypeForCurrentAmount == PowerType.Debuff)
		{
			return !(power is ITemporaryPower);
		}
		return false;
	}
}
