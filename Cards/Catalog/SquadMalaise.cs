// Adapted from STS2 0.111.0 Malaise; source SHA256 bc019caf7edfffcf25a95346ad140e36df2cad298a7f36ebf62d7ed25035b3c7.
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
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadMalaise : SquadCatalogCard
{
	public override string? FixedRole => "porno";
	public override bool IsMelee => false;
	public override string Prototype => "Malaise";
	public override CardModel PrototypeCard => ModelDb.Card<Malaise>();

	protected override bool HasEnergyCostX => true;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<WeakPower>()
	};

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadMalaise()
		: base(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		int powerAmount = ResolveEnergyXValue();
		if (base.IsUpgraded)
		{
			powerAmount++;
		}
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target, -powerAmount, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, powerAmount, Actor, this);
		if (Actor.IsDead) return;
	}
}
