// Adapted from STS2 0.111.0 Lethality; source SHA256 13b66d1eaf1fb0a6405fb39ad64a59e0f99fc605c8c5b3162171981bc7a97cf6.
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
public sealed class SquadLethality : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => false;
	public override string Prototype => "Lethality";
	public override CardModel PrototypeCard => ModelDb.Card<Lethality>();
	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<LethalityPower>(50m)];

	public SquadLethality()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<LethalityPower>(choiceContext, Actor, base.DynamicVars["LethalityPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["LethalityPower"].UpgradeValueBy(25m);
	}
}
