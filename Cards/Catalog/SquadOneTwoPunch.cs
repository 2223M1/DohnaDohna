// Adapted from STS2 0.111.0 OneTwoPunch; source SHA256 c6b3cf08f00742415d55da841989c93c1ca5576beceb3b11858265df1c32e2e3.
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
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadOneTwoPunch : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "OneTwoPunch";
	public override CardModel PrototypeCard => ModelDb.Card<OneTwoPunch>();
	private const string _attacksKey = "Attacks";

	protected override IEnumerable<DynamicVar> OriginalVars => [new DynamicVar("Attacks", 1m)];

	public SquadOneTwoPunch()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<OneTwoPunchPower>(choiceContext, Owner.Creature, base.DynamicVars["Attacks"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["Attacks"].UpgradeValueBy(1m);
	}
}
