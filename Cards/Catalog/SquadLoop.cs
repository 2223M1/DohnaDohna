// Adapted from STS2 0.111.0 Loop; source SHA256 0fdf0ed7c92c23008a44839943b7ded8f1388d071a2e5151da3bc60c34070dcd.
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
public sealed class SquadLoop : SquadCatalogCard
{
	public override string? FixedRole => "antena";
	public override bool IsMelee => false;
	public override string Prototype => "Loop";
	public override CardModel PrototypeCard => ModelDb.Card<Loop>();
	private const string _loopKey = "Loop";

	protected override IEnumerable<DynamicVar> OriginalVars => [new DynamicVar("Loop", 1m)];

	public SquadLoop()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<LoopPower>(choiceContext, Actor, base.DynamicVars["Loop"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["Loop"].UpgradeValueBy(1m);
	}
}
