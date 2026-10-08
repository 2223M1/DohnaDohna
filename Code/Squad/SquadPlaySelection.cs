using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace DohnaDohna.Code.Squad;

/// <summary>One native play-series owns one actor and auxiliary target, including its replays.</summary>
public sealed class SquadPlaySelection(CardModel card, Creature actor, Creature? auxiliary)
{
    public CardModel Card { get; } = card;
    public Creature Actor { get; set; } = actor;
    public Creature? Auxiliary { get; } = auxiliary;
    public CardPlay? Play { get; set; }
    public bool Substituting { get; set; }
    public bool AttackPresented { get; set; }
    private static readonly ConditionalWeakTable<CardModel, SquadPlaySelection> Queued = new();
    private static readonly AsyncLocal<SquadPlaySelection?> Active = new();

    public static SquadPlaySelection? CurrentFor(CardModel card) => Active.Value?.Card == card ? Active.Value : null;
    public static SquadPlaySelection? Current { get => Active.Value; set => Active.Value = value; }

    public static void Queue(SquadPlaySelection selection)
    {
        Queued.Remove(selection.Card);
        Queued.Add(selection.Card, selection);
    }
    public static SquadPlaySelection? Take(CardModel card)
    {
        Queued.TryGetValue(card, out var selection);
        Queued.Remove(card);
        return selection;
    }
}
