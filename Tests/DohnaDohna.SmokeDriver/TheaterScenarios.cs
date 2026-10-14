using System.Diagnostics;
using System.Text.Json;
using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task RecordTheater(Player owner, RunState run)
    {
        string directory = System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_DIR")
            ?? throw new InvalidOperationException("Theater must capture process audio in the same run.");
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        SaveManager.Instance.PrefsSave.KeyboardMode = true;
        SaveManager.Instance.MarkFtueAsComplete("can_play_cards_ftue");
        await SetCatalogRoster(owner, run, SquadRunState.Create(["alyce", "kikuchiyo", "antena", "kuma"]));
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var enemies = combat.Enemies.ToArray();
        // Fixture setup ends before recording: production cards, unmodified
        // effects/energy costs, real targets. Never overwrite HP to force a kill.
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].SetMaxHpInternal(i == 1 ? 55 : 35);
            enemies[i].SetCurrentHpInternal(enemies[i].MaxHp);
        }
        await PlayerCmd.GainEnergy(40, owner);
        await CardPileCmd.Add(owner.PlayerCombatState!.Hand.Cards.ToArray(), PileType.Discard);
        CardModel[] opening = [combat.CreateCard<SquadStrike>(owner), combat.CreateCard<SquadSpecial>(owner),
            combat.CreateCard<SquadDefragment>(owner), combat.CreateCard<SquadChill>(owner),
            combat.CreateCard<SquadBallLightning>(owner), combat.CreateCard<SquadBeamCell>(owner),
            combat.CreateCard<SquadPommelStrike>(owner), combat.CreateCard<SquadSwap>(owner),
            combat.CreateCard<SquadSpecial>(owner)];
        foreach (var card in opening) await CardPileCmd.Add(card, PileType.Hand);
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        var recording = new TheaterRecording(GetViewport(), directory);
        var timeline = new List<object>();
        var actors = new HashSet<string>();
        bool finisher = false;
        long start = Stopwatch.GetTimestamp();
        object Snapshot() => new
        {
            energy = owner.PlayerCombatState?.Energy,
            queue = squad.Order.ToArray(),
            allies = squad.Actors.Select(c => new { role = squad.RoleOf(c), hp = c.CurrentHp, block = c.Block }).ToArray(),
            enemies = enemies.Select(c => new { id = c.Monster!.Id.ToString(), hp = c.CurrentHp, block = c.Block }).ToArray()
        };
        async Task Hold(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        Creature Target() => enemies.First(e => e.IsAlive);

        async Task Play(CardModel card, Creature? target, double prepare = .20, bool cancel = false)
        {
            if (card.Pile?.Type != PileType.Hand)
            {
                // Same convention as NinjaSlayer's theater card cue: stage a
                // real hand card, then use the native input/action queue.
                if (owner.PlayerCombatState!.Hand.Cards.Count >= 9)
                    await CardPileCmd.Add(owner.PlayerCombatState.Hand.Cards.Last(), PileType.Discard);
                await CardPileCmd.Add(card, PileType.Hand);
            }
            string actor = squad.RoleOf(squad.Actions.ActorFor(card));
            var before = Snapshot();
            double from = Stopwatch.GetElapsedTime(start).TotalSeconds;
            var holder = NGame.Instance!.GetChildrenRecursive<NHandCardHolder>().Single(h => h.CardModel == card);
            await PreparationKey(Key.Up);
            holder.GrabFocus();
            await PreparationKey(Key.Space);
            await Hold(prepare);
            if (cancel)
            {
                await PreparationKey(Key.Escape);
                if (card.Pile?.Type != PileType.Hand) throw new Exception("Theater canceled card was spent.");
            }
            else
            {
                target?.GetCreatureNode()!.Hitbox.GrabFocus();
                await PreparationKey(Key.Space);
                for (int frame = 0; frame < 900; frame++)
                {
                    finisher |= RoleFinisher.Current != null;
                    if (card.Pile?.Type != PileType.Hand && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null) break;
                    if (frame == 899) throw new Exception("Theater native card did not complete: " + card.Id);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                actors.Add(actor);
            }
            timeline.Add(new { card = card.Id.ToString(), actor, cancel, start = from,
                end = Stopwatch.GetElapsedTime(start).TotalSeconds, before, after = Snapshot() });
            // No visual-tail wait. The next real card may begin while this role
            // or another is still recovering; native input remains authoritative.
        }

        await recording.Start();
        start = recording.StartTimestamp;
        try
        {
            await Hold(.7);
            var slime = enemies.Single(e => e.Monster is MegaCrit.Sts2.Core.Models.Monsters.TwigSlimeS).Monster!;
            var beforeHit = Snapshot();
            double hitStart = Stopwatch.GetElapsedTime(start).TotalSeconds;
            await (Task)HarmonyLib.AccessTools.Method(slime.GetType(), "TackleMove")
                .Invoke(slime, [new Creature[] { owner.Creature }])!;
            timeline.Add(new { move = "TwigSlimeS.TackleMove", start = hitStart,
                end = Stopwatch.GetElapsedTime(start).TotalSeconds, before = beforeHit, after = Snapshot() });
            await Hold(.7);
            await Play(opening[0], Target(), 1.1, cancel: true);
            await Hold(.35);
            await Play(opening[0], Target(), .35);
            await Play(opening[1], Target());
            await Play(opening[2], null, 1.0);
            await Play(opening[3], null, .20);
            await Play(opening[4], Target());
            await Play(opening[5], Target());
            await Play(opening[6], Target());
            await Play(opening[7], squad.GetActor("alyce"), .30);
            await Play(opening[8], Target(), .45);
            // Actual native recipient feedback, not a theater-only shield cue.
            await Play(combat.CreateCard<SquadRally>(owner), null, .55);
            await Play(combat.CreateCard<SquadBallLightning>(owner), Target(), .05);
            await Play(combat.CreateCard<SquadBallLightning>(owner), Target(), .05);
            await Play(combat.CreateCard<SquadSweepingBeam>(owner), null, .25);
            // Finish through genuine damage, alternating back-line shooting and
            // front-line melee. Eligibility, camera and death are production logic.
            for (int i = 0; i < 24 && enemies.Any(e => e.IsAlive); i++)
            {
                CardModel card = (i % 3) switch
                {
                    0 => combat.CreateCard<SquadPommelStrike>(owner),
                    1 => combat.CreateCard<SquadBeamCell>(owner),
                    _ => combat.CreateCard<SquadBallLightning>(owner)
                };
                await Play(card, Target(), .08);
            }
            if (enemies.Any(e => e.IsAlive) || actors.Count != 4 || !finisher)
                throw new Exception($"Theater coverage: living={enemies.Count(e => e.IsAlive)} actors={actors.Count} finisher={finisher}");
            await Hold(2);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "theater-timeline.json"), JsonSerializer.Serialize(new
            {
                roster = new[] { "alyce", "kikuchiyo", "antena", "kuma" },
                fixture = "staged native hand cards, initial 40 extra energy and 35/55/35 enemy HP; no post-start HP overwrites",
                input = "native keyboard/controller Godot events; not physical input",
                speed = SaveManager.Instance.PrefsSave.FastMode.ToString(), finisher, final = Snapshot(), cues = timeline
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await recording.Stop(); }
        await CombatManager.Instance.CheckWinCondition();
    }
}
