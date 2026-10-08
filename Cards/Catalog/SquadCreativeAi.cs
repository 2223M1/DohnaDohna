// Adapted from STS2 0.111.0 CreativeAi; source SHA256 b69cded42a10e284d767a373a74e229c4d2c0956deeb61d7fdf0e7d30e692c67.
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

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadCreativeAi : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "CreativeAi";
	public override CardModel PrototypeCard => ModelDb.Card<CreativeAi>();
	private const string _creativeAiKey = "CreativeAi";

	protected override IEnumerable<DynamicVar> OriginalVars => [new DynamicVar("CreativeAi", 1m)];

	public SquadCreativeAi()
		: base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<CreativeAiPower>(choiceContext, Owner.Creature, base.DynamicVars["CreativeAi"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
