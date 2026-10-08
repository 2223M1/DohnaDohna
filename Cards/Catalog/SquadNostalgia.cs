// Adapted from STS2 0.111.0 Nostalgia; source SHA256 09b033d8ad51337a2a22e0bf423e5783d3bfa6ca582299a4813fc2c8918f4a34.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadNostalgia : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Nostalgia";
	public override CardModel PrototypeCard => ModelDb.Card<Nostalgia>();
	public SquadNostalgia()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<NostalgiaPower>(choiceContext, Owner.Creature, 1m, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
