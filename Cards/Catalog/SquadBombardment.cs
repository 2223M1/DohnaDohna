// Adapted from STS2 0.111.0 Bombardment; source SHA256 161ff5ce36699136491b467bb7f557ff345bf021b9975e04a837b8093a652501.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadBombardment : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "Bombardment";
	public override CardModel PrototypeCard => ModelDb.Card<Bombardment>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(18m, ValueProp.Move)];

	public SquadBombardment()
		: base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(cardPlay.Target);
		if (nCreature != null)
		{
			NLargeMagicMissileVfx nLargeMagicMissileVfx = NLargeMagicMissileVfx.Create(nCreature.GetBottomOfHitbox(), new Color("50b598"));
			NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(nLargeMagicMissileVfx);
			await Cmd.Wait(nLargeMagicMissileVfx.WaitTime);
		if (Actor.IsDead) return;
		}
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	/// <remarks>
	/// We use Early here to prevent it from double-triggering if another auto-pre-play phase effect (like
	/// <see cref="T:MegaCrit.Sts2.Core.Models.Powers.MayhemPower" />) also causes it to Exhaust.
	/// </remarks>
	public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (IsFallback) return;
		CardPile? pile = base.Pile;
		if (pile != null && pile.Type == PileType.Exhaust && player == base.Owner)
		{
			await CardCmd.AutoPlay(choiceContext, this, null);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(6m);
	}
}
