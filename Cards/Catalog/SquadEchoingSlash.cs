// Adapted from STS2 0.111.0 EchoingSlash; source SHA256 e6659a3f6e016f19e06e136899726d43cfbf1d7f614f4892bcf1677844ddbfcd.
// Generated baseline; hand-reviewed squad adaptations are maintained here. Do not regenerate over edits.
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Cards.Catalog;

[RegisterCard(typeof(SquadCardPool))]
public sealed class SquadEchoingSlash : SquadCatalogCard
{
	public override string? FixedRole => "kikuchiyo";
	public override bool IsMelee => true;
	public override string Prototype => "EchoingSlash";
	public override CardModel PrototypeCard => ModelDb.Card<EchoingSlash>();
	protected override IEnumerable<DynamicVar> OriginalVars => [new DamageVar(10m, ValueProp.Move)];

	public SquadEchoingSlash()
		: base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
	{
	}

	protected override async Task OnSquadPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
        var squad = SquadCombatState.Get(Owner);
        await DohnaDohna.Code.Visuals.RoleVisuals.PlayAttack(Actor, "special", async () =>
        {
            await using AttackContext attackContext = await AttackCommand.CreateContextAsync(base.CombatState!, choiceContext,
#if DOHNADOHNA_STABLE
                this);
#else
                cardPlay);
#endif
            int attackCount = 1;
            while (attackCount-- > 0 && Actor.IsAlive && base.CombatState!.HittableEnemies.Count > 0)
            {
                var targets = base.CombatState.HittableEnemies.ToArray();
                foreach (var target in targets) VfxCmd.PlayOnCreatureCenter(target, "vfx/vfx_attack_slash");
                MegaCrit.Sts2.Core.Audio.Debug.NDebugAudioManager.Instance?.Play("slash_attack.mp3");
                var results = (await CreatureCmd.Damage(choiceContext, targets, DynamicVars.Damage, Actor, this
#if !DOHNADOHNA_STABLE
                    , cardPlay
#endif
                )).ToArray();
                attackContext.AddHit(results);
                attackCount += results.Count(r => r.WasTargetKilled);
            }
        }, squad.Cancellation.Token, base.CombatState!.HittableEnemies.ToArray());
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
