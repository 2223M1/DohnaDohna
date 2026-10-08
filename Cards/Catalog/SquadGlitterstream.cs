// Adapted from STS2 0.111.0 Glitterstream; source SHA256 07a11d9182a59adf52bee4236ca65d395ff769474b4aa8e59554a24f0f89a4ec.
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
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadGlitterstream : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "Glitterstream";
	public override CardModel PrototypeCard => ModelDb.Card<Glitterstream>();
	private const string _blockNextTurnKey = "BlockNextTurn";

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(11m, ValueProp.Move),
		new BlockVar("BlockNextTurn", 5m, ValueProp.Move)
	};

	public SquadGlitterstream()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		BlockVar blockVar = (BlockVar)base.DynamicVars["BlockNextTurn"];
		IEnumerable<AbstractModel> modifiers;
		decimal blockNextTurnAmount = Hook.ModifyBlock(base.CombatState, Actor, blockVar.BaseValue, blockVar.Props, this, cardPlay, out modifiers);
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Actor, blockNextTurnAmount, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(2m);
		base.DynamicVars["BlockNextTurn"].UpgradeValueBy(2m);
	}
}
