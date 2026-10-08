// Adapted from STS2 0.111.0 HandOfGreed; source SHA256 f269d320009c9d8e24e706700294a3556a4aca3c08537482487990ec30d548a6.
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
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadHandOfGreed : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "HandOfGreed";
	public override CardModel PrototypeCard => ModelDb.Card<HandOfGreed>();
	public const int goldAmount = 20;

	private const string _goldKey = "Gold";

	public override bool CanBeGeneratedInCombat => false;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(20m, ValueProp.Move),
		new DynamicVar("Gold", 20m)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Fatal)];

	public SquadHandOfGreed()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(5m);
		base.DynamicVars["Gold"].UpgradeValueBy(5m);
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		bool shouldTriggerFatal = cardPlay.Target.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal());
		Vector2? monsterPos = null;
		if (TestMode.IsOff)
		{
			monsterPos = NCombatRoom.Instance.GetCreatureNode(cardPlay.Target)?.VfxSpawnPosition;
		}
		AttackCommand attackCommand = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		if (shouldTriggerFatal && attackCommand.Results.SelectMany((List<DamageResult> r) => r).Any((DamageResult r) => r.WasTargetKilled))
		{
			if (monsterPos.HasValue)
			{
				VfxCmd.PlayVfx(monsterPos.Value, "vfx/vfx_coin_explosion_regular", NCombatRoom.Instance?.CombatVfxContainer);
			}
			await PlayerCmd.GainGold(base.DynamicVars["Gold"].IntValue, base.Owner);
		if (Actor.IsDead) return;
		}
	}
}
