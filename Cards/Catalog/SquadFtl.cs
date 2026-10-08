// Adapted from STS2 0.111.0 Ftl; source SHA256 161b2491bb79117311989996db3be27e2cd2207daf9a3decaaae615e733284ab.
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
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadFtl : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "Ftl";
	public override CardModel PrototypeCard => ModelDb.Card<Ftl>();
	private const string _playMaxKey = "PlayMax";

	protected override bool ShouldGlowGoldInternal => CanDrawCard;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[3]
	{
		new DamageVar(5m, ValueProp.Move),
		new IntVar("PlayMax", 3m),
		new CardsVar(1)
	};

	private bool CanDrawCard
	{
		get
		{
			int num = CombatManager.Instance.History.CardPlaysFinished.Count((CardPlayFinishedEntry e) => e.HappenedThisTurn(base.CombatState) && e.CardPlay.Card.Owner == base.Owner);
			return num < base.DynamicVars["PlayMax"].IntValue;
		}
	}

	public SquadFtl()
		: base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		if (CanDrawCard)
		{
			await CardPileCmd.Draw(choiceContext, base.DynamicVars.Cards.BaseValue, base.Owner);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
		base.DynamicVars["PlayMax"].UpgradeValueBy(1m);
	}
}
