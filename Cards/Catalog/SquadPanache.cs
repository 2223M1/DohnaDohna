// Adapted from STS2 0.111.0 Panache; source SHA256 699682dd9480bb42d7c4e172ea0e5b8c678ed06241a15641fef76cf1562e208c.
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
public sealed class SquadPanache : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "Panache";
	public override CardModel PrototypeCard => ModelDb.Card<Panache>();
	private const string _powerKey = "PanacheDamage";

	protected override IEnumerable<DynamicVar> OriginalVars => [new DynamicVar("PanacheDamage", 10m)];

	public SquadPanache()
		: base(0, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PowerCmd.Apply<PanachePower>(choiceContext, Owner.Creature, base.DynamicVars["PanacheDamage"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["PanacheDamage"].UpgradeValueBy(4m);
	}
}
