// Adapted from STS2 0.111.0 Whirlwind; source SHA256 ce47ea235c844e69af17f67c37c0a95bc4d2cfc9168decfe88c7b7a833193aa4.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadWhirlwind : SquadCatalogCard
{
	public override string? FixedRole => "tora";
	public override bool IsMelee => true;
	public override string Prototype => "Whirlwind";
	public override CardModel PrototypeCard => ModelDb.Card<Whirlwind>();
	private const string _whirlwindSfx = "event:/sfx/characters/ironclad/ironclad_whirlwind";

	protected override bool HasEnergyCostX => true;

	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(5m, ValueProp.Move)];

	public SquadWhirlwind()
		: base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int num = ResolveEnergyXValue();
		if (num > 0)
		{
			Color color = new Color("FFFFFF80");
			double num2 = ((SaveManager.Instance.PrefsSave.FastMode == FastModeType.Fast) ? 0.2 : 0.3);
			NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NHorizontalLinesVfx.Create(color, 0.8 + (double)Mathf.Min(8, num) * num2));
			SfxCmd.Play("event:/sfx/characters/ironclad/ironclad_whirlwind");
			NRun.Instance?.GlobalUi.AddChildSafely(NSmokyVignetteVfx.Create(color, color));
		}
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(num).FromSquadCard(this, cardPlay)
			.TargetingAllOpponents(base.CombatState)
			.WithHitFx("vfx/vfx_giant_horizontal_slash")
			.ExecuteSquad(choiceContext);
		if (Actor.IsDead) return;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
