// Adapted from STS2 0.111.0 DeadlyPoison; source SHA256 cd1d91f32d2638f938a845d5bdeb9306da6e0966d7d47b28c53c50281f6803b8.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadDeadlyPoison : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "DeadlyPoison";
	public override CardModel PrototypeCard => ModelDb.Card<DeadlyPoison>();
	protected override IEnumerable<string> ExtraRunAssetPaths => NSmokePuffVfx.AssetPaths;

	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<PoisonPower>(5m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<PoisonPower>()];

	public SquadDeadlyPoison()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		NPoisonImpactVfx child = NPoisonImpactVfx.Create(cardPlay.Target);
		NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(child);
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<PoisonPower>(choiceContext, cardPlay.Target, base.DynamicVars.Poison.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Poison.UpgradeValueBy(2m);
	}
}
