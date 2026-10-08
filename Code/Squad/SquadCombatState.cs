using System.Runtime.CompilerServices;
using DohnaDohna.Cards;
using DohnaDohna.Content;
using DohnaDohna.Monsters;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.Code.Squad;

/// <summary>The four native entities are the only live HP/block/power state. Save data is a boundary snapshot.</summary>
public sealed class SquadCombatState : IDisposable
{
    private static readonly ConditionalWeakTable<Player, SquadCombatState> States = new();
    private readonly Dictionary<string, Creature> _actors = [];
    private readonly List<string> _order;
    private readonly HashSet<string> _confirmedDeaths = [];
    private readonly Dictionary<CardPlay, Creature> _playedActors = new(ReferenceEqualityComparer.Instance);
    public Player Owner { get; }
    public SquadActions Actions { get; }
    public CancellationTokenSource Cancellation { get; } = new();
    public IReadOnlyList<string> Order => _order;
    public IEnumerable<Creature> Actors => _order.Select(id => _actors[id]);
    public IEnumerable<Creature> Living => _order.Where(_actors.ContainsKey).Select(id => _actors[id]).Where(c => c.IsAlive);
    public Creature Front => Living.Last();
    public string FrontRole => RoleOf(Front);
    public int DeathCount => _confirmedDeaths.Count;

    private SquadCombatState(Player owner, SquadRunState snapshot)
    {
        Owner = owner;
        _order = snapshot.Members.Select(m => m.RoleId).ToList();
        Actions = new SquadActions(this);
    }

    public static SquadCombatState? TryGet(Player? player) => player != null && States.TryGetValue(player, out var state) ? state : null;
    public static SquadCombatState Get(Player player) => TryGet(player) ?? throw new InvalidOperationException("Squad combat has not been initialized.");
    public static bool IsMember(Creature creature) => creature.Monster is SquadMember;
    public bool IsAlive(string role) => _actors.TryGetValue(role, out var actor) && actor.IsAlive;
    public string RoleOf(Creature actor) => ((SquadMember)actor.Monster!).RoleId;
    public Creature GetActor(string role) => _actors[role];
    public void RecordPlay(CardPlay play, Creature actor) => _playedActors[play] = actor;
    public Creature? ActorOf(CardPlay play) => _playedActors.GetValueOrDefault(play);

    public static async Task Create(Player owner)
    {
        if (owner.Character is not DohnaSquad) return;
        TryGet(owner)?.Dispose();
        var snapshot = SquadStore.Get(owner);
        var state = new SquadCombatState(owner, snapshot);
        States.Add(owner, state);
        // Complete the entity map before native AddCreature layout callbacks run.
        foreach (var member in snapshot.Members)
        {
            var model = (SquadMember)ModelDb.Monster<SquadMember>().ToMutable();
            model.RoleId = member.RoleId;
            var actor = owner.Creature.CombatState!.CreateCreature(model, CombatSide.Player, null);
            actor.SetMaxHpInternal(member.MaxHp);
            actor.SetCurrentHpInternal(member.Hp);
            state._actors.Add(member.RoleId, actor);
            if (member.Hp == 0) state._confirmedDeaths.Add(member.RoleId);
        }
        foreach (var actor in state.Actors)
        {
            await PlayerCmd.AddPet(actor, owner);
            if (actor.IsDead) NCombatRoom.Instance?.GetCreatureNode(actor)?.Hide();
        }
        // Native AfterRoomEntered/Grow runs before BeforeCombatStart creates
        // these members. Reapply only the owned relic's visual at this boundary.
        if (owner.GetRelic<MegaCrit.Sts2.Core.Models.Relics.BigMushroom>() is { IsMelted: false })
            DohnaDohna.Code.Patches.SquadBigMushroomPatch.GrowMembers(state);
        state.SyncController();
        state.Layout();
        await DohnaDohna.Powers.SquadInnatePower.Install(state);
        if (NCombatRoom.Instance is { } room) DohnaDohna.Code.UI.SquadSharedDisplay.Create(state, room);
        if (state.IsAlive("antena")) await OrbCmd.AddSlots(owner, 3);
    }

    public void SyncController()
    {
        if (Living.LastOrDefault() is not { } front) return; // Native Kill owns controller defeat.
        Owner.Creature.SetMaxHpInternal(front.MaxHp);
        Owner.Creature.SetCurrentHpInternal(front.CurrentHp);
    }

