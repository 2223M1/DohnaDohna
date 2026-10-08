// Adapted from STS2 0.111.0 Transfigure; source SHA256 98e919fcafdb3df3937c9b71ba5b799beb06f4e6555f4680a239e6270fe2bd4e.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadTransfigure : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "Transfigure";
	public override CardModel PrototypeCard => ModelDb.Card<Transfigure>();
	public override bool CanBeGeneratedInCombat => false;

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => [new EnergyVar(1)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		base.EnergyHoverTip,
		HoverTipFactory.Static(StaticHoverTip.ReplayStatic)
	};

	public SquadTransfigure()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<CardModel> cardToTransfigure = await CardSelectCmd.FromHand(choiceContext, base.Owner, new CardSelectorPrefs(base.SelectionScreenPrompt, 1), null, this);
		if (Actor.IsDead) return;
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		foreach (CardModel item in cardToTransfigure)
		{
			if (!item.EnergyCost.CostsX && item.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0)
			{
				item.EnergyCost.AddThisCombat(1);
			}
			item.BaseReplayCount++;
		}
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Exhaust);
	}
}
