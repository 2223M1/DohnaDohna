// Adapted from STS2 0.111.0 Reflect; source SHA256 ba36932afda165dc53dbc8cc94b6e7206ad9c4a589e5d57326945a518940007b.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadReflect : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "Reflect";
	public override CardModel PrototypeCard => ModelDb.Card<Reflect>();
	public override int CanonicalStarCost => 3;

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(15m, ValueProp.Move)];

	public SquadReflect()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<ReflectPower>(choiceContext, Actor, 1m, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(5m);
	}
}
