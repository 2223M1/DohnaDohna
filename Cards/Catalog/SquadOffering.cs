// Adapted from STS2 0.111.0 Offering; source SHA256 a95f77e207ef9ffa1721ed28340b7fd7061ab6cd3973e76045506a9d381d7d05.
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
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadOffering : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Offering";
	public override CardModel PrototypeCard => ModelDb.Card<Offering>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[3]
	{
		new HpLossVar(6m),
		new EnergyVar(2),
		new CardsVar(3)
	};

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [base.EnergyHoverTip];

	public SquadOffering()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.Damage(choiceContext, Actor, base.DynamicVars.HpLoss.BaseValue, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Actor, this
#if !DOHNADOHNA_STABLE
            , cardPlay
#endif
        );
		if (Actor.IsDead) return;
		await PlayerCmd.GainEnergy(base.DynamicVars.Energy.IntValue, base.Owner);
		if (Actor.IsDead) return;
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Cards.UpgradeValueBy(2m);
	}
}
