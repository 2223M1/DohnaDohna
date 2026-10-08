// Adapted from STS2 0.111.0 Patter; source SHA256 42f928df93254ea0021df4ea6f244e8acc9523f29448b577497101453bd150c0.
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
public sealed class SquadPatter : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "Patter";
	public override CardModel PrototypeCard => ModelDb.Card<Patter>();
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(8m, ValueProp.Move),
		new PowerVar<VigorPower>(2m)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

	public SquadPatter()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<VigorPower>(choiceContext, Actor, base.DynamicVars["VigorPower"].IntValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(2m);
		base.DynamicVars["VigorPower"].UpgradeValueBy(1m);
	}
}
