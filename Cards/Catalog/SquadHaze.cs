// Adapted from STS2 0.111.0 Haze; source SHA256 2417c3b4e5f1da21c35397aaeb39a244ceb9e6882c2d6b082b2aa8f7618810b8.
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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadHaze : SquadCatalogCard
{
	public override string? FixedRole => "kirakira";
	public override bool IsMelee => false;
	public override string Prototype => "Haze";
	public override CardModel PrototypeCard => ModelDb.Card<Haze>();
	protected override IEnumerable<DynamicVar> OriginalVars => new DynamicVar[2]
	{
		new PowerVar<PoisonPower>(4m),
		new PowerVar<WeakPower>(1m)
	};

	protected override IEnumerable<IHoverTip> AdditionalHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<PoisonPower>(),
		HoverTipFactory.FromPower<WeakPower>()
	};

	public SquadHaze()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(Actor, "Cast", base.Owner.Character.CastAnimDelay);
		if (Actor.IsDead) return;
		SpawnVfx();
		await Cmd.CustomScaledWait(0.2f, 0.4f);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<PoisonPower>(choiceContext, base.CombatState?.HittableEnemies, base.DynamicVars.Poison.BaseValue, Actor, this);
		if (Actor.IsDead) return;
		await PowerCmd.Apply<WeakPower>(choiceContext, base.CombatState?.HittableEnemies, base.DynamicVars.Weak.BaseValue, Actor, this);
		if (Actor.IsDead) return;
	}

	private void SpawnVfx()
	{
		Node node = NCombatRoom.Instance?.CombatVfxContainer;
		if (node == null)
		{
			return;
		}
		NSmokyVignetteVfx child = NSmokyVignetteVfx.Create(new Color(0.8f, 0.8f, 0.3f, 0.66f), new Color(0f, 4f, 0f, 0.33f));
		node.AddChildSafely(child);
		foreach (Creature hittableEnemy in base.CombatState.HittableEnemies)
		{
			node.AddChildSafely(NSmokePuffVfx.Create(hittableEnemy, NSmokePuffVfx.SmokePuffColor.Green));
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Poison.UpgradeValueBy(2m);
		base.DynamicVars.Weak.UpgradeValueBy(1m);
	}
}
