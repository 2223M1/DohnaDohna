// Adapted from STS2 0.111.0 Adrenaline; source SHA256 596def70723291255840c345b0be949a930fe14713560034bd25cae2abd1de92.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadAdrenaline : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "Adrenaline";
	public override CardModel PrototypeCard => ModelDb.Card<Adrenaline>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new EnergyVar(1),
		new CardsVar(2)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [base.EnergyHoverTip];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadAdrenaline()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (LocalContext.IsMe(base.Owner))
		{
			VfxCmd.PlayFullScreenInCombat("vfx/vfx_adrenaline", Actor);
		}
		await PlayerCmd.GainEnergy(base.DynamicVars.Energy.IntValue, base.Owner);
		if (Actor.IsDead) return;
		await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Energy.UpgradeValueBy(1m);
	}
}
