// Adapted from STS2 0.111.0 Prowess; source SHA256 dcd2a9c500df46e23a0fc9ddfca6218b6240ee2c9eb213c7dba09a1756a2bc96.
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
public sealed class SquadProwess : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Prowess";
	public override CardModel PrototypeCard => ModelDb.Card<Prowess>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>()
	};

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new PowerVar<StrengthPower>(1m),
		new PowerVar<DexterityPower>(1m)
	};

	public SquadProwess()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<StrengthPower>(choiceContext, Actor, base.DynamicVars.Strength.BaseValue, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<DexterityPower>(choiceContext, Actor, base.DynamicVars.Dexterity.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Dexterity.UpgradeValueBy(1m);
		base.DynamicVars.Strength.UpgradeValueBy(1m);
	}
}
