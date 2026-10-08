// Adapted from STS2 0.111.0 SeekerStrike; source SHA256 2d620293babec4b8f0d84a716127019a51c9548ba44ede89abb1ada9e46c726f.
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
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadSeekerStrike : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "SeekerStrike";
	public override CardModel PrototypeCard => ModelDb.Card<SeekerStrike>();
	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new DamageVar(9m, ValueProp.Move),
		new CardsVar(3)
	};

	public SquadSeekerStrike()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromSquadCard(this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
		IEnumerable<CardModel> cardOptions = PileType.Draw.GetPile(base.Owner).Cards.ToList().StableShuffle(base.Owner.RunState.Rng.CombatCardSelection).Take(base.DynamicVars.Cards.IntValue);
		CardModel cardModel = (await CardSelectCmd.FromCombatPile(choiceContext, PileType.Draw.GetPile(base.Owner), base.Owner, new CardSelectorPrefs(base.SelectionScreenPrompt, 1), (CardModel c) => cardOptions.Contains(c))).FirstOrDefault();
		if (Actor.IsDead) return;
		if (cardModel != null)
		{
			await CardPileCmd.Add(cardModel, PileType.Hand);
		if (Actor.IsDead) return;
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
