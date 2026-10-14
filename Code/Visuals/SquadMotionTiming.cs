using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace DohnaDohna.Code.Visuals;

internal enum AttackWindup { Automatic, Normal, Heavy }
internal enum CastWindup { Automatic, Short, Long }

// Card-owned authoring choices. These are not saved battle state and never
// change the actor, melee insertion, native hit count or projectile waits.
internal static class SquadMotionTiming
{
    internal static float Attack(CardModel? card, float automatic = .15f) =>
        (card as SquadCardModel)?.AttackWindup switch
        {
            AttackWindup.Normal => .15f,
            AttackWindup.Heavy => .20f,
            _ => automatic
        };

    internal static float Cast(CardModel? card, bool power = false, float? explicitDelay = null)
    {
        if (card is SquadCardModel { CastWindup: CastWindup.Short }) return .25f;
        if (card is SquadCardModel { CastWindup: CastWindup.Long }) return .40f;
        if (explicitDelay is { } specified) return specified;
        // 0.111.0 reference values on BOTH supported hosts, without replacing
        // the real Player/Character to make an adapted prototype behave.
        return card is SquadCatalogCard prototype ? prototype.PrototypeCard.Pool switch
        {
            NecrobinderCardPool => .40f,
            DefectCardPool when power => .50f,
            _ => .25f
        } : .25f;
    }

    internal static string Cue(CardModel? card) => card switch
    {
        SquadCardModel { IsFallback: true } => "strike",
        SquadAttackCard attack => attack.IsStrike ? "strike" : "special",
        SquadCardModel { IsMelee: true } => "special",
        _ => "strike"
    };
}
