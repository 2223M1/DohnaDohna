// Adapted from STS2 0.111.0 FightThrough; source SHA256 d4a4e9e323cebf6d6d986b6b509bd8f9d0e61b78777e888c2fa7c3a18c0991ff.
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
public sealed class SquadFightThrough : SquadCatalogCard
{
	public override string? FixedRole => "zappa";
	public override bool IsMelee => false;
	public override string Prototype => "FightThrough";
	public override CardModel PrototypeCard => ModelDb.Card<FightThrough>();
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<Wound>()];

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(13m, ValueProp.Move)];

	public SquadFightThrough()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		for (int i = 0; i < 2; i++)
		{
			CardModel card = base.CombatState.CreateCard<Wound>(base.Owner);
			CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, base.Owner));
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
	}
}
