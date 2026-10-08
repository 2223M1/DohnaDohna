// Adapted from STS2 0.111.0 BouncingFlask; source SHA256 7efcd4ec4532ff53bcdfdee4c07c71e9091ebfb2ba0375ea41c6937e7a704401.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadBouncingFlask : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "BouncingFlask";
	public override CardModel PrototypeCard => ModelDb.Card<BouncingFlask>();
	private readonly Color _vfxTint = new Color("83eb85");

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new PowerVar<PoisonPower>(3m),
		new RepeatVar(3)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

	public SquadBouncingFlask()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.RandomEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		Vector2 lastPos = Vector2.Zero;
		for (int i = 0; i < base.DynamicVars.Repeat.IntValue; i++)
		{
			Creature enemy = base.Owner.RunState.Rng.CombatTargets.NextItem(base.CombatState.HittableEnemies);
			if (enemy == null)
			{
				continue;
			}
			if (TestMode.IsOff)
			{
				if (i == 0)
				{
					lastPos = NCombatRoom.Instance.GetCreatureNode(Actor).VfxSpawnPosition;
				}
				NCreature targetNode = NCombatRoom.Instance.GetCreatureNode(enemy);
				if (targetNode != null)
				{
					NItemThrowVfx child = NItemThrowVfx.Create(lastPos, targetNode.GetBottomOfHitbox(), ModelDb.Potion<PoisonPotion>().Image);
					NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(child);
					lastPos = targetNode.VfxSpawnPosition;
					await Cmd.Wait(0.5f);
		if (Actor.IsDead) return;
					NSplashVfx child2 = NSplashVfx.Create(targetNode.VfxSpawnPosition, _vfxTint);
					NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(child2);
					NLiquidOverlayVfx child3 = NLiquidOverlayVfx.Create(enemy, _vfxTint);
					NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(child3);
					NGaseousImpactVfx child4 = NGaseousImpactVfx.Create(targetNode.VfxSpawnPosition, _vfxTint);
					NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(child4);
				}
			}
			await PowerCmd.Apply<PoisonPower>(choiceContext, enemy, base.DynamicVars.Poison.BaseValue, Actor, this);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Repeat.UpgradeValueBy(1m);
	}
}
