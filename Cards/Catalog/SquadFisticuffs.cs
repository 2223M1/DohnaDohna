// Adapted from STS2 0.111.0 Fisticuffs; source SHA256 cf6205087b59981e4e30188deb4331e6ac2f905f593e131cf1cdb7dd04bbc213.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadFisticuffs : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => true;
	public override string Prototype => "Fisticuffs";
	public override CardModel PrototypeCard => ModelDb.Card<Fisticuffs>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(7m, ValueProp.Move)];

	public SquadFisticuffs()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		AttackCommand attackCommand = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await CreatureCmd.GainBlock(Actor, attackCommand.Results.SelectMany((List<DamageResult> r) => r).Sum((DamageResult r) => r.TotalDamage + r.OverkillDamage), ValueProp.Move, cardPlay);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
