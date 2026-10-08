using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>These five selected personal powers require a resource owner. Their logic and state remain native.</summary>
public sealed class SquadPersonalPowerPlayerPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_personal_power_player";
    public static ModPatchTarget[] GetTargets() => new (Type type, string method)[]
    {
        (typeof(JuggernautPower), nameof(JuggernautPower.AfterBlockGained)),
        (typeof(ViciousPower), nameof(ViciousPower.AfterPowerAmountChanged)),
        (typeof(LoopPower), nameof(LoopPower.AfterPlayerTurnStart)),
        (typeof(HailstormPower), nameof(HailstormPower.BeforeSideTurnEnd)),
        (typeof(PlatingPower), nameof(PlatingPower.AfterSideTurnStart))
    }.Select(target =>
    {
        var method = AccessTools.DeclaredMethod(target.type, target.method);
        var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException($"{target.type.Name}.{target.method} async state machine");
        return new ModPatchTarget(machine, "MoveNext", Type.EmptyTypes);
    }).ToArray();
    public static Player? ResourceOwner(Creature creature) => creature.Player
        ?? (SquadCombatState.IsMember(creature) ? creature.PetOwner : null);
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.Player));
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getter))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(SquadPersonalPowerPlayerPatch), nameof(ResourceOwner)); count++; }
            yield return instruction;
        }
        if (count == 0) throw new InvalidOperationException("Selected personal power lost its native Player lookup.");
    }
}

public sealed class SquadPowerApplierNamePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_power_applier_name";
    public static ModPatchTarget[] GetTargets() => new[] { typeof(FlankingPower), typeof(KnockdownPower), typeof(GuardedPower) }
        .Select(t => new ModPatchTarget(t, nameof(PowerModel.AfterApplied), [typeof(Creature), typeof(CardModel)])).ToArray();
    public static bool Prefix(PowerModel __instance, ref Task __result)
    {
        if (__instance.Applier is not { } source || !SquadCombatState.IsMember(source)) return true;
        var role = RoleDefinition.Get(SquadCombatState.Get(source.PetOwner!).RoleOf(source));
        ((StringVar)__instance.DynamicVars["Applier"]).StringValue = role.GetDisplayName(LocManager.Instance.Language);
        __result = Task.CompletedTask;
        return false;
    }
}

public sealed class SquadTagTeamActorPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_tag_team_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(TagTeamPower), nameof(TagTeamPower.ModifyCardPlayCount), [typeof(CardModel), typeof(Creature), typeof(int)])];
    public static bool Prefix(TagTeamPower __instance, CardModel card, int playCount, ref int __result)
    {
        if (__instance.Applier is not { } source || !SquadCombatState.IsMember(source) || SquadCardModel.ResolveActor(card) != source) return true;
        __result = playCount;
        return false;
    }
}

public sealed class SquadLethalityActorPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_lethality_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(LethalityPower), nameof(LethalityPower.ModifyDamageMultiplicative),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)
#if !DOHNADOHNA_STABLE
        , typeof(CardPlay)
#endif
        ])];
    public static bool Prefix(LethalityPower __instance, Creature? dealer, ValueProp props, CardModel? cardSource, ref decimal __result)
    {
        if (!SquadCombatState.IsMember(__instance.Owner)) return true;
        __result = 1;
        if (!props.IsPoweredAttack() || cardSource == null || dealer != __instance.Owner) return false;
        bool playing = cardSource.Pile?.Type == PileType.Play;
        if (playing && cardSource.CurrentPlayIndex > 0) return false;
        var squad = SquadCombatState.Get(__instance.Owner.PetOwner!);
        int count = CombatManager.Instance.History.CardPlaysStarted.Count(e => e.HappenedThisTurn(__instance.CombatState)
            && e.CardPlay.Card.Type == CardType.Attack && squad.ActorOf(e.CardPlay) == __instance.Owner);
        if (count <= (playing ? 1 : 0)) __result = 1m + __instance.Amount / 100m;
        return false;
    }
}

