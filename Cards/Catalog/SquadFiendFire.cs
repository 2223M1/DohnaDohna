// Adapted from STS2 0.111.0 FiendFire; source SHA256 276eaa94c0f5b0feb7b86c38f8dd0af95e490d4dc245c6b187adceeaf916466a.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
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
public sealed class SquadFiendFire : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => false;
	public override string Prototype => "FiendFire";
	public override CardModel PrototypeCard => ModelDb.Card<FiendFire>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(7m, ValueProp.Move)];

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<string> ExtraRunAssetPaths => NGroundFireVfx.AssetPaths;

	public SquadFiendFire()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		List<CardModel> list = PileType.Hand.GetPile(base.Owner).Cards.ToList();
		int cardCount = list.Count;
		foreach (CardModel item in list)
		{
			await CardCmd.Exhaust(choiceContext, item);
		if (Actor.IsDead) return;
		}
		float scale = 0.8f;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(cardCount).FromSquadCard(this, cardPlay)
			.Targeting(cardPlay.Target)
			.BeforeDamage(delegate
			{
				NGroundFireVfx nGroundFireVfx = NGroundFireVfx.Create(cardPlay.Target);
				if (nGroundFireVfx == null)
				{
					return Task.CompletedTask;
				}
				SfxCmd.Play("event:/sfx/characters/attack_fire");
				nGroundFireVfx.Scale = Vector2.One * scale;
				NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(nGroundFireVfx);
				scale += 0.1f;
				return Task.CompletedTask;
			})
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
