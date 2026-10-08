// Adapted from STS2 0.111.0 FlameBarrier; source SHA256 8f3cf2ecc74813d99dafec741623da84af9ee9f20df8b7ebeb04137e085618d2.
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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadFlameBarrier : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => false;
	public override string Prototype => "FlameBarrier";
	public override CardModel PrototypeCard => ModelDb.Card<FlameBarrier>();
	private const string _damageBackKey = "DamageBack";

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(12m, ValueProp.Move),
		new DynamicVar("DamageBack", 4m)
	};

	public SquadFlameBarrier()
		: base(2, CardType.Skill, CardRarity.Uncommon, SquadTargeting.Member)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NFireBurningVfx child = NFireBurningVfx.Create(Actor, 0.75f, goingRight: false);
		NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(child);
		await CreatureCmd.GainBlock(cardPlay.Target!, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<FlameBarrierPower>(choiceContext, cardPlay.Target!, base.DynamicVars["DamageBack"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
		base.DynamicVars["DamageBack"].UpgradeValueBy(2m);
	}
}
