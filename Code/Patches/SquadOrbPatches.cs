using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using DohnaDohna.Code.Squad;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// The real player's native queue, model, focus hook and orb commands remain
// authoritative. Only these selected native endpoints need a squad actor.
public sealed class SquadOrbFocusPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_orb_focus_owner";
    public static ModPatchTarget[] GetTargets() => [new(typeof(FocusPower), nameof(FocusPower.ModifyOrbValue), [typeof(OrbModel), typeof(decimal)])];
    public static bool Prefix(FocusPower __instance, OrbModel orb, decimal value, ref decimal __result)
    {
        if (SquadCombatState.TryGet(orb.Owner) is not { } squad) return true;
        var focusOwner = squad.IsAlive("antena") ? squad.GetActor("antena") : orb.Owner.Creature;
        __result = __instance.Owner == focusOwner ? Math.Max(value + __instance.Amount, 0) : value;
        return false;
    }
}

public sealed class SquadOrbDamageOwnerPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_orb_damage_owner";
    public static ModPatchTarget[] GetTargets() => new (Type type, string method)[]
    {
        (typeof(LightningOrb), "ApplyLightningDamage"), (typeof(DarkOrb), nameof(DarkOrb.Evoke))
    }.Select(t => new ModPatchTarget(AccessTools.DeclaredMethod(t.type, t.method)
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext", Type.EmptyTypes)).ToArray();
    public static Creature Dealer(Player player) => SquadCombatState.TryGet(player) is { } squad && squad.IsAlive("antena")
        ? squad.GetActor("antena") : player.Creature;
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int count = 0;
        var getter = AccessTools.PropertyGetter(typeof(Player), nameof(Player.Creature));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getter))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(SquadOrbDamageOwnerPatch), nameof(Dealer)); count++; }
            yield return instruction;
        }
        if (count == 0) throw new InvalidOperationException("Native orb damage no longer has a player creature endpoint.");
    }
}

public sealed class SquadOrbLayoutPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_orb_visual_layout";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NOrbManager), "TweenLayout", Type.EmptyTypes)];
    public static bool Prefix(NOrbManager __instance, NCreature ____creatureNode, List<NOrb> ____orbs, ref Tween? ____curTween)
    {
        if (SquadCombatState.TryGet(____creatureNode.Entity.Player) == null) return true;
        ____curTween?.Kill();
        ____curTween = __instance.CreateTween().SetParallel();
        // Queue order runs left to right over a shallow ellipse. Keep native
        // NOrb tooltips, activation animations and controller navigation.
        for (int i = 0; i < ____orbs.Count; i++)
        {
            float angle = ____orbs.Count == 1 ? -Mathf.Pi / 2
                : Mathf.Lerp(Mathf.DegToRad(-160), Mathf.DegToRad(-20), (float)i / (____orbs.Count - 1));
            float extra = Math.Max(0, ____orbs.Count - 3);
            var position = new Vector2(Mathf.Cos(angle) * (98 + 17 * extra), Mathf.Sin(angle) * (78 + 6 * extra));
            ____curTween.TweenProperty(____orbs[i], "position", position, .25).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        return false;
    }
}
