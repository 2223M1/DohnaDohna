// Adapted from STS2 0.111.0 Envenom; source SHA256 a844d07c07afee5265ddc070cfbd5fb37451e52444228702b687d54a6bb5ea39.
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

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadEnvenom : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "Envenom";
	public override CardModel PrototypeCard => ModelDb.Card<Envenom>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<EnvenomPower>(1m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

	public SquadEnvenom()
		: base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<EnvenomPower>(choiceContext, Actor, base.DynamicVars["EnvenomPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["EnvenomPower"].UpgradeValueBy(1m);
	}
}
