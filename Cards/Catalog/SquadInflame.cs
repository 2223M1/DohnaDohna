// Adapted from STS2 0.111.0 Inflame; source SHA256 adacf6eebe8337b67563e62dfee2bb3370fb65ff99bb5072ae2360cf966ad810.
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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadInflame : SquadCatalogCard
{
	public override string? FixedRole => "kuma";
	public override bool IsMelee => false;
	public override string Prototype => "Inflame";
	public override CardModel PrototypeCard => ModelDb.Card<Inflame>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<StrengthPower>(2m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

	protected override IEnumerable<string> ExtraRunAssetPaths => NGroundFireVfx.AssetPaths;

	public SquadInflame()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		NPowerUpVfx.CreateNormal(Actor);
		await PowerCmd.Apply<StrengthPower>(choiceContext, Actor, base.DynamicVars["StrengthPower"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	public override async Task OnEnqueuePlayVfx(Creature? target)
	{
		if (IsFallback) return;
		NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NGroundFireVfx.Create(Actor));
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["StrengthPower"].UpgradeValueBy(1m);
	}
}
