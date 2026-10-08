// Adapted from STS2 0.111.0 Thrash; source SHA256 25d98dbb144d010a1d995009f4adeb6941687d9c3d04c24870dde8c829350fc4.
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
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadThrash : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => true;
	public override string Prototype => "Thrash";
	public override CardModel PrototypeCard => ModelDb.Card<Thrash>();
	private decimal _extraDamage;

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(4m, ValueProp.Move)];

	private decimal ExtraDamage
	{
		get
		{
			return _extraDamage;
		}
		set
		{
			AssertMutable();
			_extraDamage = value;
		}
	}

	public SquadThrash()
		: base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(2).FromSquadCard(this, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_thrash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		CardPile pile = PileType.Hand.GetPile(base.Owner);
		CardModel cardModel = base.Owner.RunState.Rng.CombatCardSelection.NextItem(pile.Cards.Where((CardModel c) => c.Type == CardType.Attack));
		if (cardModel != null)
		{
			decimal damage = default(decimal);
			if (cardModel.DynamicVars.ContainsKey("CalculatedDamage"))
			{
				damage = cardModel.DynamicVars.CalculatedDamage.Calculate(null);
			}
			else if (cardModel.DynamicVars.ContainsKey("Damage"))
			{
				damage = cardModel.DynamicVars.Damage.BaseValue;
			}
			else if (cardModel.DynamicVars.ContainsKey("OstyDamage"))
			{
				damage = cardModel.DynamicVars.OstyDamage.BaseValue;
			}
			else
			{
				Log.Warn(base.Id.Entry + " exhausted attack card " + cardModel.Id.Entry + " that did not have an appropriate damage var!");
			}
			damage = Hook.ModifyDamage(base.Owner.RunState, Actor.CombatState, null, Actor, damage, ValueProp.Move, cardModel,
#if !DOHNADOHNA_STABLE
                null,
#endif
                ModifyDamageHookType.All, CardPreviewMode.None, out IEnumerable<AbstractModel> _);
			base.DynamicVars.Damage.BaseValue += damage;
			ExtraDamage += damage;
			await CardCmd.Exhaust(choiceContext, cardModel);
		if (Actor.IsDead) return;
		}
	}

	protected override void AfterDowngraded()
	{
		base.AfterDowngraded();
		base.DynamicVars.Damage.BaseValue += ExtraDamage;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
