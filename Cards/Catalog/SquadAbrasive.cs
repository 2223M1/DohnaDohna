// Adapted from STS2 0.111.0 Abrasive; source SHA256 843912702bf205461319e5d85fc5dafe230134f9f8af5359097f79b7880fa1ba.
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
public sealed class SquadAbrasive : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => false;
	public override string Prototype => "Abrasive";
	public override CardModel PrototypeCard => ModelDb.Card<Abrasive>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<DexterityPower>(),
		HoverTipFactory.FromPower<ThornsPower>()
	};

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new PowerVar<ThornsPower>(4m),
		new PowerVar<DexterityPower>(1m)
	};

	public SquadAbrasive()
		: base(3, CardType.Power, CardRarity.Rare, SquadTargeting.Member)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<DexterityPower>(choiceContext, cardPlay.Target!, base.DynamicVars.Dexterity.BaseValue, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<ThornsPower>(choiceContext, cardPlay.Target!, base.DynamicVars["ThornsPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["ThornsPower"].UpgradeValueBy(2m);
	}
}
