using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using DohnaDohna.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadStartPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_start";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.BeforeCombatStart), [typeof(IRunState), typeof(ICombatState)])];
    public static void Postfix(ICombatState? combatState, ref Task __result) => __result = Finish(__result, combatState);
    private static async Task Finish(Task original, ICombatState? combat)
    {
        await original;
        if (combat == null) return;
        foreach (var owner in combat.Players.Where(p => p.Character is DohnaSquad).ToArray())
        {
            await SquadCombatState.Create(owner);
            await PowerCmd.Apply<SquadRules>(new BlockingPlayerChoiceContext(), owner.Creature, 1, owner.Creature, null);
        }
    }
}

public sealed class SquadDamageTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_damage_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
        [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)
#if !DOHNADOHNA_STABLE
        , typeof(CardPlay)
#endif
        ])];
    public static void Prefix(ref IEnumerable<Creature>? targets, ref Creature? dealer)
    {
        if (targets == null) return;
        var mapped = new List<Creature>();
        foreach (var target in targets)
        {
            // Explicit members already have a damage owner (notably native
            // Thorns' dealer target). Only resolve the hidden player gateway here.
            var squad = SquadCombatState.TryGet(target.Player);
            var result = squad != null && squad.Living.Any() ? squad.Front : target;
            if (!mapped.Contains(result)) mapped.Add(result);
        }
        targets = mapped;
        var attackerSquad = SquadCombatState.TryGet(dealer?.Player);
        if (attackerSquad?.Living.Any() == true) dealer = attackerSquad.Front;
    }
}

public sealed class SquadEnemyAttackTargetsPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_enemy_hit_targets";
    public static ModPatchTarget[] GetTargets() => [new(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand),
        "GetPossibleTargets", Type.EmptyTypes)];
    public static void Postfix(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand __instance,
        ref IReadOnlyList<Creature> __result)
    {
        if (__instance.Attacker?.Side != CombatSide.Enemy) return;
        // Native Execute obtains this list anew for EACH hit, before its VFX,
        // SFX and damage. Resolving only inside Damage was too late for hit art.
        __result = __result.Select(target =>
        {
            var owner = target.Player ?? (SquadCombatState.IsMember(target) ? target.PetOwner : null);
            var squad = SquadCombatState.TryGet(owner);
            return squad?.Living.Any() == true ? squad.Front : target;
        }).Distinct().ToArray();
    }
}

public sealed class SquadCardAttackerPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_card_attacker";
    public static ModPatchTarget[] GetTargets() => [new(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand),
        nameof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand.FromCard),
#if DOHNADOHNA_STABLE
        [typeof(CardModel)])];
#else
        [typeof(CardModel), typeof(CardPlay)])];
#endif
    public static void Postfix(CardModel card, MegaCrit.Sts2.Core.Commands.Builders.AttackCommand __instance)
    {
        if (SquadCombatState.TryGet(card.Owner) is not { } squad) return;
        var actor = SquadPlaySelection.CurrentFor(card)?.Actor ?? squad.Actions.ActorFor(card);
        AccessTools.PropertySetter(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand),
            nameof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand.Attacker)).Invoke(__instance, [actor]);
    }
}

public sealed class SquadBlockReceiverPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_independent_block";
    public static ModPatchTarget[] GetTargets()
    {
        var method = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
            [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)
#if !DOHNADOHNA_STABLE
            , typeof(CardPlay)
#endif
            ]);
        var stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException("CreatureCmd.Damage async state machine");
        return [new(stateMachine, "MoveNext", Type.EmptyTypes)];
    }
    public static Player? BlockOwner(Creature creature) => SquadCombatState.IsMember(creature) ? null : creature.PetOwner;
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.PetOwner));
        var replacement = AccessTools.Method(typeof(SquadBlockReceiverPatch), nameof(BlockOwner));
        var result = instructions.ToList();
        int matches = 0;
        foreach (var instruction in result)
            if (Equals(instruction.operand, getter)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; matches++; }
        if (matches != 1) throw new InvalidOperationException($"Expected one pet block-owner lookup, found {matches}.");
        return result;
    }
}

public sealed class SquadTurnParticipantsPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_turn_participants";
    public static ModPatchTarget[] GetTargets() =>
#if DOHNADOHNA_STABLE
    [new(typeof(Hook), nameof(Hook.BeforeTurnEnd), [typeof(ICombatState), typeof(CombatSide), typeof(IEnumerable<Creature>)]),
     new(typeof(Hook), nameof(Hook.AfterTurnEnd), [typeof(ICombatState), typeof(CombatSide), typeof(IEnumerable<Creature>)])];
#else
    [new(typeof(Hook), nameof(Hook.BeforeSideTurnEnd), [typeof(ICombatState), typeof(CombatSide), typeof(IEnumerable<Creature>)]),
     new(typeof(Hook), nameof(Hook.AfterSideTurnEnd), [typeof(ICombatState), typeof(CombatSide), typeof(IEnumerable<Creature>)])];
#endif
    public static void Prefix(CombatSide side, ref IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        var list = participants.ToList();
        foreach (var owner in list.Where(c => c.Player != null).Select(c => c.Player!).ToArray())
            if (SquadCombatState.TryGet(owner) is { } squad) list.AddRange(squad.Living);
        participants = list.Distinct().ToArray();
    }
}

public sealed class SquadGainBlockPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_block_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool)])];
    public static void Prefix(ref Creature creature)
    {
        if (SquadCombatState.TryGet(creature.Player) is { } squad) creature = squad.Front;
    }
}

public sealed class SquadDexterityPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_dexterity_source";
    public static ModPatchTarget[] GetTargets() => [new(typeof(MegaCrit.Sts2.Core.Models.Powers.DexterityPower), "ModifyBlockAdditive",
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)])];
    public static void Postfix(MegaCrit.Sts2.Core.Models.Powers.DexterityPower __instance, Creature target,
        ValueProp props, CardModel? cardSource, ref decimal __result)
    {
        if (cardSource != null && cardSource.Owner.Character is DohnaSquad && __instance.Owner == target
            && props.IsPoweredCardOrMonsterMoveBlock()) __result = __instance.Amount;
    }
}
