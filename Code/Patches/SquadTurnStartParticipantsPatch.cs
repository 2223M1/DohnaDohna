using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadTurnStartParticipantsPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_extra_turn_members";
    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(Hook), nameof(Hook.BeforeSideTurnStart), [typeof(ICombatState), typeof(CombatSide), typeof(IReadOnlyList<Creature>)])];
    public static void Prefix(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        var missing = participants.Select(c => SquadCombatState.TryGet(c.Player)).OfType<SquadCombatState>()
            .SelectMany(s => s.Living).Except(participants).ToArray();
        if (missing.Length == 0) return;
        // Both supported StartTurn implementations retain this same list for block clearing
        // and AfterTurnStart. Do not replace the enumeration only at the hook boundary.
        if (participants is not List<Creature> nativeTurnList)
            throw new InvalidOperationException("Squad extra turn requires the native mutable participant list.");
        foreach (var actor in missing) { actor.BeforeTurnStart(side); nativeTurnList.Add(actor); }
    }
}
