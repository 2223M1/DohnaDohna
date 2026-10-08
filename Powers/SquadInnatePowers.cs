using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DohnaDohna.Relics;

namespace DohnaDohna.Powers;

public abstract class SquadInnatePower : ModPowerTemplate
{
    public abstract string Role { get; }
    public SquadRoleRelic SourceRelic { get; private set; } = null!;
    private bool _detached;
    protected bool Enabled => !_detached && Owner.IsAlive && SourceRelic is { IsMelted: false, HasBeenRemovedFromState: false };
    public override bool ShouldReceiveCombatHooks => base.ShouldReceiveCombatHooks && Enabled;
    protected override bool IsVisibleInternal => false;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => new(IconPath: RoleDefinition.Get(Role).AssetRoot + "/portrait.png",
        BigIconPath: RoleDefinition.Get(Role).AssetRoot + "/portrait.png");
    protected SquadCombatState Squad => SquadCombatState.Get(Owner.PetOwner!);
    protected bool Performs(CardPlay play) => play.Card.Owner == Owner.PetOwner && SquadPlaySelection.CurrentFor(play.Card)?.Actor == Owner;
    public static async Task Install(SquadCombatState squad)
    {
        foreach (var relic in squad.Owner.Relics.OfType<SquadRoleRelic>().Where(r => !r.IsMelted && squad.IsAlive(r.Role)).ToArray())
            await Attach(squad, relic);
        await RefreshAuras(squad);
    }
    public static async Task Attach(SquadCombatState squad, SquadRoleRelic relic)
    {
        var actor = squad.GetActor(relic.Role);
        if (actor.Powers.OfType<SquadInnatePower>().Any()) throw new InvalidOperationException("Duplicate role passive relic.");
        {
            var power = squad.RoleOf(actor) switch
            {
                "kuma" => (PowerModel)ModelDb.Power<KumaInnate>(), "porno" => ModelDb.Power<PornoInnate>(),
                "tora" => ModelDb.Power<ToraInnate>(), "medhico" => ModelDb.Power<MedhicoInnate>(),
                "kirakira" => ModelDb.Power<KirakiraInnate>(), "antena" => ModelDb.Power<AntenaInnate>(),
                "kikuchiyo" => ModelDb.Power<KikuchiyoInnate>(), "alyce" => ModelDb.Power<AlyceInnate>(),
                "zappa" => ModelDb.Power<ZappaInnate>(), "joker" => ModelDb.Power<JokerInnate>(),
                _ => throw new InvalidOperationException("Unknown passive role.")
            };
            var mutable = (SquadInnatePower)power.ToMutable();
            mutable.SourceRelic = relic;
            await PowerCmd.Apply(new BlockingPlayerChoiceContext(), mutable, actor, relic.Multiplier, actor, null);
        }
        await RefreshAuras(squad);
    }
    public async Task Detach()
    {
        _detached = true;
        if (this is SquadAuraPower aura) await aura.Refresh();
    }
    public static async Task RefreshAuras(SquadCombatState squad)
    {
        foreach (var aura in squad.Actors.SelectMany(a => a.Powers).OfType<SquadAuraPower>().ToArray()) await aura.Refresh();
    }
}

public abstract class SquadAuraPower : SquadInnatePower
{
    private HashSet<Creature> _recipients = [];
    protected override void DeepCloneFields() { base.DeepCloneFields(); _recipients = new(_recipients); }
    protected abstract IEnumerable<Creature> Desired();
    protected abstract Task Change(Creature target, int sign);
    public async Task Refresh()
    {
        var desired = Enabled ? Desired().ToHashSet() : [];
        var remove = _recipients.Except(desired).ToArray();
        var add = desired.Except(_recipients).ToArray();
        _recipients.Clear();
        _recipients.UnionWith(desired);
        foreach (var target in remove) if (target.IsAlive) await Change(target, -1);
        foreach (var target in add) await Change(target, 1);
    }
}

