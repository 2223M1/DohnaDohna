// Adapted from STS2 0.111.0 PrepTime; source SHA256 898cef0b15c076358b9c466a71e4f07e581291be1056f747fa57a9784dc7d62e.
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
public sealed class SquadPrepTime : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "PrepTime";
	public override CardModel PrototypeCard => ModelDb.Card<PrepTime>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<PrepTimePower>(4m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

	public SquadPrepTime()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<PrepTimePower>(choiceContext, Actor, base.DynamicVars["PrepTimePower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["PrepTimePower"].UpgradeValueBy(2m);
	}
}
