using DohnaDohna.Content;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2RitsuLib.Patching.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using Godot;
using System.Reflection.Emit;
using HarmonyLib;

namespace DohnaDohna.Code.Patches;

public sealed class SquadSelectPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_secondary_select";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter),
        [typeof(NCharacterSelectButton), typeof(CharacterModel)])];
    public static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel,
        Control ____relicTitle, Control ____relicDescription, Control ____relicIcon, Control ____relicIconOutline)
    {
        // Primary selection remains primary; only native confirm opens the roster.
        if (SquadSelectPanel.Get(__instance) is { IsSecondary: true } panel) panel.ReturnToPrimary(false);
        foreach (var control in new[] { ____relicTitle, ____relicDescription, ____relicIcon, ____relicIconOutline })
            control.Visible = characterModel.StartingRelics.Count > 0;
    }

    // The host assumes every character has a starting relic. Skip only the two
    // relic-preview blocks when the real list is empty; never grant a dummy relic.
    public static bool HasStartingRelic(CharacterModel character) => character.StartingRelics.Count > 0;
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var code = instructions.ToList();
        var getter = AccessTools.PropertyGetter(typeof(CharacterModel), nameof(CharacterModel.StartingRelics));
        var tint = AccessTools.PropertySetter(typeof(CanvasItem), nameof(CanvasItem.SelfModulate));
        int count = 0;
        for (int i = code.Count - 1; i >= 0; i--)
        {
            if (!code[i].Calls(getter)) continue;
            int end = i, seen = 0;
            while (++end < code.Count && seen < 2) if (code[end].Calls(tint)) seen++;
            if (seen != 2 || end >= code.Count || code[i - 1].opcode != OpCodes.Ldarg_2)
                throw new InvalidOperationException("Character relic preview boundary changed.");
            var label = generator.DefineLabel();
            code[end].labels.Add(label);
            var first = new CodeInstruction(OpCodes.Ldarg_2);
            first.labels.AddRange(code[i - 1].labels);
            code[i - 1].labels.Clear();
            code.InsertRange(i - 1, [first,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SquadSelectPatch), nameof(HasStartingRelic))),
                new CodeInstruction(OpCodes.Brfalse, label)]);
            count++;
        }
        if (count != 2) throw new InvalidOperationException($"Expected two native relic preview blocks, found {count}.");
        return code;
    }
}

public sealed class SquadEmbarkPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_validate_embark";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCharacterSelectScreen), "OnEmbarkPressed", [typeof(NButton)])];
    public static bool Prefix(NCharacterSelectScreen __instance)
    {
        if (__instance.Lobby.LocalPlayer.character is not DohnaSquad) return true;
        if (SquadSelectPanel.Get(__instance) is { IsSecondary: true } selector)
        {
            if (!selector.Commit()) return false;
            // Let the host tutorial prompt complete first. On its callback the
            // original embark handler locks native buttons; lock our row too.
            if (SaveManager.Instance.SeenFtue("accept_tutorials_ftue")) selector.LockSelection();
            return true;
        }
        SquadSelectPanel.Open(__instance);
        return false;
    }
}

public sealed class SquadSelectBackPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_secondary_select_back";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NSubmenuStack), nameof(NSubmenuStack.Pop), Type.EmptyTypes)];
    public static bool Prefix(NSubmenuStack __instance)
    {
        if (__instance.Peek() is not NCharacterSelectScreen screen || SquadSelectPanel.Get(screen) is not { IsSecondary: true } selector) return true;
        selector.ReturnToPrimary();
        return false;
    }
}

public sealed class SquadSelectClosePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_select_close";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuClosed), Type.EmptyTypes)];
    public static void Prefix(NCharacterSelectScreen __instance) => SquadSelectPanel.Get(__instance)?.ClearDraft();
}

// Switching to keyboard/controller navigation and closing a native modal both
// ask the submenu for focus again. Its primary character bar is hidden here.
public sealed class SquadSelectDefaultFocusPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_secondary_select_focus";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NSubmenu), "get_DefaultFocusedControl", Type.EmptyTypes)];
    public static void Postfix(NSubmenu __instance, ref Control? __result)
    {
        if (__instance is NCharacterSelectScreen screen && SquadSelectPanel.Get(screen) is { IsSecondary: true } panel)
            __result = panel.DefaultFocusedControl;
    }
}
