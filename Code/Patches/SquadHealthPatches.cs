using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Patching.Models;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace DohnaDohna.Code.Patches;

public sealed class SquadHealTargetPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_heal_target";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.Heal), [typeof(Creature), typeof(decimal), typeof(bool)])];
    public static void Prefix(ref Creature creature)
    {
        if (SquadCombatState.TryGet(creature.Player) is { } squad && squad.Living.Any()) creature = squad.Front;
    }
    public static void Postfix(Creature creature, ref Task __result) => __result = Sync(__result, creature);
    private static async Task Sync(Task original, Creature creature)
    {
        await original;
        SquadCombatState.TryGet(creature.PetOwner)?.SyncController();
        if (creature.Player is { Character: DohnaSquad } player && creature == player.Creature && SquadCombatState.TryGet(player) == null)
        {
            var state = SquadStore.Get(player);
            if (state.Front is { } front) { front.Hp = creature.CurrentHp; front.MaxHp = creature.MaxHp; }
            SquadStore.State.Set((MegaCrit.Sts2.Core.Runs.RunState)player.RunState, player.NetId, state);
        }
    }
}

public sealed class SquadRestPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_rest_and_revive";
    public static ModPatchTarget[] GetTargets()
    {
        var method = AccessTools.Method(typeof(HealRestSiteOption), nameof(HealRestSiteOption.ExecuteRestSiteHeal), [typeof(Player), typeof(bool)]);
        var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException("Rest heal state machine");
        return [new(machine, "MoveNext", Type.EmptyTypes)];
    }
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var method = AccessTools.Method(typeof(HealRestSiteOption), nameof(HealRestSiteOption.ExecuteRestSiteHeal), [typeof(Player), typeof(bool)]);
        var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var heal = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal), [typeof(Creature), typeof(decimal), typeof(bool)]);
        var mimicked = AccessTools.Field(machine, "isMimicked") ?? throw new MissingFieldException("Rest isMimicked");
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(heal))
            {
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                yield return load;
                yield return new CodeInstruction(OpCodes.Ldfld, mimicked);
                instruction.operand = AccessTools.Method(typeof(SquadRestPatch), nameof(HealSquad));
                count++;
            }
            yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException($"Expected one rest heal call, found {count}.");
    }
    public static async Task HealSquad(Creature creature, decimal amount, bool playAnim, bool mimicked)
    {
        if (creature.Player is not { Character: DohnaSquad } player)
        { await CreatureCmd.Heal(creature, amount, playAnim); return; }
        var state = SquadStore.Get(player).Copy();
        var front = state.Front ?? throw new InvalidOperationException("Cannot rest a defeated squad.");
        string? revived = null;
        var dead = state.Members.Where(m => m.Hp == 0).ToArray();
        if (!mimicked && dead.Length > 0)
        {
            // KnowledgeDemon uses this exact native command/screen. A living
            // four-member squad has at most three dead members, its supported limit.
            var options = dead.Select(member =>
            {
                var option = ((MegaCrit.Sts2.Core.Runs.RunState)player.RunState).CreateCard<DohnaDohna.Cards.SquadReviveChoice>(player);
                option.SetMember(member);
                return (CardModel)option;
            }).ToArray();
            var selected = await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), options, player, canSkip: false);
            if (selected is not DohnaDohna.Cards.SquadReviveChoice { RoleId: { } role })
                throw new InvalidOperationException("Required native revival choice returned no member.");
            revived = role;
            state.ReviveAtRear(role);
        }
        foreach (var member in state.Members.Where(m => m.Hp > 0 || m.RoleId == revived))
        {
            var subject = member == front ? creature : new Creature(player, member.Hp, member.MaxHp);
            decimal healAmount = member == front ? amount : MegaCrit.Sts2.Core.Hooks.Hook.ModifyRestSiteHealAmount(
                player.RunState, subject, HealRestSiteOption.GetBaseHealAmount(subject));
            await CreatureCmd.Heal(subject, healAmount, playAnim && member == front);
            member.Hp = subject.CurrentHp;
            member.MaxHp = subject.MaxHp;
        }
        state.Validate();
        SquadStore.State.Set((MegaCrit.Sts2.Core.Runs.RunState)player.RunState, player.NetId, state);
        // Native AfterRestSiteHeal and rewards run once, after the complete squad heal.
    }
}
