using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using DohnaDohna.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DohnaDohna.Relics;

/// <summary>Native inventory/save identity; its short-lived personal power owns combat counters and aura recipients.</summary>
public abstract class SquadRoleRelic : ModRelicTemplate
{
    public abstract string Role { get; }
    public virtual int Multiplier => 1;
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool IsAllowedInShops => false;
    public override RelicAssetProfile AssetProfile => new(IconPath: RoleDefinition.Get(Role).AssetRoot + "/portrait.png",
        IconOutlinePath: RoleDefinition.Get(Role).AssetRoot + "/portrait.png", BigIconPath: RoleDefinition.Get(Role).AssetRoot + "/portrait.png");
    public static SquadRoleRelic Basic(string role) => role switch
    {
        "kuma" => ModelDb.Relic<KumaEmblem>(), "alyce" => ModelDb.Relic<AlyceEmblem>(),
        "antena" => ModelDb.Relic<AntenaEmblem>(), "tora" => ModelDb.Relic<ToraEmblem>(),
        "kikuchiyo" => ModelDb.Relic<KikuchiyoEmblem>(), "medhico" => ModelDb.Relic<MedhicoEmblem>(),
        "joker" => ModelDb.Relic<JokerEmblem>(), "zappa" => ModelDb.Relic<ZappaEmblem>(),
        "kirakira" => ModelDb.Relic<KirakiraEmblem>(), "porno" => ModelDb.Relic<PornoEmblem>(),
        _ => throw new ArgumentException("Unknown squad relic role.", nameof(role))
    };

    public static void RegisterStarters()
    {
        RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(e =>
        {
            // Per-player roster, not a mutable Character.StartingRelics list.
            foreach (var player in e.RunState.Players.Where(p => p.Character is DohnaSquad)) AddStarters(player, 1);
        }, replayCurrentState: false);
        RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(e =>
        {
            // Actual pre-catalog saves carried this exact marker. Migrate once at
            // the load boundary; never replenish an absent or removed new relic.
            foreach (var player in e.RunState.Players.Where(p => p.Character is DohnaSquad))
            {
                if (player.GetRelic<SquadStandard>() is not { IsMelted: false } legacy
                    || player.Relics.OfType<SquadRoleRelic>().Any()) continue;
                player.RemoveRelicInternal(legacy);
                AddStarters(player, legacy.FloorAddedToDeck);
            }
        }, replayCurrentState: false);
    }

    private static void AddStarters(Player player, int floor)
    {
        foreach (var member in SquadStore.Get(player).Members)
        {
            var relic = Basic(member.RoleId).ToMutable();
            relic.FloorAddedToDeck = floor;
            player.AddRelicInternal(relic);
            SaveManager.Instance.MarkRelicAsSeen(relic);
        }
    }

    public override async Task AfterObtained()
    {
        if (SquadCombatState.TryGet(Owner) is { } squad && squad.IsAlive(Role))
            await SquadInnatePower.Attach(squad, this);
    }
    public override async Task AfterRemoved()
    {
        if (SquadCombatState.TryGet(Owner) is not { } squad || !squad.Order.Contains(Role)) return;
        var power = squad.GetActor(Role).Powers.OfType<SquadInnatePower>().SingleOrDefault(p => p.SourceRelic == this);
        if (power == null) return;
        await power.Detach();
        await PowerCmd.Remove(power);
    }
}

[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(KumaEmblemPlus))]
public sealed class KumaEmblem : SquadRoleRelic { public override string Role => "kuma"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class KumaEmblemPlus : SquadRoleRelic { public override string Role => "kuma"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(AlyceEmblemPlus))]
public sealed class AlyceEmblem : SquadRoleRelic { public override string Role => "alyce"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class AlyceEmblemPlus : SquadRoleRelic { public override string Role => "alyce"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(AntenaEmblemPlus))]
public sealed class AntenaEmblem : SquadRoleRelic { public override string Role => "antena"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class AntenaEmblemPlus : SquadRoleRelic { public override string Role => "antena"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(ToraEmblemPlus))]
public sealed class ToraEmblem : SquadRoleRelic { public override string Role => "tora"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class ToraEmblemPlus : SquadRoleRelic { public override string Role => "tora"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(KikuchiyoEmblemPlus))]
public sealed class KikuchiyoEmblem : SquadRoleRelic { public override string Role => "kikuchiyo"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class KikuchiyoEmblemPlus : SquadRoleRelic { public override string Role => "kikuchiyo"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(MedhicoEmblemPlus))]
public sealed class MedhicoEmblem : SquadRoleRelic { public override string Role => "medhico"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class MedhicoEmblemPlus : SquadRoleRelic { public override string Role => "medhico"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(JokerEmblemPlus))]
public sealed class JokerEmblem : SquadRoleRelic { public override string Role => "joker"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class JokerEmblemPlus : SquadRoleRelic { public override string Role => "joker"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(ZappaEmblemPlus))]
public sealed class ZappaEmblem : SquadRoleRelic { public override string Role => "zappa"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class ZappaEmblemPlus : SquadRoleRelic { public override string Role => "zappa"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(KirakiraEmblemPlus))]
public sealed class KirakiraEmblem : SquadRoleRelic { public override string Role => "kirakira"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class KirakiraEmblemPlus : SquadRoleRelic { public override string Role => "kirakira"; public override int Multiplier => 2; }
[RegisterRelic(typeof(SquadRelicPool)), RegisterTouchOfOrobasRefinement(typeof(PornoEmblemPlus))]
public sealed class PornoEmblem : SquadRoleRelic { public override string Role => "porno"; }
[RegisterRelic(typeof(SquadRelicPool))]
public sealed class PornoEmblemPlus : SquadRoleRelic { public override string Role => "porno"; public override int Multiplier => 2; }
