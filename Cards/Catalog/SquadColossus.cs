// Adapted from STS2 0.111.0 Colossus; source SHA256 f5d0dff342fdb4d21fffd85143d77e36f6944e5348a608ee19ae6c17748791fc.
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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadColossus : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => false;
	public override string Prototype => "Colossus";
	public override CardModel PrototypeCard => ModelDb.Card<Colossus>();
	private const string _powerVarName = "Colossus";

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(4m, ValueProp.Move),
		new DynamicVar("Colossus", 1m)
	};

	public SquadColossus()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<ColossusPower>(choiceContext, Actor, base.DynamicVars["Colossus"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
