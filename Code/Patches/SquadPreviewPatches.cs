using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadDamagePreviewPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_damage_preview_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.ModifyDamage),
        [typeof(IRunState), typeof(ICombatState), typeof(Creature), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel),
#if !DOHNADOHNA_STABLE
         typeof(CardPlay),
#endif
         typeof(ModifyDamageHookType), typeof(CardPreviewMode), typeof(IEnumerable<AbstractModel>).MakeByRefType()])];
    public static void Prefix(ref Creature? dealer, ref Creature? target, CardModel? cardSource)
    {
        if (cardSource != null && SquadCombatState.TryGet(dealer?.Player) is { } attackingSquad && attackingSquad.Living.Any())
            dealer = SquadPlaySelection.CurrentFor(cardSource)?.Actor ?? attackingSquad.Actions.ActorFor(cardSource);
        if (SquadCombatState.TryGet(target?.Player) is { } defendingSquad && defendingSquad.Living.Any())
            target = defendingSquad.Front;
    }
}

public sealed class SquadBlockPreviewPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_block_preview_actor";
    public static ModPatchTarget[] GetTargets() => [new(typeof(Hook), nameof(Hook.ModifyBlock),
        [typeof(ICombatState), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay), typeof(IEnumerable<AbstractModel>).MakeByRefType()])];
    public static void Prefix(ref Creature target, CardModel? cardSource)
    {
        if (SquadCombatState.TryGet(target.Player) is { } squad && squad.Living.Any())
            target = cardSource is SquadCardModel ? SquadCardModel.ResolveActor(cardSource) : squad.Front;
    }
}
