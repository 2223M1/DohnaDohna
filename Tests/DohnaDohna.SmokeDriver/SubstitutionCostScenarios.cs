using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyNativeSubstitutionCosts(Player owner, RunState run)
    {
        var state = SquadRunState.Create(["kuma", "alyce", "antena", "tora"]);
        state.Members.Single(m => m.RoleId == "antena").Hp = 0;
        await SetCatalogRoster(owner, run, state);
        await EnterBattle(owner);
        var combat = owner.Creature.CombatState!;
        var context = new BlockingPlayerChoiceContext();
        await CardCmd.Discard(context, PileType.Hand.GetPile(owner).Cards.ToArray());
        await RelicCmd.Obtain<SneckoEye>(owner);
        CatalogCheck(owner.Creature.HasPower<ConfusedPower>(), "native Snecko Eye installs shared Confused");
        var costs = new List<int>();
        SquadTempest? original = null;
        for (int i = 0; i < 6; i++)
        {
            var card = combat.CreateCard<SquadTempest>(owner);
            original = card;
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
            var drawn = (await CardPileCmd.Draw(context, 1, owner)).Single();
            var modifiers = (List<LocalCostModifier>)AccessTools.Field(typeof(CardEnergyCost), "_localModifiers").GetValue(card.EnergyCost)!;
            int nativeRoll = modifiers.Last().Amount;
            CatalogCheck(drawn == card && card.IsFallback && nativeRoll is >= 0 and <= 3
                && card.EnergyCost.GetWithModifiers(CostModifiers.None) == 1
                && card.EnergyCost.GetAmountToSpend() == nativeRoll, "native Confused draw modifies substitute base via original modifier list");
            costs.Add(nativeRoll);
        }
        var clone = (SquadTempest)original!.CreateClone();
        await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, owner);
        await CardCmd.AutoPlay(context, combat.CreateCard<BulletTime>(owner), null);
        CatalogCheck(PileType.Hand.GetPile(owner).Cards.All(c => !c.EnergyCost.CostsX && c.EnergyCost.GetAmountToSpend() == 0),
            "real Bullet Time makes substituted X cards and their native clone free");
        clone.EnergyCost.AddThisCombat(1);
        CatalogCheck(clone.EnergyCost.GetAmountToSpend() == 1 && original.EnergyCost.GetAmountToSpend() == 0,
            "later native relative modifier wins in application order and clones are independent");
        clone.EnergyCost.EndOfTurnCleanup();
        CatalogCheck(clone.EnergyCost.GetWithModifiers(CostModifiers.All) == costs[^1] + 1,
            "native expiry removes only Bullet Time modifier and keeps Confused / later additions");
        GD.Print("DOHNA_NATIVE_CONFUSED_ROLLS " + string.Join(',', costs));
        await Screenshot("substitution-native-costs.png");
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition:false);
        // Cost-preview boundary only: the real zero-HP rest revival is exercised by Recovery.
        SquadStore.Get(owner).Members.Single(m => m.RoleId == "antena").Hp = 1;
        CatalogCheck(!original.IsFallback && original.EnergyCost.CostsX && clone.EnergyCost.CostsX,
            "revived owner restores canonical X identity for originals and native clones");
    }
}
