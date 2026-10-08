// Adapted from STS2 0.111.0 ManifestAuthority; source SHA256 25eb61f438b18860ac777b88c2af1b30c6899854f0012f529dd7c1dad865326a.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadManifestAuthority : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "ManifestAuthority";
	public override CardModel PrototypeCard => ModelDb.Card<ManifestAuthority>();
	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new BlockVar(7m, ValueProp.Move)];

	public SquadManifestAuthority()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		CardModel cardModel = CardFactory.GetDistinctForCombat(base.Owner, ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(base.Owner.UnlockState, base.Owner.RunState.CardMultiplayerConstraint), 1, base.Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
		if (cardModel != null)
		{
			if (base.IsUpgraded)
			{
				CardCmd.Upgrade(cardModel);
			}
			await CardPileCmd.AddGeneratedCardToCombat(cardModel, PileType.Hand, base.Owner);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(1m);
	}
}
