using System.Security.Cryptography;
using System.Text.Json;
using DohnaDohna.Cards;
using DohnaDohna.Code.Patches;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    // Uses the actual optional assembly and native commands. Reflection is
    // confined to this test driver; no copied rune algorithm or product bridge.
    private async Task VerifyHextech(Player owner, RunState run)
    {
        var hex = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "HextechRunes");
        RelicModel Rune(string name) => ModelDb.GetById<RelicModel>(ModelDb.GetId(hex.GetType("HextechRunes." + name, true)!)).ToMutable();
        var context = new BlockingPlayerChoiceContext();
        await SetCatalogRoster(owner, run, SquadRunState.Create(["kuma", "alyce", "antena", "zappa"]));
        var brutal = await RelicCmd.Obtain(Rune("BrutalForceRune"), owner);
        var arcane = await RelicCmd.Obtain(Rune("ArcanePunchRune"), owner);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        CatalogCheck(squad.Front.GetPowerAmount<StrengthPower>() == 1 && !owner.Creature.HasPower<StrengthPower>(),
            "real Hextech BrutalForce opening Strength belongs to the front");
        CatalogCheck(owner.PlayerCombatState!.Hand.Cards.Count == 7, "real Hextech opening +2 draw occurs once, not four times");
        var pool = ModelDb.Character<DohnaDohna.Content.DohnaSquad>().CardPool.AllCards.OfType<SquadCatalogCard>();
        CatalogCheck(SquadPool.ForPlayer(owner, pool).Count() == 80, "optional mods preserve the selected 80-card pool");
        var enemy = combat.Enemies.OrderByDescending(c => c.MaxHp).First();
        await CreatureCmd.GainMaxHp(enemy, 100);
        int energy = owner.PlayerCombatState.Energy;
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), enemy);
        CatalogCheck(owner.PlayerCombatState.Energy == energy, "real Hextech first attack does not grant energy early");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), enemy);
        CatalogCheck(owner.PlayerCombatState.Energy == energy + 1, "real Hextech two attacks grant shared energy exactly once");
        await squad.Swap(squad.GetActor("alyce"));
        CatalogCheck(squad.GetActor("zappa").GetPowerAmount<StrengthPower>() == 1 && squad.Front.GetPowerAmount<StrengthPower>() == 0,
            "Hextech personal Strength does not follow a later front change");
        await Screenshot("hextech-shared-and-personal.png");

        var nearDeathRune = Rune("NearDeathFeastRune");
        CatalogCheck(!(bool)AccessTools.Method(nearDeathRune.GetType(), "IsAvailableForPlayer").Invoke(nearDeathRune, [owner])!,
            "Ironclad-only player near-death rune is naturally excluded; no fake character identity");
        await RelicCmd.Remove(brutal);
        await RelicCmd.Remove(arcane);

        // The modifier's own debug fixture activates a real enemy hex in this
        // already-running combat, without replaying act selection UI callbacks.
        var modifierType = hex.GetType("HextechRunes.HextechMayhemModifier", true)!;
        var modifier = run.Modifiers.SingleOrDefault(m => m.GetType() == modifierType);
        if (modifier == null)
        {
            modifier = ModelDb.GetById<ModifierModel>(ModelDb.GetId(modifierType)).ToMutable();
            run.AddModifierDebug(modifier);
            modifier.OnRunCreated(run);
        }
        var kind = Enum.Parse(hex.GetType("HextechRunes.MonsterHexKind", true)!, "NearDeathFeast");
        var rarity = Enum.Parse(hex.GetType("HextechRunes.HextechRarityTier", true)!, "Silver");
        AccessTools.Method(modifierType, "DebugSetOnlyMonsterHex").Invoke(modifier, [run.CurrentActIndex, kind, rarity]);
        await (Task)AccessTools.Method(modifierType, "ApplyToCurrentEnemiesIfNeeded").Invoke(modifier, null)!;
        CatalogCheck((bool)AccessTools.Method(modifierType, "HasActiveMonsterHex").Invoke(modifier, [kind])!, "actual enemy NearDeathFeast is active");
        foreach (var other in combat.Enemies.Where(c => c != enemy).ToArray()) await CreatureCmd.Kill(other, true);
        foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
        // Alyce has a same-turn follow-up bonus against this already-attacked
        // target. Use an unbuffed Zappa for an exact 6-damage debt boundary.
        await squad.Swap(squad.GetActor("zappa"));
        await PowerCmd.Remove<StrengthPower>(squad.Front);
        await CreatureCmd.SetCurrentHp(enemy, 3);
        var strike = combat.CreateCard<SquadStrike>(owner);
        CatalogCheck(!SquadFinisher.CanFinish(new AttackCommand(6).FromCard(strike, null).Targeting(enemy), strike, null, [enemy]),
            "external HP interception prevents a speculative clearing closeup");
        await CardCmd.AutoPlay(context, strike, enemy);
        CatalogCheck(enemy.CurrentHp == 1 && enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding,
            $"real Hextech negative HP debt survives lethal-looking squad card: hp={enemy.CurrentHp}, alive={enemy.IsAlive}, ending={CombatManager.Instance.IsOverOrEnding}");
        CatalogCheck(enemy.GetPowerAmount<StrengthPower>() == 3, "enemy debt triggers its own native Strength exactly once");
        CatalogCheck(RoleFinisher.Current == null, "no clearing closeup after real Hextech survival");
        await Screenshot("hextech-near-death-survival.png");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), enemy);
        CatalogCheck(enemy.IsDead, "next real card crosses enemy debt limit and completes native death");
        await CombatManager.Instance.CheckWinCondition();
        var proof = new {
            mode = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode.ToString(),
            sourceCommit = "e0529bf5f5148a56f3f7857a858c499c82d1f8fd", version = "0.9.7 source build, not Steam binary",
            hextechDllSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(hex.Location))).ToLowerInvariant(),
            assertions = "startup Strength, opening draw, shared energy, fixed power owner, 80-card pool, enemy near-death survival/death",
            notRun = new[] { "full act selection", "all runes", "forced player near-death on squad", "multiplayer" }
        };
        File.WriteAllText(Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "hextech.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
    }
}
