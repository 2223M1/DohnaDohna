using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public sealed class SquadMaxHpGainPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_shared_max_hp_gain";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), [typeof(Creature), typeof(decimal)])];
    public static void Prefix(ref Creature creature, ref decimal amount, out (Player? Owner, string? Role, int Amount) __state)
    {
        __state = default;
        if (creature.Player is not { Character: DohnaSquad } owner) return;
        if (amount < 0) return; // Native input validation is authoritative.
        amount = decimal.Ceiling(amount / 4);
        var squad = SquadCombatState.TryGet(owner);
        var role = squad?.FrontRole ?? SquadStore.Get(owner).Front!.RoleId;
        __state = (owner, role, (int)amount);
        if (squad != null) creature = squad.Front;
    }
    public static void Postfix((Player? Owner, string? Role, int Amount) __state, ref Task __result)
    {
        if (__state.Owner != null) __result = Complete(__result, __state.Owner, __state.Role!, __state.Amount);
    }
    private static async Task Complete(Task original, Player owner, string role, int amount)
    {
        await original;
        if (SquadCombatState.TryGet(owner) is { } squad)
        {
            foreach (var actor in squad.Actors.Where(c => squad.RoleOf(c) != role))
            {
                if (actor.IsAlive) await CreatureCmd.GainMaxHp(actor, amount);
                else actor.SetMaxHpInternal(actor.MaxHp + amount);
            }
            squad.SyncController();
        }
        else
        {
            var state = SquadStore.Get(owner);
            foreach (var member in state.Members)
            {
                if (member.RoleId == role) { member.MaxHp = owner.Creature.MaxHp; member.Hp = owner.Creature.CurrentHp; }
                else { member.MaxHp += amount; if (member.Hp > 0) member.Hp = Math.Min(member.MaxHp, member.Hp + amount); }
            }
            SquadStore.State.Set((RunState)owner.RunState, owner.NetId, state);
        }
    }
}

public sealed class SquadMaxHpLossPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_shared_max_hp_loss";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp),
        [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(bool)])];
    public static void Prefix(ref Creature creature, ref decimal amount, out (Player? Owner, string? Role, int Amount) __state)
    {
        __state = default;
        if (creature.Player is not { Character: DohnaSquad } owner || amount < 0) return;
        amount = decimal.Ceiling(amount / 4);
        var squad = SquadCombatState.TryGet(owner);
        __state = (owner, squad?.FrontRole ?? SquadStore.Get(owner).Front!.RoleId, (int)amount);
        if (squad != null) creature = squad.Front;
    }
    public static void Postfix(PlayerChoiceContext choiceContext, bool isFromCard,
        (Player? Owner, string? Role, int Amount) __state, ref Task __result)
    {
        if (__state.Owner != null) __result = Complete(__result, choiceContext, isFromCard, __state.Owner, __state.Role!, __state.Amount);
    }
    private static async Task Complete(Task original, PlayerChoiceContext context, bool fromCard, Player owner, string role, int amount)
    {
        await original;
        if (SquadCombatState.TryGet(owner) is { } squad)
        {
            foreach (var actor in squad.Actors.Where(c => squad.RoleOf(c) != role))
            {
                if (actor.IsAlive) await CreatureCmd.LoseMaxHp(context, actor, amount, fromCard);
                else actor.SetMaxHpInternal(Math.Max(1, actor.MaxHp - amount));
            }
            squad.SyncController();
        }
        else
        {
            var state = SquadStore.Get(owner);
            foreach (var member in state.Members)
            {
                if (member.RoleId == role) { member.MaxHp = owner.Creature.MaxHp; member.Hp = owner.Creature.CurrentHp; }
                else { member.MaxHp = Math.Max(1, member.MaxHp - amount); member.Hp = Math.Min(member.Hp, member.MaxHp); }
            }
            SquadStore.State.Set((RunState)owner.RunState, owner.NetId, state);
        }
    }
}
