// Adapted from STS2 0.111.0 Backstab; source SHA256 6a26f646b66728684b269f962aed3b8f4db791fdd320e25929c56839d39372e5.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadBackstab : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => true;
	public override string Prototype => "Backstab";
	public override CardModel PrototypeCard => ModelDb.Card<Backstab>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Exhaust,
		CardKeyword.Innate
	};

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(11m, ValueProp.Move)];

	public SquadBackstab()
		: base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_dramatic_stab", null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
