using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using HarmonyLib;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>Replace only the ancient's healing call; native event guards and save timing remain authoritative.</summary>
public sealed class SquadAncientRecoveryPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_ancient_squad_recovery";
    public static ModPatchTarget[] GetTargets()
    {
        var method = AccessTools.Method(typeof(AncientEventModel), "BeforeEventStarted", [typeof(bool)]);
        var machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException("AncientEventModel.BeforeEventStarted state machine");
        return [new(machine, "MoveNext", Type.EmptyTypes)];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var heal = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal), [typeof(Creature), typeof(decimal), typeof(bool)]);
        var replacement = AccessTools.Method(typeof(SquadAncientRecoveryPatch), nameof(HealSquad));
        var stateMachine = AccessTools.Method(typeof(AncientEventModel), "BeforeEventStarted", [typeof(bool)])
            .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var eventField = AccessTools.Field(stateMachine, "<>4__this")
            ?? throw new MissingFieldException("Ancient event state machine owner");
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(heal))
            {
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                yield return load;
                yield return new CodeInstruction(OpCodes.Ldfld, eventField);
                instruction.operand = replacement;
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException($"Expected one ancient healing call, found {matches}.");
    }

    public static async Task HealSquad(Creature creature, decimal amount, bool playAnim, AncientEventModel ancient)
    {
        if (creature.Player is not { Character: DohnaSquad } player)
        { await CreatureCmd.Heal(creature, amount, playAnim); return; }

        var state = SquadStore.Get(player).Copy();
        bool initial = ancient is Neow;
        var front = state.Front ?? throw new InvalidOperationException("A defeated squad cannot enter another act.");
        var revived = state.Members.Where(m => m.Hp == 0).ToArray();
        decimal ratio = RunManager.Instance.HasAscension(MegaCrit.Sts2.Core.Entities.Ascension.AscensionLevel.WearyTraveler) ? .8m : 1m;
        foreach (var member in state.Members)
        {
            // Detached native health subject: never inserted as an extra player or combat entity.
            // Native Heal accepts 0 HP and raises its own revive event when HP becomes positive.
            var subject = member == front ? creature : new Creature(player, initial ? 0 : member.Hp, member.MaxHp);
            await CreatureCmd.Heal(subject, (subject.MaxHp - subject.CurrentHp) * ratio, playAnim: false);
            member.Hp = subject.CurrentHp;
            member.MaxHp = subject.MaxHp;
        }
        if (!initial)
        {
            state.Members.RemoveAll(m => revived.Contains(m));
            state.Members.InsertRange(0, revived);
        }
        state.Validate();
        SquadStore.State.Set((RunState)player.RunState, player.NetId, state);
        creature.SetMaxHpInternal(state.Front!.MaxHp);
        creature.SetCurrentHpInternal(state.Front.Hp);
    }
}