    public void Layout(bool animate = false)
    {
        if (NCombatRoom.Instance is not { } room) return;
        var encounter = Owner.Creature.CombatState?.Encounter;
        SquadFormation.Ensure(room).Reflow(this, encounter?.GetCameraScaling() ?? room.SceneContainer.Scale.X,
            encounter?.FullyCenterPlayers ?? false, animate);
    }

    public async Task Swap(Creature target)
    {
        if (!SquadTargeting.IsLivingMember(target, Owner) || !Living.Any() || target == Front) return;
        var before = Living.ToArray();
        var oldFront = Front;
        int a = _order.IndexOf(FrontRole), b = _order.IndexOf(RoleOf(target));
        (_order[a], _order[b]) = (_order[b], _order[a]);
        await Reordered(before, oldFront, target, directSwap: true);
        SyncController();
        Layout(animate: true);
        if (NCombatRoom.Instance is { } room) await SquadFormation.Ensure(room).FinishMovement(Cancellation.Token);
    }

    public async Task MoveToFront(Creature actor)
    {
        if (!SquadTargeting.IsLivingMember(actor, Owner) || actor == Front) return;
        var before = Living.ToArray();
        var destination = Front;
        _order.Remove(RoleOf(actor));
        _order.Add(RoleOf(actor));
        await Reordered(before, actor, destination, directSwap: false);
        SyncController();
        Layout(animate: true);
        if (NCombatRoom.Instance is { } room) await SquadFormation.Ensure(room).FinishMovement(Cancellation.Token);
    }

    private async Task Reordered(Creature[] before, Creature mover, Creature destination, bool directSwap)
    {
        await DohnaDohna.Powers.SquadInnatePower.RefreshAuras(this);
        if (IsAlive("medhico"))
        {
            var medic = GetActor("medhico");
            bool crossed = Array.IndexOf(before, medic) != Array.IndexOf(Living.ToArray(), medic);
            var recipient = mover == medic ? destination
                : directSwap && destination == medic || !directSwap && crossed ? mover : null;
            if (recipient != null && medic.GetPower<DohnaDohna.Powers.MedhicoInnate>() is { } passive) await passive.Exchange(recipient);
        }
        if (IsAlive("joker") && Array.IndexOf(before, GetActor("joker")) != Array.IndexOf(Living.ToArray(), GetActor("joker")))
            if (GetActor("joker").GetPower<DohnaDohna.Powers.JokerInnate>() is { } passive) await passive.Reordered();
    }

    public async Task RotateFront()
    {
        if (Living.Count() < 2) return;
        var oldFront = FrontRole;
        _order.Remove(oldFront);
        _order.Insert(0, oldFront);
        SyncController();
        Layout(animate: true);
        if (NCombatRoom.Instance is { } room) await SquadFormation.Ensure(room).FinishMovement(Cancellation.Token);
    }

    public async Task ConfirmDeath(Creature actor, bool wasPrevented)
    {
        if (wasPrevented || actor.IsAlive || !_confirmedDeaths.Add(RoleOf(actor))) return;
        if (RoleOf(actor) == "antena")
        {
            // Removal, not evocation: native queue capacity survives this death.
            var queue = Owner.PlayerCombatState!.OrbQueue;
            foreach (var orb in queue.Orbs.ToArray()) { queue.Remove(orb); orb.RemoveInternal(); }
            var manager = NCombatRoom.Instance?.GetCreatureNode(Owner.Creature)?.OrbManager;
            manager?.ClearOrbs();
            manager?.AddSlotAnim(queue.Capacity);
        }
        await DohnaDohna.Powers.SquadInnatePower.RefreshAuras(this);
        SyncController();
        Layout();
        await RoleVisuals.PlayDeath(actor, Cancellation.Token);
        if (!Living.Any())
        {
            // Native LoseCombat deactivates powers and does not run the victory-only
            // AfterCombatEnd hook. Capture before native defeat serializes the run.
            Capture();
            await CreatureCmd.Kill(Owner.Creature);
        }
    }

    public void Capture()
    {
        var snapshot = new SquadRunState
        {
            Members = Actors.Select(c => new SquadMemberState { RoleId = RoleOf(c), Hp = c.CurrentHp, MaxHp = c.MaxHp }).ToList()
        };
        snapshot.Validate();
        SquadStore.State.Set((RunState)Owner.RunState, Owner.NetId, snapshot);
    }

    public static void DisposeForRoom(NCombatRoom room)
    {
        foreach (var entry in States.ToArray())
            if (entry.Value._actors.Values.Any(c => room.GetCreatureNode(c) != null)) entry.Value.Dispose();
    }

    public void Dispose()
    {
        States.Remove(Owner);
        Cancellation.Cancel();
        Cancellation.Dispose();
    }
}
