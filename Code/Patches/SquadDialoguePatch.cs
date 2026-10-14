using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// Native commands AND pile/card UI call these factories. Change only a hidden
// squad controller's speaker; explicit members, enemies and ordinary players
// retain their native anchors, bubble appearance and lifetime. Both factories
// snapshot TalkPosition into the same room VFX container, not the hidden body.
public sealed class SquadDialoguePatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_squad_dialogue";
    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(NThoughtBubbleVfx), nameof(NThoughtBubbleVfx.Create), [typeof(string), typeof(Creature), typeof(double?)]),
        new(typeof(NSpeechBubbleVfx), nameof(NSpeechBubbleVfx.Create), [typeof(string), typeof(Creature), typeof(double), typeof(VfxColor)])
    ];
    public static void Prefix(ref Creature speaker)
    {
        if (SquadCombatState.TryGet(speaker.Player) is { } squad && speaker == squad.Owner.Creature
            && squad.Living.LastOrDefault() is { } front)
            speaker = front;
    }
}
