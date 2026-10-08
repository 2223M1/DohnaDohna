// Adapted from STS2 0.111.0 Feed; source SHA256 7a8dd1fdd67cabc592a5a3b216cf27c114f763253d57a2e84daac64ae3107703.
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
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadFeed : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "Feed";
	public override CardModel PrototypeCard => ModelDb.Card<Feed>();
	public override bool CanBeGeneratedInCombat => false;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(10m, ValueProp.Move),
		new MaxHpVar(3m)
	};

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Fatal)];

	public SquadFeed()
		: base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		bool shouldTriggerFatal = cardPlay.Target.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal());
		AttackCommand attackCommand = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_bite", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		if (shouldTriggerFatal && attackCommand.Results.SelectMany((List<DamageResult> r) => r).Any((DamageResult r) => r.WasTargetKilled))
		{
			await CreatureCmd.GainMaxHp(Actor, base.DynamicVars.MaxHp.IntValue);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars.MaxHp.UpgradeValueBy(1m);
	}
}
