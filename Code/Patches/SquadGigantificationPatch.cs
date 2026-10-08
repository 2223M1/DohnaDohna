using System.Reflection.Emit;
using HarmonyLib;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>Retain the native power's per-command consumption; change only card actor attribution.</summary>
public sealed class SquadGigantificationPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_gigantification_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(GigantificationPower), nameof(GigantificationPower.BeforeAttack)),
        new(typeof(GigantificationPower), nameof(GigantificationPower.ModifyDamageMultiplicative))];
    public static Creature Actor(CardModel card) => SquadCombatState.TryGet(card.Owner) is { } squad && squad.Living.Any()
        ? SquadPlaySelection.CurrentFor(card)?.Actor ?? squad.Actions.ActorFor(card) : card.Owner.Creature;
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = instructions.ToList();
        var owner = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Owner));
        var creature = AccessTools.PropertyGetter(typeof(Player), nameof(Player.Creature));
        int matches = 0;
        for (int i = 0; i < result.Count - 1; i++)
            if (result[i].Calls(owner) && result[i + 1].Calls(creature))
            {
                result[i].opcode = OpCodes.Call;
                result[i].operand = AccessTools.Method(typeof(SquadGigantificationPatch), nameof(Actor));
                result[i + 1].opcode = OpCodes.Nop;
                result[i + 1].operand = null;
                matches++;
            }
        if (matches != 1) throw new InvalidOperationException($"Expected one native card owner comparison, found {matches}.");
        return result;
    }
}
