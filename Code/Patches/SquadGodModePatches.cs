using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// The console owns its toggle and CombatSetUp subscription. Only expand its
// explicit recipients; no second godmode flag, save field or damage bypass.
public sealed class SquadGodModeEnablePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_godmode_enable";
    public static ModPatchTarget[] GetTargets() => [new(typeof(GodModeConsoleCmd), "EnableGodMode", [typeof(Player)])];
    public static bool Prefix(Player player, ref Task __result)
    {
        if (SquadCombatState.TryGet(player) is not { } squad) return true;
        __result = Enable(squad);
        return false;
    }
    private static async Task Enable(SquadCombatState squad)
    {
        var context = new ThrowingPlayerChoiceContext();
        foreach (var actor in squad.Living.ToArray())
        {
            await PowerCmd.Apply<StrengthPower>(context, actor, 999999999m, actor, null);
            await PowerCmd.Apply<BufferPower>(context, actor, 999999999m, actor, null);
            await PowerCmd.Apply<RegenPower>(context, actor, 999999999m, actor, null);
        }
    }
}

public sealed class SquadGodModeDisablePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_godmode_disable";
    public static ModPatchTarget[] GetTargets() => [new(typeof(GodModeConsoleCmd), "DisableGodMode", [typeof(Player)])];
    public static bool Prefix(Player player, ref Task __result)
    {
        if (SquadCombatState.TryGet(player) is not { } squad) return true;
        __result = Disable(squad);
        return false;
    }
    private static async Task Disable(SquadCombatState squad)
    {
        // Native godmode removes these powers entirely, including ordinary
        // stacks. Do the same on all members, even after a front change.
        foreach (var actor in squad.Actors.Prepend(squad.Owner.Creature))
        {
            await PowerCmd.Remove<StrengthPower>(actor);
            await PowerCmd.Remove<BufferPower>(actor);
            await PowerCmd.Remove<RegenPower>(actor);
        }
    }
}
