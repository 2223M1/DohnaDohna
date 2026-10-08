// Adapted from STS2 0.111.0 TheScythe; source SHA256 21090f0614cc1bc4a86c0e53ef8feeee2a3e888f46f87ea75cb3f532e7dd532a.
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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadTheScythe : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => true;
	public override string Prototype => "TheScythe";
	public override CardModel PrototypeCard => ModelDb.Card<TheScythe>();
	private const string _increaseKey = "Increase";

	private const int _baseDamage = 13;

	private int _currentDamage = 13;

	private int _increasedDamage;

	[SavedProperty]
	public int CurrentDamage
	{
		get
		{
			return _currentDamage;
		}
		set
		{
			AssertMutable();
			_currentDamage = value;
			base.DynamicVars.Damage.BaseValue = _currentDamage;
		}
	}

	[SavedProperty]
	public int IncreasedDamage
	{
		get
		{
			return _increasedDamage;
		}
		set
		{
			AssertMutable();
			_increasedDamage = value;
		}
	}

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(CurrentDamage, ValueProp.Move),
		new IntVar("Increase", 5m)
	};

	public SquadTheScythe()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		int intValue = base.DynamicVars["Increase"].IntValue;
		BuffFromPlay(intValue);
		(base.DeckVersion as SquadTheScythe)?.BuffFromPlay(intValue);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["Increase"].UpgradeValueBy(2m);
	}

	protected override void AfterDowngraded()
	{
		UpdateDamage();
	}

	private void BuffFromPlay(int extraDamage)
	{
		IncreasedDamage += extraDamage;
		UpdateDamage();
	}

	private void UpdateDamage()
	{
		CurrentDamage = 13 + IncreasedDamage;
	}
}
