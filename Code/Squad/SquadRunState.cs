using DohnaDohna.Content;

namespace DohnaDohna.Code.Squad;

public sealed class SquadMemberState
{
    public string RoleId { get; set; } = "";
    public int Hp { get; set; } = 25;
    public int MaxHp { get; set; } = 25;
}

/// <summary>Only run facts are persisted. Combat entities and card piles belong to the host.</summary>
public sealed class SquadRunState
{
    public List<SquadMemberState> Members { get; set; } = [];

    public void Validate()
    {
        if (Members.Count != 4 || Members.Select(m => m.RoleId).Distinct().Count() != 4)
            throw new InvalidDataException("A DohnaDohna squad must contain four distinct roles.");
        foreach (var member in Members)
        {
            _ = RoleDefinition.Get(member.RoleId);
            if (member.MaxHp < 1 || member.Hp < 0 || member.Hp > member.MaxHp)
                throw new InvalidDataException($"Invalid HP for {member.RoleId}.");
        }
    }

    public static SquadRunState Create(IEnumerable<string> orderedIds)
    {
        var state = new SquadRunState
        {
            Members = orderedIds.Select(id => new SquadMemberState
            {
                RoleId = id, Hp = RoleDefinition.Get(id).StartingHp, MaxHp = RoleDefinition.Get(id).StartingHp
            }).ToList()
        };
        state.Validate();
        return state;
    }

    public SquadMemberState? Front => Members.LastOrDefault(m => m.Hp > 0);
    public SquadRunState Copy() => new()
    {
        Members = Members.Select(m => new SquadMemberState { RoleId = m.RoleId, Hp = m.Hp, MaxHp = m.MaxHp }).ToList()
    };
    public bool IsAlive(string id) => Members.Single(m => m.RoleId == id).Hp > 0;

    public void Swap(string first, string second)
    {
        var a = Members.FindIndex(m => m.RoleId == first);
        var b = Members.FindIndex(m => m.RoleId == second);
        if (a < 0 || b < 0 || Members[a].Hp == 0 || Members[b].Hp == 0)
            throw new ArgumentException("Swap targets must be living squad members.");
        (Members[a], Members[b]) = (Members[b], Members[a]);
    }

    public void RotateFront()
    {
        var living = Members.Where(m => m.Hp > 0).ToList();
        if (living.Count < 2) return;
        var oldFront = living[^1];
        Members.Remove(oldFront);
        Members.Insert(0, oldFront);
    }

    public void ReviveAtRear(string id)
    {
        var member = Members.Single(m => m.RoleId == id);
        if (member.Hp != 0) throw new ArgumentException("Only a dead member can be revived.");
        // Still at zero: the native healing command decides the resulting HP.
        Members.Remove(member);
        Members.Insert(0, member);
    }
}
