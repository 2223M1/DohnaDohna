// Adapted from STS2 0.111.0 Defragment; source SHA256 80ee08a81619587aafe7c152604d933367c9a12798c8e9da82a6742f2155df80.
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
public sealed class SquadDefragment : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Defragment";
	public override CardModel PrototypeCard => ModelDb.Card<Defragment>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<FocusPower>()];

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<FocusPower>(1m)];

	public SquadDefragment()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<FocusPower>(choiceContext, Actor, base.DynamicVars["FocusPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["FocusPower"].UpgradeValueBy(1m);
	}
}
