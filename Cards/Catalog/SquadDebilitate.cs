// Adapted from STS2 0.111.0 Debilitate; source SHA256 62a830004ea6e05b6dcb900667cef61586e3da3e3818ba0ae7077764efcf5206.
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
public sealed class SquadDebilitate : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => true;
	public override string Prototype => "Debilitate";
	public override CardModel PrototypeCard => ModelDb.Card<Debilitate>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<WeakPower>()
	};

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(10m, ValueProp.Move),
		new PowerVar<DebilitatePower>(2m)
	};

	public SquadDebilitate()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<DebilitatePower>(choiceContext, cardPlay.Target, base.DynamicVars["DebilitatePower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars["DebilitatePower"].UpgradeValueBy(1m);
	}
}
