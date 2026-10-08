// Adapted from STS2 0.111.0 Pinpoint; source SHA256 27183262c1b9138ecfa5e8924e4c5827b44dee8c02254ce6dc46306406abd5c4.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadPinpoint : SquadCatalogCard
{
	public override string? FixedRole => "alyce";
	public override bool IsMelee => false;
	public override string Prototype => "Pinpoint";
	public override CardModel PrototypeCard => ModelDb.Card<Pinpoint>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(15m, ValueProp.Move)];

	public SquadPinpoint()
		: base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}

    // Intrinsic discount is separate from native external local modifiers, so substitution can omit it.
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card != this || IsFallback) return false;
        int count = CombatManager.Instance.History.CardPlaysFinished.Count(e => e.CardPlay.Card.Type == CardType.Skill
            && e.CardPlay.Card.Owner == Owner && e.HappenedThisTurn(CombatState));
        modifiedCost = Math.Max(0, originalCost - count);
        return count != 0;
    }
}
