using DohnaDohna.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.Code.Squad;

/// <summary>Read-only eligibility for fixed, single-hit native commands. Never simulates their execution.</summary>
internal static class SquadFinisher
{
    // Exact fields checked against both supported hosts; dynamic/random attacks
    // and pre-hit callbacks stay ordinary because their outcome is not known yet.
    private static readonly AccessTools.FieldRef<AttackCommand, decimal> Damage = AccessTools.FieldRefAccess<AttackCommand, decimal>("_damagePerHit");
    private static readonly AccessTools.FieldRef<AttackCommand, int> Hits = AccessTools.FieldRefAccess<AttackCommand, int>("_hitCount");
    private static readonly System.Reflection.FieldInfo Calculated = AccessTools.Field(typeof(AttackCommand), "_calculatedDamageVar");
    private static readonly System.Reflection.FieldInfo BeforeDamage = AccessTools.Field(typeof(AttackCommand), "_beforeDamage");
    private static readonly System.Reflection.FieldInfo AfterAnimation = AccessTools.Field(typeof(AttackCommand), "_afterAttackerAnim");

    internal static bool CanFinish(AttackCommand command, CardModel card, CardPlay? play, IReadOnlyList<Creature> targets)
    {
        var actor = command.Attacker ?? throw new InvalidOperationException("Squad attack has no actor.");
        var combat = actor.CombatState;
        var run = card.Owner.RunState;
        if (combat == null || actor.IsDead || targets.Count == 0 || command.IsRandomlyTargeted
            || Calculated.GetValue(command) != null || BeforeDamage.GetValue(command) != null || AfterAnimation.GetValue(command) != null
            || Hook.ShouldStopCombatFromEnding(combat)
            || Hook.ModifyAttackHitCount(combat, command, Hits(command)) != 1) return false;
        var primary = combat.Enemies.Where(c => c.IsAlive && c.IsPrimaryEnemy).ToArray();
        if (primary.Length == 0 || primary.Any(c => !targets.Contains(c))) return false;

        // Unknown gameplay content may have pre-hit/death side effects. Passive
        // run observers (metrics, audio, lifecycle) are not attack participants;
        // merely registering one must not disable every finisher in other mods.
        if (run.IterateHookListeners(combat).Any(model =>
            model is CardModel or PowerModel or MonsterModel or RelicModel or PotionModel && IsExternal(model)))
            return false;
        // Reflection can kill the actor before the attack's remaining targets.
        // Leave these attacks ordinary instead of speculative lethal protection.
        if (targets.Any(c => c.HasPower<ThornsPower>())) return false;

        foreach (var target in primary)
        {
            if (!Hook.ShouldDie(run, combat, target, out _)) return false;
            decimal damage = Hook.ModifyDamage(run, combat, target, actor, Damage(command),
                command.DamageProps, card,
#if !DOHNADOHNA_STABLE
                play,
#endif
                ModifyDamageHookType.All, CardPreviewMode.Normal, out var modifiers);
            if (modifiers.Any(IsExternal)) return false;
            decimal block = command.DamageProps.HasFlag(ValueProp.Unblockable) ? 0 : Math.Min(target.Block, damage);
            decimal lost = Hook.ModifyHpLost(run, combat, target, Math.Max(0, damage - block), command.DamageProps,
                actor, card, HpLossHookPhase.BeforeOsty, out modifiers);
            if (modifiers.Any(IsExternal)) return false;
            if (Hook.ModifyUnblockedDamageTarget(combat, target, lost, command.DamageProps, actor) != target) return false;
            lost = Hook.ModifyHpLost(run, combat, target, lost, command.DamageProps, actor, card, HpLossHookPhase.AfterOsty, out modifiers);
            if (modifiers.Any(IsExternal)) return false;
            if (lost < target.CurrentHp) return false;
        }
        return true;
    }

    private static bool IsExternal(AbstractModel model) => model.GetType().Assembly != typeof(CardModel).Assembly
        && model.GetType().Assembly != typeof(SquadFinisher).Assembly;
}
