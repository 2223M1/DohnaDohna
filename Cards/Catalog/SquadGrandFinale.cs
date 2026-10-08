// Adapted from STS2 0.111.0 GrandFinale; source SHA256 629adde00c0bdba520af8dce06f6436d42c4c070add45e651a08ca1b250fb299.
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
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadGrandFinale : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => true;
	public override string Prototype => "GrandFinale";
	public override CardModel PrototypeCard => ModelDb.Card<GrandFinale>();
	protected override bool ShouldGlowGoldInternal => IsPlayable;

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(60m, ValueProp.Move)];

	protected override bool IsOriginalPlayable => PileType.Draw.GetPile(base.Owner).Cards.Count == 0;

	public SquadGrandFinale()
		: base(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NGrandFinaleVfx nGrandFinaleVfx = NGrandFinaleVfx.Create(Actor);
		if (nGrandFinaleVfx != null)
		{
			NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(nGrandFinaleVfx);
			await Cmd.Wait(NGrandFinaleVfx.totalAnticipationDuration);
		if (Actor.IsDead) return;
		}
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).TargetingAllOpponents(base.CombatState)
			.WithHitVfxNode(NGrandFinaleImpactVfx.Create)
			.WithHitFx(null, null, "blunt_attack.mp3")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(15m);
	}
}