public sealed class SquadUnmovableActorPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_unmovable_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(UnmovablePower), nameof(UnmovablePower.ModifyBlockMultiplicative),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)])];
    public static bool Prefix(UnmovablePower __instance, Creature target, ValueProp props, CardModel? cardSource, CardPlay? cardPlay, ref decimal __result)
    {
        if (!SquadCombatState.IsMember(__instance.Owner)) return true;
        __result = 1;
        if (target != __instance.Owner || !props.IsCardOrMonsterMove()
            || cardSource != null && SquadCardModel.ResolveActor(cardSource) != __instance.Owner) return false;
        int count = CombatManager.Instance.History.Entries.OfType<BlockGainedEntry>().Count(e => e.HappenedThisTurn(__instance.CombatState)
            && e.Receiver == __instance.Owner && e.CardPlay != null && e.CardPlay != cardPlay && e.Props.IsCardOrMonsterMove());
        if (count < __instance.Amount) __result = 2;
        return false;
    }
}

public sealed class SquadBeaconTargetsPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_beacon_targets";
    public static ModPatchTarget[] GetTargets() => [new(typeof(BeaconOfHopePower), nameof(BeaconOfHopePower.AfterBlockGained),
        [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel)])];
    private static readonly AccessTools.FieldRef<BeaconOfHopePower, bool> Processing =
        AccessTools.FieldRefAccess<BeaconOfHopePower, bool>("_hasAlreadyBeenGivenBlock");
    public static bool Prefix(BeaconOfHopePower __instance, Creature creature, decimal amount, ref Task __result)
    {
        if (!SquadCombatState.IsMember(__instance.Owner)) return true;
        __result = Share(__instance, creature, amount);
        return false;
    }
    private static async Task Share(BeaconOfHopePower power, Creature creature, decimal amount)
    {
        if (creature != power.Owner || amount < 2 || power.CombatState!.CurrentSide != power.Owner.Side || Processing(power)) return;
        Processing(power) = true;
        try
        {
            foreach (var ally in SquadCombatState.Get(power.Owner.PetOwner!).Living.Where(c => c != power.Owner).ToArray())
                await CreatureCmd.GainBlock(ally, amount * .5m, ValueProp.Unpowered, null);
        }
        finally { Processing(power) = false; }
    }
}

public sealed class SquadTankTargetsPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_tank_targets";
    public static ModPatchTarget[] GetTargets() => [new(typeof(TankPower), nameof(TankPower.AfterApplied), [typeof(Creature), typeof(CardModel)])];
    public static bool Prefix(TankPower __instance, ref Task __result)
    {
        if (!SquadCombatState.IsMember(__instance.Owner)) return true;
        __result = Protect(__instance);
        return false;
    }
    private static async Task Protect(TankPower power)
    {
        foreach (var ally in SquadCombatState.Get(power.Owner.PetOwner!).Living.Where(c => c != power.Owner).ToArray())
            await PowerCmd.Apply<GuardedPower>(new ThrowingPlayerChoiceContext(), ally, power.Amount, power.Owner, null);
    }
}

public sealed class SquadGuardedLifetimePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_guarded_lifetime";
    public static ModPatchTarget[] GetTargets() => [new(typeof(GuardedPower), nameof(GuardedPower.AfterDeath),
        [typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(float)])];
    public static bool Prefix(GuardedPower __instance, ref Task __result)
    {
        if (__instance.Applier is not { } source || !SquadCombatState.IsMember(source)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

public sealed class SquadPlatingOpeningPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_plating_opening";
    public static ModPatchTarget[] GetTargets() => [new(typeof(PlatingPower), nameof(PlatingPower.BeforeSideTurnStart),
        [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState)])];
    public static bool Prefix(PlatingPower __instance, ref Task __result)
    {
        if (!SquadCombatState.IsMember(__instance.Owner)) return true;
        __result = Task.CompletedTask; // Native opening grant belongs only to monsters, not owned squad members.
        return false;
    }
}
