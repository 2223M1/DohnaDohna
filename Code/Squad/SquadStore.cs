using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace DohnaDohna.Code.Squad;

public static class SquadStore
{
    public static PlayerRunSavedData<SquadRunState> State { get; private set; } = null!;

    public static void Register()
    {
        using (RitsuLibFramework.BeginModDataRegistration("DohnaDohna"))
            State = RitsuLibFramework.GetRunSavedDataStore("DohnaDohna").RegisterPerPlayer(
                "squad", () => new SquadRunState(), new RunSavedDataOptions { SyncLobbyOnChange = true });
    }

    public static SquadRunState Get(Player owner)
    {
        var state = State.Get(owner);
        state.Validate();
        return state;
    }
}
