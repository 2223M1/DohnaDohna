// Adapted from STS2 0.111.0 Rally; source SHA256 99ab0b13502a14f6e945165157b5288c8a78abb5a30e02e4334c25770b07ddbb.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadRally : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Rally";
	public override CardModel PrototypeCard => ModelDb.Card<Rally>();


	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(12m, ValueProp.Move)];

	public SquadRally()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = SquadCombatState.Get(Owner).Living.ToArray();
		foreach (Creature item in enumerable)
		{
			await CreatureCmd.GainBlock(item, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(5m);
	}
}
