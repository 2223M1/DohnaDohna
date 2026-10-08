// Adapted from STS2 0.111.0 SweepingBeam; source SHA256 4e5105566ba7c7e4af65c0af9ddbf2b8e200b35821482b981e5a8ca062d55071.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadSweepingBeam : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "SweepingBeam";
	public override CardModel PrototypeCard => ModelDb.Card<SweepingBeam>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(6m, ValueProp.Move),
		new CardsVar(1)
	};

	public SquadSweepingBeam()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Attack", base.Owner.Character.AttackAnimDelay);
		if (Actor.IsDead) return;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).TargetingAllOpponents(base.CombatState)
			.WithAttackerAnim("Cast", 0.5f)
			.BeforeDamage(async delegate
			{
				List<Creature> targets = base.CombatState.HittableEnemies.ToList();
		if (Actor.IsDead) return;
				NSweepingBeamVfx nSweepingBeamVfx = NSweepingBeamVfx.Create(Actor, targets);
				if (nSweepingBeamVfx != null)
				{
					NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(nSweepingBeamVfx);
					await Cmd.Wait(0.5f);
		if (Actor.IsDead) return;
				}
			})
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
