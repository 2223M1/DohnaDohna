// Adapted from STS2 0.111.0 GangUp; source SHA256 da26e0589b8fbfc57a87fbba482d51f8a2fe21ddd901f081fc3ef191ba3df261.
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
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadGangUp : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "GangUp";
	public override CardModel PrototypeCard => ModelDb.Card<GangUp>();


	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[3]
	{
		new CalculationBaseVar(5m),
		new ExtraDamageVar(5m),
		new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) => CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Count((DamageReceivedEntry e) => e.Receiver == target && e.Result.Props.IsPoweredAttack() && e.HappenedThisTurn(card.CombatState) && e.Dealer != null && e.Dealer != SquadCardModel.ResolveActor(card) && e.Dealer.Side == SquadCardModel.ResolveActor(card).Side))
	};

	public SquadGangUp()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
		base.DynamicVars.ExtraDamage.UpgradeValueBy(2m);
	}
}
