using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.UI;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyCoreSquadRules(Player owner, RunState run)
    {
        var fixture = SquadRunState.Create(["alyce", "antena", "kikuchiyo", "medhico"]);
        foreach (var member in fixture.Members) member.Hp = member.MaxHp = 25;
        await SetCatalogRoster(owner, run, fixture);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var context = new BlockingPlayerChoiceContext();
        var combat = owner.Creature.CombatState!;
        var actors = squad.Living.ToArray();
        foreach (var actor in actors)
        {
            var potion = await PotionCmd.TryToProcure<BlockPotion>(owner);
            CatalogCheck(potion.success && potion.potion.IsValidTarget(actor), "native potion accepts " + squad.RoleOf(actor));
            await potion.potion.OnUseWrapper(context, actor);
            CatalogCheck(actor.Block == 12, "potion block belongs only to selected member");
        }
        await PowerCmd.Apply<StrengthPower>(context, actors[0], 2, actors[0], null);
        await PowerCmd.Apply<DexterityPower>(context, actors[1], 3, actors[1], null);
        await squad.Swap(actors[0]);
        CatalogCheck(actors[0].GetPowerAmount<StrengthPower>() == 2 && actors[1].GetPowerAmount<DexterityPower>() == 3
            && actors.All(c => c.Block == 12), "native member stats do not transfer on swap");
        await VerifyRepeatedFormation(squad);
        foreach (var actor in actors) await CreatureCmd.LoseBlock(context, actor, actor.Block, null);
        var enemy = combat.Enemies.First();
        int hp = squad.Front.CurrentHp;
        var area = await DamageCmd.Attack(3).FromMonster(enemy.Monster!).WithNoAttackerAnim().Execute(context);
        CatalogCheck(area.Results.SelectMany(r => r).Count() == 1 && squad.Front.CurrentHp == hp - 3, "enemy area attack hits squad once");
        await PowerCmd.Apply<WeakPower>(context, owner.Creature, 2, enemy, null);
        CatalogCheck(squad.Front.GetPowerAmount<WeakPower>() == 2 && !owner.Creature.HasPower<WeakPower>(), "hostile status resolves to front");
        var poison = await PowerCmd.Apply<PoisonPower>(context, squad.Front, 2, enemy, null);
        hp = squad.Front.CurrentHp;
        await poison!.Trigger();
        CatalogCheck(squad.Front.CurrentHp == hp - 2, "native poison damages its member");
        while (squad.Living.Any())
            await CreatureCmd.Damage(context, owner.Creature, 100, ValueProp.Move | ValueProp.Unpowered | ValueProp.Unblockable, enemy);
        CatalogCheck(owner.Creature.IsDead && SquadStore.Get(owner).Members.All(m => m.Hp == 0), "four dead members enter native defeat and snapshot");
        foreach (var card in owner.PlayerCombatState!.AllCards.Where(c => c is SquadCardModel or SquadDefend or SquadSwap))
        {
            _ = card.TargetType;
            CatalogCheck(!card.CanPlay(), "native defeat leaves no playable squad card");
        }
    }

    private async Task ClickNativeChoice(Control button, string label)
    {
        // Native choose-a-card screen ignores selections for its first 350ms.
        await ToSignal(GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
        int options = GetTree().Root.GetChildrenRecursive<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NGridCardHolder>()
            .Count(h => h.CardModel is SquadReviveChoice or SquadRelicUpgradeChoice);
        await Screenshot($"native-{label}-{options}.png");
        var point = button.GetViewport().GetFinalTransform() * button.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = new Vector2(20, 0) });
        Input.FlushBufferedEvents();
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left,
            Pressed = true
        });
        Input.FlushBufferedEvents();
        for (int i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            Pressed = false
        });
        Input.FlushBufferedEvents();
        GD.Print($"DOHNA_SMOKE_CHOICE_MOUSE_INPUT_SENT {label} options={options}");
    }

    private async Task VerifyRestChoices(Player owner, RunState run)
    {
        var saved = SquadStore.Get(owner).Copy();
        var deck = owner.Deck.Cards.ToArray();
        for (int count = 1; count <= 3; count++)
        {
            var fixture = SquadRunState.Create(["kuma", "alyce", "antena", "tora"]);
            foreach (var member in fixture.Members) member.Hp = member.MaxHp = 25;
            foreach (var member in fixture.Members.Take(count)) member.Hp = 0;
            fixture.Members[^1].Hp = 5;
            SquadStore.State.Set(run, owner.NetId, fixture);
            owner.Creature.SetMaxHpInternal(25);
            owner.Creature.SetCurrentHpInternal(5);
            var chosenRole = fixture.Members[count - 1].RoleId;
            _reviveInput = null;
            _respondChoice = chosenRole;
            await HealRestSiteOption.ExecuteRestSiteHeal(owner, false).WaitAsync(TimeSpan.FromSeconds(20));
            await (_reviveInput ?? throw new Exception("No native revival input was dispatched"));
            var restored = SquadStore.Get(owner);
            if (restored.Members[0].RoleId != chosenRole || restored.Members[0].Hp != 7
                || restored.Members.Count(m => m.Hp == 0) != count - 1 || restored.Front!.Hp != 12
                || !owner.Deck.Cards.SequenceEqual(deck))
                throw new Exception("Native revival choice changed the wrong member, HP, order or deck");
            GD.Print($"DOHNA_SMOKE_NATIVE_REVIVE_CHOICE_PASS options={count} selected={chosenRole} hp=7 front=12");
            // Native overlay release is deferred; wait before opening the next fixture.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        SquadStore.State.Set(run, owner.NetId, saved);
        owner.Creature.SetMaxHpInternal(saved.Front!.MaxHp);
        owner.Creature.SetCurrentHpInternal(saved.Front.Hp);
    }

    private async Task VerifyBoundaryCases(Player owner, RunState run)
    {
        var saved = SquadStore.Get(owner).Copy();
        var fixture = SquadRunState.Create(["kuma", "alyce", "tora", "porno"]);
        foreach (var member in fixture.Members) member.Hp = member.MaxHp = 25;
        await SetCatalogRoster(owner, run, fixture);
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var context = new BlockingPlayerChoiceContext();
        var combat = owner.Creature.CombatState!;
        var enemy = combat.Enemies.First();
        await CreatureCmd.GainMaxHp(enemy, 1000);
        foreach (var member in squad.Living)
            foreach (var power in member.Powers.ToArray()) await PowerCmd.Remove(power);
        await CreatureCmd.LoseBlock(context, enemy, enemy.Block, null);

        // Native potion, preview hooks and attacks: a rear actor's modifiers
        // must not be consumed by the front's attack or by repeated previews.
        var alyce = squad.GetActor("alyce");
        if (squad.Front == alyce) await squad.Swap(squad.GetActor("tora"));
        var giant = await PotionCmd.TryToProcure<GigantificationPotion>(owner);
        if (!giant.success || !giant.potion.IsValidTarget(alyce)) throw new Exception("Cannot target rear member with gigantification");
        await giant.potion.OnUseWrapper(context, alyce);
        await PowerCmd.Apply<VigorPower>(context, alyce, 2, alyce, null);
        var signature = combat.CreateCard<AlyceSignature>(owner);
        await CardPileCmd.Add(signature, PileType.Hand);
        for (int i = 0; i < 20; i++)
        {
            signature.DynamicVars.Damage.UpdateCardPreview(signature, CardPreviewMode.Normal, enemy, true);
            if (signature.DynamicVars.Damage.PreviewValue != 27 || alyce.GetPowerAmount<VigorPower>() != 2
                || alyce.GetPowerAmount<GigantificationPower>() != 1)
                throw new Exception($"Preview mismatch: damage={signature.DynamicVars.Damage.PreviewValue}, vigor={alyce.GetPowerAmount<VigorPower>()}, giant={alyce.GetPowerAmount<GigantificationPower>()}");
        }
        var strike = combat.CreateCard<SquadStrike>(owner);
        int before = enemy.CurrentHp;
        await CardCmd.AutoPlay(context, strike, enemy);
        if (before - enemy.CurrentHp != 6 || alyce.GetPowerAmount<VigorPower>() != 2
            || alyce.GetPowerAmount<GigantificationPower>() != 1)
            throw new Exception("Front attack stole rear actor's modifiers");
        before = enemy.CurrentHp;
        var oldFront = squad.Front;
        await CardCmd.AutoPlay(context, signature, enemy);
        if (before - enemy.CurrentHp != 27 || alyce.GetPowerAmount<VigorPower>() != 0
            || alyce.GetPowerAmount<GigantificationPower>() != 0 || squad.Front != oldFront)
            throw new Exception("Rear attack failed native modifier consumption");
        GD.Print("DOHNA_SMOKE_GIANT_VIGOR_PREVIEW_PASS damage=27 previews=20");

        // The potion's HP effect is member-local, its extra turn is player-wide.
        var ambergris = await PotionCmd.TryToProcure<Ambergris>(owner);
        if (!ambergris.success) throw new Exception("Cannot procure extra-turn fixture");
        await CreatureCmd.SetCurrentHp(alyce, 5);
        await ambergris.potion.OnUseWrapper(context, alyce);
        if (alyce.CurrentHp != 17 || alyce.GetPowerAmount<AmbergrisPower>() != 0
            || owner.Creature.GetPowerAmount<AmbergrisPower>() != 1)
            throw new Exception("Ambergris split member healing/player turn ownership incorrectly");
        foreach (var member in squad.Living)
        {
            await CreatureCmd.GainBlock(member, 8, ValueProp.Unpowered, null);
            await PowerCmd.Apply<DohnaDohna.Powers.SquadTemporaryStrength>(context, member, 1, member, null);
        }
        int turn = owner.PlayerCombatState!.TurnNumber;
        bool enemyTurnObserved = false;
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(owner, turn));
        for (int i = 0; i < 1800; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            enemyTurnObserved |= combat.CurrentSide == CombatSide.Enemy;
            if (owner.PlayerCombatState.TurnNumber > turn && owner.PlayerCombatState.Phase == PlayerTurnPhase.Play
                && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null) break;
        }
        if (owner.PlayerCombatState.TurnNumber != turn + 1 || enemyTurnObserved
            || squad.Living.Any(m => m.Block != 0 || m.GetPowerAmount<DohnaDohna.Powers.SquadTemporaryStrength>() != 0
                || m.GetPowerAmount<StrengthPower>() != 0))
            throw new Exception("Native extra turn did not clear all member block/temporary strength");
        GD.Print("DOHNA_SMOKE_EXTRA_TURN_PASS members=4 enemyTurns=0");

        // Native thorns kills the attacker during its own card. The remaining
        // signature effect must not run; its native discard and future substitution remain legal.
        var porno = squad.GetActor("porno");
        await squad.Swap(porno);
        await CreatureCmd.SetCurrentHp(porno, 1);
        await PowerCmd.Apply<ThornsPower>(context, enemy, 2, enemy, null);
        var dyingCard = combat.CreateCard<PornoSignature>(owner);
        await CardPileCmd.Add(dyingCard, PileType.Hand);
        await CardCmd.AutoPlay(context, dyingCard, enemy);
        if (porno.IsAlive || enemy.GetPowerAmount<WeakPower>() != 0 || dyingCard.Pile?.Type == PileType.Exhaust)
            throw new Exception("Thorns death continued signature effect or passively exhausted card");
        await PowerCmd.Remove<ThornsPower>(enemy);
        var generated = combat.CreateCard<PornoSignature>(owner);
        var copied = combat.CloneCard(dyingCard);
        foreach (var card in new[] { generated, copied })
        {
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
            int targetHp = enemy.CurrentHp;
            await CardCmd.AutoPlay(context, card, enemy);
            if (card.Pile?.Type == PileType.Exhaust || card is not SquadCardModel { IsFallback: true } || targetHp - enemy.CurrentHp != 6)
                throw new Exception("Generated/copied dead-role card did not substitute normally");
        }
        GD.Print("DOHNA_SMOKE_THORNS_GENERATED_DEAD_CARD_PASS");
        await FinishCombat(owner);
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
        SquadStore.State.Set(run, owner.NetId, saved);
        owner.Creature.SetMaxHpInternal(saved.Front!.MaxHp);
        owner.Creature.SetCurrentHpInternal(saved.Front.Hp);
    }

    private async Task VerifyAllRoleDeaths(Player owner, RunState run)
    {
        var saved = SquadStore.Get(owner).Copy();
        var preference = DeathPreference().Binding;
        bool beforePreference = preference.Read();
        try
        {
            foreach (bool cutInsEnabled in new[] { false, true })
            {
                preference.Write(cutInsEnabled);
                var female = new HashSet<string> { "alyce", "antena", "kikuchiyo", "medhico", "kirakira", "porno" };
                string[][] groups = [["kuma", "alyce", "antena", "tora"], ["tora", "kikuchiyo", "medhico", "joker"],
            ["joker", "zappa", "kirakira", "porno"], ["porno", "kuma", "alyce", "tora"]];
                var tested = new HashSet<string>();
                foreach (var group in groups)
                {
                    SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(group));
                    await EnterBattle(owner);
                    var squad = SquadCombatState.Get(owner);
                    var enemy = owner.Creature.CombatState!.Enemies.First();
                    foreach (var role in group.Take(3).Where(r => !tested.Contains(r)))
                    {
                        var actor = squad.GetActor(role);
                        await squad.Swap(actor);
                        await PowerCmd.Remove<BufferPower>(actor);
                        bool sawPoster = false;
                        var started = Time.GetTicksMsec();
                        var startedFrame = Engine.GetProcessFrames();
                        string cue = cutInsEnabled ? "dead" : "dead-no-cut-in";
                        MotionMarker(role, cue, "start");
                        AudioProbe.Mark(role + "-" + cue + "-begin");
                        var death = CreatureCmd.Damage(new BlockingPlayerChoiceContext(), actor, 100,
                            ValueProp.Unpowered | ValueProp.Unblockable, enemy);
                        while (!death.IsCompleted)
                        {
                            if (!sawPoster && GetTree().Root.GetChildrenRecursive<DeathCutIn>().Any())
                            {
                                sawPoster = true;
                                await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
                                await Screenshot("death-" + role + ".png");
                            }
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }
                        await death;
                        MotionMarker(role, cue, "end");
                        AudioProbe.Mark(role + "-" + cue + "-end");
                        if (actor.IsAlive || sawPoster != (cutInsEnabled && female.Contains(role)) || actor.GetCreatureNode()!.Visible
                            || (sawPoster && (System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_MOVIE") == "1"
                                ? Engine.GetProcessFrames() - startedFrame < 90
                                : Time.GetTicksMsec() - started < 1500)))
                            throw new Exception($"Death presentation mismatch for {role}: alive={actor.IsAlive}, poster={sawPoster}, "
                                + $"visible={actor.GetCreatureNode()!.Visible}, frames={Engine.GetProcessFrames() - startedFrame}, wallMs={Time.GetTicksMsec() - started}");
                        tested.Add(role);
                        GD.Print($"DOHNA_SMOKE_ROLE_DEATH_PASS {role} setting={cutInsEnabled} poster={sawPoster} elapsedMs={Time.GetTicksMsec() - started}");
                    }
                    await FinishCombat(owner);
                    await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
                }
                if (tested.Count != 10) throw new Exception("Not all role deaths were exercised");
            }
        }
        finally { preference.Write(beforePreference); }
        SquadStore.State.Set(run, owner.NetId, saved);
        owner.Creature.SetMaxHpInternal(saved.Front!.MaxHp);
        owner.Creature.SetCurrentHpInternal(saved.Front.Hp);
        GD.Print("DOHNA_SMOKE_TEN_ROLE_DEATHS_PASS females=6 males=4 settings=off,on");
    }
}
