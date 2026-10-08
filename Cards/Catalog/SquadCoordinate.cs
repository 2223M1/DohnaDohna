// Adapted from STS2 0.111.0 Coordinate; source SHA256 e13c6beba491c6b86e729f88eb7c975a654db2da3f17d98669a7b375eec005e0.
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
public sealed class SquadCoordinate : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Coordinate";
	public override CardModel PrototypeCard => ModelDb.Card<Coordinate>();


	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<StrengthPower>(5m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

	public SquadCoordinate()
		: base(1, CardType.Skill, CardRarity.Uncommon, SquadTargeting.OtherMember)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		decimal baseValue = base.DynamicVars.Strength.BaseValue;
		await PowerCmd.Apply<CoordinatePower>(choiceContext, cardPlay.Target, baseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Strength.UpgradeValueBy(3m);
	}
}
