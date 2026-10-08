// Adapted from STS2 0.111.0 Outbreak; source SHA256 149fc064c7b3a3fd63949a336feb4afa91598475c1faf83ddcc2de2c9ca7b2a3.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadOutbreak : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "Outbreak";
	public override CardModel PrototypeCard => ModelDb.Card<Outbreak>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<PoisonPower>(9m)];

	public SquadOutbreak()
		: base(3, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		foreach (Creature hittableEnemy in base.CombatState.HittableEnemies)
		{
			NPoisonImpactVfx child = NPoisonImpactVfx.Create(hittableEnemy);
			NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(child);
			await PowerCmd.Apply<PoisonPower>(choiceContext, hittableEnemy, base.DynamicVars.Poison.BaseValue, Actor, this);
		if (Actor.IsDead) return;
		}
		foreach (Creature hittableEnemy2 in base.CombatState.HittableEnemies.ToArray())
		{
			PoisonPower power = hittableEnemy2.GetPower<PoisonPower>();
			if (power != null)
			{
#if DOHNADOHNA_STABLE
                // In 0.107.1 the same native poison trigger lives in this hook.
                await power.AfterSideTurnStart(hittableEnemy2.Side, [hittableEnemy2], base.CombatState);
#else
				await power.Trigger();
#endif
		if (Actor.IsDead) return;
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Poison.UpgradeValueBy(3m);
	}
}
