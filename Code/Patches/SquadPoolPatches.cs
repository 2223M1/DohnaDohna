using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

public static class SquadPool
{
    public static IEnumerable<CardModel> ForPlayer(Player player, IEnumerable<CardModel> options)
    {
        if (player.Character is not DohnaSquad) return options;
        var members = SquadStore.Get(player).Members.Select(m => m.RoleId).ToHashSet();
        return options.Where(c => c is not SquadAttackCard { FixedRole: not null }
            && (c is not SquadCardModel { FixedRole: { } role } || members.Contains(role)));
    }
}

public sealed class SquadRewardPoolPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_reward_pool";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards), [typeof(Player)])];
    public static void Postfix(Player player, ref IEnumerable<CardModel> __result) => __result = SquadPool.ForPlayer(player, __result);
}

public sealed class SquadMerchantPoolPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_merchant_pool";
    public static ModPatchTarget[] GetTargets() =>
    [new(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType)]),
     new(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardRarity)])];
    public static void Prefix(Player player, ref IEnumerable<CardModel> options) => options = SquadPool.ForPlayer(player, options);
}

public sealed class SquadGeneratedPoolPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_generated_pool";
    public static ModPatchTarget[] GetTargets() =>
    [new(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(int), typeof(MegaCrit.Sts2.Core.Random.Rng)]),
     new(typeof(CardFactory), nameof(CardFactory.GetForCombat), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(int), typeof(MegaCrit.Sts2.Core.Random.Rng)])];
    public static void Prefix(Player player, ref IEnumerable<CardModel> cards) => cards = SquadPool.ForPlayer(player, cards);
}

public sealed class SquadTransformPoolPatch : IPatchMethod
{
    public static string PatchId => "dohnadohna_transform_pool";
    public static ModPatchTarget[] GetTargets() => [new(typeof(CardFactory), "GetFilteredTransformationOptions",
        [typeof(CardModel), typeof(IEnumerable<CardModel>), typeof(bool)])];
    public static void Prefix(CardModel original, ref IEnumerable<CardModel> originalOptions) =>
        originalOptions = SquadPool.ForPlayer(original.Owner, originalOptions);
}
