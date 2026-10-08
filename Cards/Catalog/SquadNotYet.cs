// Adapted from STS2 0.111.0 NotYet; source SHA256 21ef8b51093329ca22f1fb830b3d18c39863cf8c5deb94339a15c0dbaf2923a2.
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

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadNotYet : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "NotYet";
	public override CardModel PrototypeCard => ModelDb.Card<NotYet>();
	public override bool CanBeGeneratedInCombat => false;

	protected override IEnumerable<DynamicVar> OriginalVars => [new HealVar(10m)];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	public SquadNotYet()
		: base(2, CardType.Skill, CardRarity.Rare, SquadTargeting.Member)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await CreatureCmd.Heal(cardPlay.Target!, base.DynamicVars.Heal.BaseValue);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Heal.UpgradeValueBy(3m);
	}
}
