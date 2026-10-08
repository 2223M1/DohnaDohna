// Adapted from STS2 0.111.0 Expose; source SHA256 7058ee0ce84c962487eb54b179119a5dcb628df7964b19851b07d0f3b19d267b.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
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
public sealed class SquadExpose : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "Expose";
	public override CardModel PrototypeCard => ModelDb.Card<Expose>();
	private const string _powerKey = "Power";

	protected override IEnumerable<DynamicVar> OriginalVars => [new DynamicVar("Power", 2m)];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[3]
	{
		HoverTipFactory.FromPower<VulnerablePower>(),
		HoverTipFactory.FromPower<ArtifactPower>(),
		HoverTipFactory.Static(StaticHoverTip.Block)
	};

	public SquadExpose()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		VfxCmd.PlayOnCreatureCenter(Actor, "vfx/vfx_flying_slash");
		int amount = base.DynamicVars["Power"].IntValue;
#if DOHNADOHNA_STABLE
        await CreatureCmd.LoseBlock(cardPlay.Target, cardPlay.Target.Block);
#else
		await CreatureCmd.LoseBlock(choiceContext, cardPlay.Target, cardPlay.Target.Block, Actor);
#endif
		if (Actor.IsDead) return;
		if (cardPlay.Target.HasPower<ArtifactPower>())
		{
			await PowerCmd.Remove<ArtifactPower>(cardPlay.Target);
		if (Actor.IsDead) return;
		}
		await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, amount, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["Power"].UpgradeValueBy(1m);
	}
}
