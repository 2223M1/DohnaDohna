using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Combat.CardTargeting;

namespace DohnaDohna.Code.Squad;

/// <summary>Predicates only. RitsuLib owns mouse/controller arrows and native target validation.</summary>
public static class SquadTargeting
{
    public static TargetType Member { get; } = CustomTargetType.RegisterSingleTargetTypeWithContext(
        "DohnaDohna", "member", c => IsLivingMember(c.TargetCreature, c.Player));

    public static TargetType Swap { get; } = CustomTargetType.RegisterSingleTargetTypeWithContext(
        "DohnaDohna", "swap", c => IsLivingMember(c.TargetCreature, c.Player)
            && c.TargetCreature != SquadCombatState.Get(c.Player).Front);

    public static TargetType OtherMember { get; } = CustomTargetType.RegisterSingleTargetTypeWithContext(
        "DohnaDohna", "other_member", c => IsLivingMember(c.TargetCreature, c.Player)
            && c.TargetCreature != (c.Card != null ? DohnaDohna.Cards.SquadCardModel.ResolveActor(c.Card) : SquadCombatState.Get(c.Player).Front));

    public static bool IsLivingMember(Creature candidate, Player player) =>
        candidate.IsAlive && candidate.PetOwner == player && SquadCombatState.IsMember(candidate);
}
