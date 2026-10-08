using System.Reflection.Emit;
using DohnaDohna.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

/// <summary>Substitution changes the input to native modifiers, not their algorithm or stored state.</summary>
public sealed class SquadSubstitutionCostPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_substitution_cost";
    public static ModPatchTarget[] GetTargets() =>
    [new(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers), [typeof(CostModifiers)]),
     new(typeof(CardEnergyCost), nameof(CardEnergyCost.GetAmountToSpend), Type.EmptyTypes),
     new(typeof(CardEnergyCost), nameof(CardEnergyCost.GetResolved), Type.EmptyTypes)];
    private static readonly AccessTools.FieldRef<CardEnergyCost, CardModel> Card = AccessTools.FieldRefAccess<CardEnergyCost, CardModel>("_card");
    private static readonly AccessTools.FieldRef<CardEnergyCost, int> Base = AccessTools.FieldRefAccess<CardEnergyCost, int>("_base");
    public static int EffectiveBase(CardEnergyCost cost) => Card(cost) is SquadCardModel { IsFallback: true } ? 1 : Base(cost);
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, System.Reflection.MethodBase __originalMethod)
    {
        var field = AccessTools.Field(typeof(CardEnergyCost), "_base");
        int count = 0, xReads = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(field))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(SquadSubstitutionCostPatch), nameof(EffectiveBase)); count++; }
            // The host's tiny getter can be inlined before its separate display
            // patch is installed. Resolve X here too, before native modifiers.
            if (instruction.Calls(AccessTools.PropertyGetter(typeof(CardEnergyCost), nameof(CardEnergyCost.CostsX))))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(SquadSubstitutionXPatch), nameof(SquadSubstitutionXPatch.EffectiveX)); xReads++; }
            yield return instruction;
        }
        int expectedBase = __originalMethod.Name == nameof(CardEnergyCost.GetWithModifiers) ? 2 : 0;
        if (count != expectedBase || xReads != 1) throw new InvalidOperationException($"Unexpected native cost inputs in {__originalMethod.Name}: base={count}, X={xReads}.");
    }
}

public sealed class SquadSubstitutionXPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_substitution_x";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardEnergyCost), "get_CostsX", Type.EmptyTypes)];
    private static readonly AccessTools.FieldRef<CardEnergyCost, CardModel> Card = AccessTools.FieldRefAccess<CardEnergyCost, CardModel>("_card");
    public static bool EffectiveX(CardEnergyCost cost) => Card(cost) is not SquadCardModel { IsFallback: true }
        && SquadSubstitutionClonePatch.CanonicalX(cost);
    public static void Postfix(CardModel ____card, ref bool __result)
    { if (____card is SquadCardModel { IsFallback: true }) __result = false; }
}

public sealed class SquadSubstitutionClonePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_substitution_clone";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardEnergyCost), nameof(CardEnergyCost.Clone), [typeof(CardModel)])];
    private static readonly AccessTools.FieldRef<CardEnergyCost, bool> OriginalX = AccessTools.FieldRefAccess<CardEnergyCost, bool>("<CostsX>k__BackingField");
    public static bool CanonicalX(CardEnergyCost cost) => OriginalX(cost);
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(AccessTools.PropertyGetter(typeof(CardEnergyCost), nameof(CardEnergyCost.CostsX))))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(SquadSubstitutionClonePatch), nameof(CanonicalX)); count++; }
            yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException($"Expected one cloned X-cost read, found {count}.");
    }
}

public sealed class SquadSubstitutionDescriptionPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_substitution_description";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardModel), "get_Description", Type.EmptyTypes)];
    public static void Postfix(CardModel __instance, ref LocString __result)
    {
        if (__instance is SquadCardModel { IsFallback: true })
            __result = new LocString("cards", "DOHNA_SQUAD.fallback_" + __instance.Type.ToString().ToLowerInvariant());
    }
}

public sealed class SquadSubstitutionStarPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_substitution_stars";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardModel), "get_CurrentStarCost", Type.EmptyTypes)];
    public static void Postfix(CardModel __instance, ref int __result)
    { if (__instance is SquadCardModel { IsFallback: true }) __result = 0; }
}
