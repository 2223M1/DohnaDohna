// Adapted from STS2 0.111.0 SwordBoomerang; source SHA256 d2ad1c2e795b3c8b8e96af74f0cb1dd79b61bb5f9be7aec716184bd98f4649dc.
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
public sealed class SquadSwordBoomerang : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "SwordBoomerang";
	public override CardModel PrototypeCard => ModelDb.Card<SwordBoomerang>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(3m, ValueProp.Move),
		new RepeatVar(3)
	};

	public SquadSwordBoomerang()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.RandomEnemy)
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
