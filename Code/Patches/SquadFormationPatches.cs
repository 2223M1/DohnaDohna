using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadFormationPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_formation";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets),
        [typeof(List<NCreature>), typeof(float), typeof(bool)])];

    public static void Prefix(ref List<NCreature> creatureNodes, out List<NCreature> __state)
    {
        // Let native layout position real players and ordinary pets only.
        __state = creatureNodes;
        creatureNodes = creatureNodes.Where(n => !SquadCombatState.IsMember(n.Entity)).ToList();
    }

    public static void Postfix(List<NCreature> __state, float scaling, bool fullyCenterPlayers)
    {
        if (NCombatRoom.Instance is not { } room) return;
        foreach (var owner in __state.Select(n => n.Entity.Player).OfType<MegaCrit.Sts2.Core.Entities.Players.Player>())
            if (SquadCombatState.TryGet(owner) is { } squad)
                SquadFormation.Ensure(room).Reflow(squad, scaling, fullyCenterPlayers);
    }
}

public sealed class SquadFormationChangedPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_formation_creatures_changed";
    public static ModPatchTarget[] GetTargets() => [new(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature)),
        new(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))];
    public static void Postfix(NCombatRoom __instance)
    {
        foreach (var player in __instance.CreatureNodes.Select(n => n.Entity.Player).OfType<MegaCrit.Sts2.Core.Entities.Players.Player>())
            SquadCombatState.TryGet(player)?.Layout();
    }
}
