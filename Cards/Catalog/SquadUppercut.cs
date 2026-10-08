// Adapted from STS2 0.111.0 Uppercut; source SHA256 94957502fa8820d40d95d1a378896793541fe299396101d4fa87ba6a8bbc522e.
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
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadUppercut : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => true;
	public override string Prototype => "Uppercut";
	public override CardModel PrototypeCard => ModelDb.Card<Uppercut>();
	private const string _powerKey = "Power";

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(13m, ValueProp.Move),
		new DynamicVar("Power", 1m)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<WeakPower>(),
		HoverTipFactory.FromPower<VulnerablePower>()
	};

	public SquadUppercut()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithAttackerAnim(Ironclad.GetHeavyAnimIfApplicable(base.Owner.Character), Ironclad.GetHeavyAttackDelayIfApplicable(base.Owner.Character))
			.WithHitFx("vfx/vfx_heavy_blunt", null, "heavy_attack.mp3")
			.WithHitVfxSpawnedAtBase()
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		int amount = base.DynamicVars["Power"].IntValue;
		await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, amount, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, amount, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["Power"].UpgradeValueBy(1m);
	}
}
