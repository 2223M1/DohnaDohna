using DohnaDohna.Cards;
using DohnaDohna.Code.Visuals;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Code.Squad;

/// <summary>Role effects and their once-per-combat counters; no HP, powers or card copies.</summary>
public sealed class SquadActions(SquadCombatState squad)
{
    private bool _opened;
    public bool OpeningDone => _opened;
    public Creature ActorFor(CardModel card) => card is SquadCardModel { FixedRole: { } role } && squad.IsAlive(role)
        ? squad.GetActor(role) : squad.Front;

    public Creature[] AuxiliaryCandidates(CardModel card, Creature actor) => card is SquadAttackCard { IsStrike: false }
        && card is not SquadCardModel { IsFallback: true } ? squad.RoleOf(actor) switch
        {
            "medhico" => squad.Living.ToArray(),
            _ => []
        } : [];

    private Task Apply<T>(PlayerChoiceContext context, Creature actor, int amount) where T : PowerModel =>
        amount == 0 ? Task.CompletedTask : PowerCmd.Apply<T>(context, actor, amount, actor, null);

    public async Task TurnStart(PlayerChoiceContext context)
    {
        if (!_opened)
        {
            _opened = true;
            if (squad.IsAlive("kirakira") && squad.GetActor("kirakira").GetPower<DohnaDohna.Powers.KirakiraInnate>() is { } passive)
                await passive.Choose(context);
        }
    }

    public async Task Attack(PlayerChoiceContext context, SquadAttackCard card, CardPlay play)
    {
        var selection = SquadPlaySelection.CurrentFor(card)
            ?? throw new InvalidOperationException("Attack has no native play-series context.");
        var actor = selection.Actor;
        if (actor.IsDead) return;
        var role = squad.RoleOf(actor);
        var command = new AttackCommand(card.DynamicVars.Damage.BaseValue)
#if DOHNADOHNA_STABLE
            .FromCard(card)
#else
            .FromCard(card, play)
#endif
            .WithNoAttackerAnim();
        // Host has no generic pet-attacker setter (FromOsty rejects non-Osty).
        AccessTools.PropertySetter(typeof(AttackCommand), nameof(AttackCommand.Attacker)).Invoke(command, [actor]);
        if (play.Target?.IsAlive != true) return;
        command.Targeting(play.Target);
        if (!card.IsStrike && role == "alyce")
            AccessTools.PropertySetter(typeof(AttackCommand), nameof(AttackCommand.DamageProps)).Invoke(command, [ValueProp.Move | ValueProp.Unblockable]);
        Creature[] targets = [play.Target];
        bool cinematic = SquadFinisher.CanFinish(command, card, play, targets);
        ConfigureNativeImpact(command, role, card.IsStrike);
        await RoleVisuals.PlayAttack(actor, card.IsStrike ? "strike" : "special", ResolveHit, squad.Cancellation.Token, targets, cinematic);
        squad.SyncController();

        async Task ResolveHit()
        {
            if (actor.IsDead) return;
            await command.Execute(context);
            if (card.IsStrike || actor.IsDead) return;
            bool upgraded = card.IsUpgraded;
            int scale = card.EffectMultiplier;
            switch (role)
            {
                case "kuma":
                    foreach (var ally in squad.Living.Where(c => c != actor).ToArray())
                        await PowerCmd.Apply<VigorPower>(context, ally, (upgraded ? 2 : 1) * scale, actor, card);
                    break;
                case "tora": await PowerCmd.Apply<DohnaDohna.Powers.SquadTemporaryStrength>(context, actor, (upgraded ? 2 : 1) * scale, actor, card); break;
                case "kikuchiyo" when command.Results.SelectMany(r => r).Any(r => r.WasTargetKilled): await CardPileCmd.Draw(context, (upgraded ? 2 : 1) * scale, squad.Owner); break;
                case "medhico" when selection.Auxiliary?.IsAlive == true: await CreatureCmd.GainBlock(selection.Auxiliary, (upgraded ? 6 : 4) * scale, ValueProp.Move, play); break;
                case "joker": await Apply<VigorPower>(context, actor, (upgraded ? 5 : 3) * scale); break;
                case "zappa": await CreatureCmd.GainBlock(actor, (upgraded ? 6 : 4) * scale, ValueProp.Move, play); break;
                case "kirakira" when play.Target?.IsAlive == true: await PowerCmd.Apply<PoisonPower>(context, play.Target, (upgraded ? 3 : 2) * scale, actor, card); break;
                case "porno" when play.Target?.IsAlive == true: await PowerCmd.Apply<WeakPower>(context, play.Target, (upgraded ? 2 : 1) * scale, actor, card); break;
                case "antena":
                    for (int i = 0; i < scale && actor.IsAlive; i++)
                        await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.LightningOrb>(context, squad.Owner);
                    break;
            }
        }
    }

    // Only the native command spawns contact feedback, at its actual target and
    // damage node. Short finishers use this same single contact, with a visual accent.
    internal static void ConfigureNativeImpact(AttackCommand command, string role, bool strike)
    {
        switch (role)
        {
            case "antena":
                command.WithHitFx(tmpSfx: "blunt_attack.mp3") // Beam Cell's energy-impact sound.
                    .WithHitVfxNode(NSweepingBeamImpactVfx.Create);
                break;
            case "tora" when strike:
            case "joker" when !strike:
                command.WithHitFx(sfx: "event:/sfx/characters/attack_fire")
                    .WithHitVfxNode(t => NFireBurstVfx.Create(t, .75f));
                break;
            case "kirakira" when !strike:
                // Gas grenade: native poison plume and a compact physical pop.
                command.WithHitFx(tmpSfx: "blunt_attack.mp3").WithHitVfxNode(NPoisonImpactVfx.Create);
                break;
            case "medhico":
            case "zappa":
            case "porno" when !strike:
                command.WithHitFx(tmpSfx: "heavy_attack.mp3").WithHitVfxNode(t =>
                    t.GetCreatureNode() is { } node ? NHeavyBluntVfx.Create(node.GetBottomOfHitbox()) : null);
                break;
            case "kikuchiyo":
            case "porno":
                command.WithHitFx("vfx/vfx_attack_slash", tmpSfx: "slash_attack.mp3").SpawningHitVfxOnEachCreature();
                break;
            case "kuma":
            case "alyce":
            case "tora":
            case "joker":
            case "kirakira":
                command.WithHitFx("vfx/vfx_attack_blunt", tmpSfx: "blunt_attack.mp3").SpawningHitVfxOnEachCreature();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(role), role, "Missing native impact mapping");
        }
    }
}
