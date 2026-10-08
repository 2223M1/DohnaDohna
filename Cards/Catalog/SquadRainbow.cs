// Adapted from STS2 0.111.0 Rainbow; source SHA256 5b8716db0d6d7a59d5768d8fffa5909cdc26e0d3f4e6c8d787fd1999d95d01fe.
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
using MegaCrit.Sts2.Core.Models.Orbs;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadRainbow : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Rainbow";
	public override CardModel PrototypeCard => ModelDb.Card<Rainbow>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[4]
	{
		HoverTipFactory.Static(StaticHoverTip.Channeling),
		HoverTipFactory.FromOrb<LightningOrb>(),
		HoverTipFactory.FromOrb<FrostOrb>(),
		HoverTipFactory.FromOrb<DarkOrb>()
	};

	public SquadRainbow()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await OrbCmd.Channel<LightningOrb>(choiceContext, base.Owner);
		if (Actor.IsDead) return;
		await OrbCmd.Channel<FrostOrb>(choiceContext, base.Owner);
		if (Actor.IsDead) return;
		await OrbCmd.Channel<DarkOrb>(choiceContext, base.Owner);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
