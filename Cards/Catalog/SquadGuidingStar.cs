// Adapted from STS2 0.111.0 GuidingStar; source SHA256 86e5b065e570473bb93cdc6c0a04e9e61cdcdb4f5eb832b0ad6daf5bd537b50f.
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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadGuidingStar : SquadCatalogCard
{
	public override string? FixedRole => null;
	public override bool IsMelee => false;
	public override string Prototype => "GuidingStar";
	public override CardModel PrototypeCard => ModelDb.Card<GuidingStar>();
	private const string _guidingStarSfx = "event:/sfx/characters/regent/regent_guiding_star";

	public override int CanonicalStarCost => 1;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(12m, ValueProp.Move),
		new CardsVar(2)
	};

	public SquadGuidingStar()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(cardPlay.Target);
		if (nCreature != null)
		{
			SfxCmd.Play("event:/sfx/characters/regent/regent_guiding_star");
			NSmallMagicMissileVfx nSmallMagicMissileVfx = NSmallMagicMissileVfx.Create(nCreature.GetBottomOfHitbox(), new Color("50b598"));
			NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(nSmallMagicMissileVfx);
			await Cmd.Wait(nSmallMagicMissileVfx.WaitTime);
		if (Actor.IsDead) return;
		}
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithNoAttackerAnim().FromSquadCard(this, cardPlay)
			.Targeting(cardPlay.Target)
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, base.DynamicVars.Cards.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
		base.DynamicVars.Cards.UpgradeValueBy(1m);
	}
}
