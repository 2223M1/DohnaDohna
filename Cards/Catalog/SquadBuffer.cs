// Adapted from STS2 0.111.0 Buffer; source SHA256 fad565757c0114230a231ea28e19602c7f5a566cc4fbb3491833d1242e7ca1a1.
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
public sealed class SquadBuffer : SquadCatalogCard
{
	public override string? FixedRole => "medhico";
	public override bool IsMelee => false;
	public override string Prototype => "Buffer";
	public override CardModel PrototypeCard => ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Buffer>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<BufferPower>(1m)];

	public SquadBuffer()
		: base(2, CardType.Power, CardRarity.Rare, SquadTargeting.Member)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<BufferPower>(choiceContext, cardPlay.Target!, base.DynamicVars["BufferPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["BufferPower"].UpgradeValueBy(1m);
	}
}
