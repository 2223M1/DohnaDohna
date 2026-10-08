// Adapted from STS2 0.111.0 AllForOne; source SHA256 326ab63e77f78dec05e1a8363ef0bd4cb39bfc15a50e2f88fcfc9cdf8c53fee3.
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
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadAllForOne : SquadCatalogCard
{
	public override string? FixedRole => "joker";
	public override bool IsMelee => false;
	public override string Prototype => "AllForOne";
	public override CardModel PrototypeCard => ModelDb.Card<AllForOne>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(10m, ValueProp.Move)];

	public SquadAllForOne()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_heavy_blunt", null, "blunt_attack.mp3")
			.WithHitVfxSpawnedAtBase()
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		IEnumerable<CardModel> enumerable = PileType.Discard.GetPile(base.Owner).Cards.Where(Filter).ToList();
		foreach (CardModel item in enumerable)
		{
			await CardPileCmd.Add(item, PileType.Hand);
		if (Actor.IsDead) return;
		}
	}

	private bool Filter(CardModel card)
	{
		bool flag = card.EnergyCost.GetWithModifiers(CostModifiers.All) == 0 && !card.EnergyCost.CostsX;
		bool flag2 = flag;
		if (flag2)
		{
			CardType type = card.Type;
			bool flag3 = (uint)(type - 1) <= 2u;
			flag2 = flag3;
		}
		return flag2;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
