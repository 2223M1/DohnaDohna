// Adapted from STS2 0.111.0 Blur; source SHA256 d9db2f6fb8620fa36edc5ab1aa64f518eb677a533b7273f895e34a8a0bc14cb0.
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
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadBlur : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "Blur";
	public override CardModel PrototypeCard => ModelDb.Card<Blur>();
	private const string _blurKey = "Blur";

	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new BlockVar(5m, ValueProp.Move),
		new DynamicVar("Blur", 1m)
	};

	public SquadBlur()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(Actor, base.DynamicVars.Block, cardPlay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<BlurPower>(choiceContext, Actor, base.DynamicVars["Blur"].BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
