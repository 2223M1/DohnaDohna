// Adapted from STS2 0.111.0 Ricochet; source SHA256 94ecf31f64c61c587e5a756a78c0944bd58006be1581b352907b536eba49c8ea.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadRicochet : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "Ricochet";
	public override CardModel PrototypeCard => ModelDb.Card<Ricochet>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(3m, ValueProp.Move),
		new RepeatVar(4)
	};

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];

	public SquadRicochet()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(base.DynamicVars.Repeat.IntValue).FromSquadCard(this, cardPlay)
			.TargetingRandomOpponents(base.CombatState)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Repeat.UpgradeValueBy(1m);
	}
}
