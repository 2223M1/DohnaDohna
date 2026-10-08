// Adapted from STS2 0.111.0 Footwork; source SHA256 03c45f76eb2ac123d7211be498fec4b4598084360f31a670740e138a584a0e47.
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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadFootwork : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "Footwork";
	public override CardModel PrototypeCard => ModelDb.Card<Footwork>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new PowerVar<DexterityPower>(2m)];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<DexterityPower>()];

	public SquadFootwork()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "PowerUp", base.Owner.Character.PowerUpAnimDelay);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<DexterityPower>(choiceContext, Actor, base.DynamicVars.Dexterity.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Dexterity.UpgradeValueBy(1m);
	}
}
