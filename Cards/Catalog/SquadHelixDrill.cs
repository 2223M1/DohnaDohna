// Adapted from STS2 0.111.0 HelixDrill; source SHA256 bcf768c817bb7a1b9de8d5a3bfd76a03525997d2c86b737b0d5efa0a5fd5e7ef.
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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadHelixDrill : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "HelixDrill";
	public override CardModel PrototypeCard => ModelDb.Card<HelixDrill>();
	private const string _calculatedHitsKey = "CalculatedHits";

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[4]
	{
		new DamageVar(3m, ValueProp.Move),
		new CalculationBaseVar(0m),
		new CalculationExtraVar(1m),
		new CalculatedVar("CalculatedHits").WithMultiplier(delegate(CardModel card, Creature? _)
		{
			if (card.Pile == null)
			{
				return 0m;
			}
			int num = (from e in CombatManager.Instance.History.Entries.OfType<EnergySpentEntry>()
				where e.HappenedThisTurn(card.CombatState) && e.Actor.Player == card.Owner
				select e).Sum((EnergySpentEntry c) => c.Amount);
			if (card.Pile.Type == PileType.Play)
			{
				num -= card.EnergyCost.GetWithModifiers(CostModifiers.All);
			}
			return num;
		})
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [base.EnergyHoverTip];

	public SquadHelixDrill()
		: base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount((int)((CalculatedVar)base.DynamicVars["CalculatedHits"]).Calculate(cardPlay.Target)).FromSquadCard(this, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
