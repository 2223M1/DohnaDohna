// Adapted from STS2 0.111.0 ThrummingHatchet; source SHA256 8d06d17e1987b7d5a63ef019f8c7a7535af83a5666b5f6353ba3708ed49d7837.
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
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadThrummingHatchet : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "ThrummingHatchet";
	public override CardModel PrototypeCard => ModelDb.Card<ThrummingHatchet>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(11m, ValueProp.Move)];

	public SquadThrummingHatchet()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
	{
		if (IsFallback) return;
		if (player == base.Owner && CombatManager.Instance.History.CardPlaysFinished.Any((CardPlayFinishedEntry e) => e.HappenedLastPlayerTurn(base.Owner) && e.CardPlay.Card == this))
		{
			CardPile? pile = base.Pile;
			if (pile == null || pile.Type != PileType.Hand)
			{
				await CardPileCmd.Add(this, PileType.Hand);
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