[RegisterPower] public sealed class KumaInnate : SquadAuraPower
{
    public override string Role => "kuma";
    protected override IEnumerable<Creature> Desired() => Squad.Living.TakeWhile(c => c != Owner).Append(Owner);
    protected override Task Change(Creature target, int sign) => PowerCmd.Apply<StrengthPower>(new BlockingPlayerChoiceContext(), target, sign * Amount, Owner, null);
}
[RegisterPower] public sealed class PornoInnate : SquadAuraPower
{
    public override string Role => "porno";
    protected override IEnumerable<Creature> Desired() => Squad.Living.SkipWhile(c => c != Owner).Take(2);
    protected override Task Change(Creature target, int sign) => PowerCmd.Apply<ThornsPower>(new BlockingPlayerChoiceContext(), target, 2 * sign * Amount, Owner, null);
}
[RegisterPower] public sealed class ToraInnate : SquadInnatePower
{
    public override string Role => "tora";
    private HashSet<Type> _seen = [];
    protected override void DeepCloneFields() { base.DeepCloneFields(); _seen = new(_seen); }
    private bool _awarding, _turnReady;
    private static bool Qualifies(PowerModel p) => p.Amount > 0 && p.TypeForCurrentAmount == PowerType.Buff
        && p is StrengthPower or DexterityPower or VigorPower or FocusPower or ArtifactPower or BufferPower
            or IntangiblePower or ThornsPower or PlatingPower or RegenPower;
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    {
        if (player != Owner.PetOwner) return;
        _seen.Clear();
        _turnReady = true;
        foreach (var power in Owner.Powers.ToArray()) await Observe(context, power);
    }
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource) =>
        power.Owner == Owner ? Observe(context, power) : Task.CompletedTask;
    private async Task Observe(PlayerChoiceContext context, PowerModel power)
    {
        if (!_turnReady || _awarding || !Qualifies(power) || !_seen.Add(power.GetType())) return;
        _awarding = true;
        try { await PowerCmd.Apply<SquadTemporaryStrength>(context, Owner, Amount, Owner, null); }
        finally { _awarding = false; }
    }
}
[RegisterPower] public sealed class MedhicoInnate : SquadInnatePower
{
    public override string Role => "medhico";
    private bool _used;
    public async Task Exchange(Creature ally)
    {
        if (_used || !Enabled || ally.IsDead || ally == Owner) return;
        _used = true;
        Flash();
        SourceRelic.Flash();
        await CreatureCmd.Heal(ally, 3 * Amount);
    }
}
[RegisterPower] public sealed class KirakiraInnate : SquadInnatePower
{
    public override string Role => "kirakira";
    public async Task Choose(PlayerChoiceContext context)
    {
        var selected = await CardSelectCmd.FromHand(context, Owner.PetOwner!,
            new CardSelectorPrefs(new LocString("powers", "DOHNA_DOHNA_POWER_KIRAKIRA_INNATE.selection"), Amount), null, this);
        foreach (var card in selected) CardCmd.ApplyKeyword(card, CardKeyword.Retain);
    }
}
[RegisterPower] public sealed class AntenaInnate : SquadInnatePower
{
    public override string Role => "antena";
    private bool _used;
    private CardPlay? _first;
    private HashSet<Creature> _hit = [];
    protected override void DeepCloneFields() { base.DeepCloneFields(); _hit = new(_hit); }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    {
        if (player == Owner.PetOwner) { _used = false; _first = null; _hit.Clear(); }
        return Task.CompletedTask;
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (!_used && Performs(play) && play.Card.Type == CardType.Attack) { _used = true; _first = play; }
        return Task.CompletedTask;
    }
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || cardSource == null || !props.IsPoweredAttack() || _first == null
            || SquadPlaySelection.CurrentFor(cardSource)?.Play != _first || !_hit.Add(target)) return;
        foreach (var ally in Squad.Living.Where(c => c != Owner).ToArray())
            await PowerCmd.Apply<SquadTemporaryDexterity>(context, ally, Amount, Owner, cardSource);
    }
}
[RegisterPower] public sealed class KikuchiyoInnate : SquadInnatePower
{
    public override string Role => "kikuchiyo";
    private bool _used;
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    { if (player == Owner.PetOwner) _used = false; return Task.CompletedTask; }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (_used || !Performs(play) || play.Card.Type != CardType.Attack || Owner.IsDead) return;
        _used = true;
        Flash();
        SourceRelic.Flash();
        await CardPileCmd.Draw(context, Amount, Owner.PetOwner!);
    }
}
[RegisterPower] public sealed class AlyceInnate : SquadInnatePower
{
    public override string Role => "alyce";
    private HashSet<Creature> _supportedTargets = [];
    protected override void DeepCloneFields() { base.DeepCloneFields(); _supportedTargets = new(_supportedTargets); }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    { if (player == Owner.PetOwner) _supportedTargets.Clear(); return Task.CompletedTask; }
    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != null && dealer != Owner && dealer.PetOwner == Owner.PetOwner && SquadCombatState.IsMember(dealer)
            && cardSource?.Type == CardType.Attack && props.IsPoweredAttack()) _supportedTargets.Add(target);
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource
#if !DOHNADOHNA_STABLE
        , CardPlay? cardPlay
#endif
        ) => dealer == Owner && target != null && cardSource?.Type == CardType.Attack && props.IsPoweredAttack()
            && _supportedTargets.Contains(target) ? 4 * Amount : 0;
}
[RegisterPower] public sealed class ZappaInnate : SquadInnatePower
{
    public override string Role => "zappa";
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
        => side == CombatSide.Player && Owner.IsAlive && Squad.Front == Owner
            ? CreatureCmd.GainBlock(Owner, 4 * Amount, ValueProp.Unpowered, null) : Task.CompletedTask;
}
[RegisterPower] public sealed class JokerInnate : SquadInnatePower
{
    public override string Role => "joker";
    public Task Reordered() => Enabled ? PowerCmd.Apply<VigorPower>(new BlockingPlayerChoiceContext(), Owner, 2 * Amount, Owner, null) : Task.CompletedTask;
}
public sealed class SquadTemporaryDexterity : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<SquadSpecial>();
}
