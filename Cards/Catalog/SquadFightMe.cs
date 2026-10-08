// Adapted from STS2 0.111.0 FightMe; source SHA256 803c2c4d55968903da3a7245d2a937a42c7d10fbe96342795761272a16aa5d16.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
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
public sealed class SquadFightMe : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => true;
	public override string Prototype => "FightMe";
	public override CardModel PrototypeCard => ModelDb.Card<FightMe>();
	private const string _enemyStrengthKey = "EnemyStrength";

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[4]
	{
		new DamageVar(5m, ValueProp.Move),
		new RepeatVar(2),
		new PowerVar<StrengthPower>(3m),
		new DynamicVar("EnemyStrength", 1m)
	};

	public SquadFightMe()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(base.DynamicVars.Repeat.IntValue).FromSquadCard(this, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "heavy_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<StrengthPower>(choiceContext, Actor, base.DynamicVars["StrengthPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target, base.DynamicVars["EnemyStrength"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
		base.DynamicVars.Strength.UpgradeValueBy(1m);
	}
}
