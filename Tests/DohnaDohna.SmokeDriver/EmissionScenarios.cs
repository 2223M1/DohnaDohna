using Godot;
using HarmonyLib;
using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private static SquadCardModel? _emissionCard;
    private static SaucerShotStyle _emissionStyle;

    // Test-only authoring override on one staged card. Exercise the production
    // selector, flight, native command and actual damage for every editor style;
    // never add a test card model or an override field to the shipped package.
    private static void EmissionCardStyle(SquadCardModel __instance, ref SaucerShotStyle __result)
    {
        if (ReferenceEquals(__instance, _emissionCard)) __result = _emissionStyle;
    }

    private async Task VerifyEmissions(Player owner, RunState run)
    {
        SaveManager.Instance.PrefsSave.FastMode = Entry.TestSpeed;
        await SetCatalogRoster(owner, run, SquadRunState.Create(["kuma", "alyce", "kikuchiyo", "antena"]));
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var combat = owner.Creature.CombatState!;
        var enemies = combat.HittableEnemies.ToArray();
        foreach (var enemy in enemies) await CreatureCmd.GainMaxHp(enemy, 1000);
        var actor = squad.GetActor("antena");
        var visual = (RoleVisuals)actor.GetCreatureNode()!.Visuals;
        var context = new BlockingPlayerChoiceContext();
        async Task Hold(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        var recording = new TheaterRecording(GetViewport(), System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_DIR")!);
        await recording.Start();
        try
        {
        using (var texture = GD.Load<Texture2D>("res://images/vfx/sweeping_beam/sweeping_beam_flipbook_quick.png").GetImage())
            texture.SavePng(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "sweep-native-texture.png"));
        await Hold(2); // Let the native turn banner leave the recorded firing lane.
        var stylePatch = new Harmony("DohnaDohna.SmokeDriver.EmissionStyles");
        var styleGetter = AccessTools.PropertyGetter(typeof(SquadCardModel), nameof(SquadCardModel.SaucerShot));
        stylePatch.Patch(styleGetter, postfix: new HarmonyMethod(typeof(LoadProbe), nameof(EmissionCardStyle)));
        try
        {
            foreach (var style in Enum.GetValues<SaucerShotStyle>().Where(s => s != SaucerShotStyle.Automatic))
            {
              foreach (var victim in SaucerShot.IsBeam(style) ? enemies : [enemies[0]])
              {
                string label = "emission-" + style + "-" + Array.IndexOf(enemies, victim);
                MotionMarker("antena", label, "start");
                _emissionStyle = style;
                _emissionCard = combat.CreateCard<SquadStrike>(owner);
                if (_emissionCard.SaucerShot != style) throw new Exception("Test style override not active");
                Vector2 hover = visual.SaucerMuzzle!.Value;
                decimal hp = victim.CurrentHp;
                var otherHp = enemies.Where(e => e != victim).ToDictionary(e => e, e => e.CurrentHp);
                bool flight = false, contact = false;
                Vector2? firingOrigin = null;
                Vector2? firingFixture = null;
                var fixtureField = typeof(RoleVisuals).GetField("_saucerPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                Vector2 hoverFixture = (Vector2)fixtureField.GetValue(visual)!;
                var command = CardCmd.AutoPlay(context, _emissionCard, victim);
                while (!command.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var shot = NCombatRoom.Instance!.CombatVfxContainer.GetChildren().OfType<SaucerShot>()
                        .FirstOrDefault(s => ReferenceEquals(s.Play?.Card, _emissionCard));
                    // Native screen shake moves the whole room. Only the
                    // saucer's own local return must wait for the ray tail.
                    if (shot is { Attached: true } && firingFixture is { } held
                        && ((Vector2)fixtureField.GetValue(visual)!).DistanceTo(held) > .02f)
                        throw new Exception($"Saucer returned while attached: fixture={held}->{fixtureField.GetValue(visual)} global={firingOrigin}->{visual.SaucerMuzzle}");
                    if (!flight && shot is { Pending: true })
                    {
                        if (SaucerShot.IsBeam(style) && EmissionArt(shot).Visible) throw new Exception("Beam crossed before real Damage");
                        await Screenshot(label + "-flight.png"); flight = true;
                    }
                    if (!contact && victim.CurrentHp < hp)
                    {
                        var muzzle = visual.SaucerMuzzle!.Value;
                        float localTravel = ((Vector2)fixtureField.GetValue(visual)!).DistanceTo(hoverFixture);
                        if (style == SaucerShotStyle.SmallCannon ? localTravel > .02f : localTravel < 30)
                            throw new Exception($"Unexpected saucer travel: {style} distance={localTravel}");
                        if (shot == null) throw new Exception("No emission at real Damage");
                        if (SaucerShot.IsBeam(style)) VerifyEmissionRay(shot, style, victim, otherHp.Keys);
                        firingOrigin = muzzle;
                        firingFixture = (Vector2)fixtureField.GetValue(visual)!;
                        if (otherHp.Any(p => p.Key.CurrentHp != p.Value)) throw new Exception("Single ray damaged an uninvolved foe");
                        GD.Print($"DOHNA_EMISSION_NATIVE_STYLE {label} hover={hover} contact={muzzle} hp={hp}->{victim.CurrentHp}");
                        await Screenshot(label + ".png"); contact = true;
                    }
                }
                await command;
                if (!flight || !contact) throw new Exception("Style lacked real flight/damage: " + style);
                await Hold(1.2);
                if (NCombatRoom.Instance!.CombatVfxContainer.GetChildren().OfType<SaucerShot>()
                    .Any(s => ReferenceEquals(s.Play?.Card, _emissionCard))) throw new Exception("Emission not released: " + style);
                MotionMarker("antena", label, "end");
              }
            }
        }
        finally { _emissionCard = null; stylePatch.UnpatchAll(stylePatch.Id); }
        // Native random targets are deliberately unknown during windup.
        // The local visual must not fire a fake AOE before the real selection.
        var deferred = SaucerShot.Create(visual, enemies, .2, SaucerShotStyle.SmallCannon, false, deferredTarget: true);
        var artField = typeof(SaucerShot).GetField("_art", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await Hold(.1);
        if (((Node2D)artField.GetValue(deferred)!).Visible) throw new Exception("Random emission revealed speculative targets");
        deferred.Contact([enemies[0]]);
        var targetField = typeof(SaucerShot).GetField("_targets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var resolved = (MegaCrit.Sts2.Core.Nodes.Combat.NCreature[])targetField.GetValue(deferred)!;
        if (resolved.Length != 1 || resolved[0].Entity != enemies[0] || !((Node2D)artField.GetValue(deferred)!).Visible)
            throw new Exception("Random emission did not latch only the real target");
        await Hold(.7);
        if (GodotObject.IsInstanceValid(deferred)) throw new Exception("Deferred emission not released");
        GD.Print("DOHNA_SMOKE_DEFERRED_EMISSION_PASS");
        MotionMarker("antena", "native-card-chain", "start");
        foreach (var card in new MegaCrit.Sts2.Core.Models.CardModel[]
            { combat.CreateCard<SquadStrike>(owner), combat.CreateCard<SquadBallLightning>(owner),
              combat.CreateCard<SquadSweepingBeam>(owner), combat.CreateCard<SquadDefragment>(owner), combat.CreateCard<SquadChill>(owner) })
        {
            decimal before = enemies.Sum(e => e.CurrentHp);
            await CardCmd.AutoPlay(context, card, card.Type == CardType.Attack && card.TargetType != TargetType.AllEnemies ? enemies[0] : null);
            if (card.Type == CardType.Attack && enemies.Sum(e => e.CurrentHp) >= before) throw new Exception("Emission card did not damage: " + card.Id);
        }
        await Hold(1.5);
        MotionMarker("antena", "native-card-chain", "end");
        MotionMarker("kikuchiyo", "melee-insertion", "start");
        var melee = combat.CreateCard<SquadPommelStrike>(owner);
        await CardCmd.AutoPlay(context, melee, enemies[0]);
        if (squad.Front != squad.GetActor("kikuchiyo")) throw new Exception("Melee insertion failed");
        await Hold(1.2);
        MotionMarker("kikuchiyo", "melee-insertion", "end");
        MotionMarker("antena", "swap-jump", "start");
        await CardCmd.AutoPlay(context, combat.CreateCard<SquadSwap>(owner), actor);
        await Hold(.8);
        if (squad.Front != actor) throw new Exception("Swap failed");
        MotionMarker("antena", "swap-jump", "end");
        await VerifyFastContacts(owner);
        await FinishCombat(owner);
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        actor = squad.GetActor("antena");
        if (squad.Front != actor) await squad.Swap(actor);
        await Hold(.5);
        combat = owner.Creature.CombatState!;
        var last = combat.HittableEnemies.First();
        await CreatureCmd.Kill(combat.HittableEnemies.Where(e => e != last).ToArray(), true);
        last.SetCurrentHpInternal(1); // Final fixture preparation, before the real card.
        MotionMarker("antena", "b-mask-finisher", "start");
        var action = CardCmd.AutoPlay(context, combat.CreateCard<SquadStrike>(owner), last);
        bool captured = false;
        while (!action.IsCompleted)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!captured && RoleFinisher.Current is { Holding: true })
            { await Hold(.08); await Screenshot("emission-b-mask-finisher.png"); captured = true; }
        }
        await action;
        if (!captured || last.IsAlive) throw new Exception("Real Antena finisher missing");
        await Hold(1.5);
        MotionMarker("antena", "b-mask-finisher", "end");
        await SetCatalogRoster(owner, run, SquadRunState.Create(["kuma", "alyce", "antena", "zappa"]));
        await EnterBattle(owner);
        squad = SquadCombatState.Get(owner);
        combat = owner.Creature.CombatState!;
        last = combat.HittableEnemies.First();
        await CreatureCmd.Kill(combat.HittableEnemies.Where(e => e != last).ToArray(), true);
        last.SetCurrentHpInternal(1);
        var lastNode = last.GetCreatureNode()!;
        await Hold(2);
        MotionMarker("zappa", "melee-finisher-return", "start");
        var zappa = (RoleVisuals)squad.GetActor("zappa").GetCreatureNode()!.Visuals;
        var finisher = CardCmd.AutoPlay(context, combat.CreateCard<SquadSpecial>(owner), last);
        captured = false;
        while (!finisher.IsCompleted || zappa.IsPlaying || RoleFinisher.Current != null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!captured && RoleFinisher.Current is { Holding: true })
            {
                GD.Print($"DOHNA_FINISHER_REACH role=zappa bounds={zappa.PoseBounds()} offset={zappa.FinisherOffset} target={lastNode.GetBottomOfHitbox()}");
                await Hold(.08); await Screenshot("emission-melee-finisher.png"); captured = true;
            }
        }
        await finisher;
        if (!captured || last.IsAlive) throw new Exception("Real melee finisher missing");
        MotionMarker("zappa", "melee-finisher-return", "end");
        }
        finally { await recording.Stop(); }
    }

    private static Node2D EmissionArt(SaucerShot shot) => (Node2D)typeof(SaucerShot)
        .GetField("_art", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(shot)!;

    private static void VerifyEmissionRay(SaucerShot shot, SaucerShotStyle style, Creature victim, IEnumerable<Creature> others)
    {
        var art = EmissionArt(shot);
        if (!art.Visible || shot.Pending) throw new Exception("Contact beam is hidden");
        Vector2 end;
        switch (style)
        {
            case SaucerShotStyle.Hyperbeam:
                var line = art.GetNode<Line2D>("vfx_hyperbeam_laser_line");
                if (line.Points.Length != 7) throw new Exception("Lost native Hyperbeam profile");
                end = line.ToGlobal(line.Points[^1]); break;
            case SaucerShotStyle.Laser:
                end = art.GetNode<Node2D>("TargetingBone").GlobalPosition; break;
            case SaucerShotStyle.PriestBeam:
                var beam = art.GetNode<Sprite2D>("Beam");
                end = beam.ToGlobal(new Vector2(-beam.Texture.GetWidth(), 0)); break;
            case SaucerShotStyle.SweepingBeam:
                var rig = art.GetChildren().OfType<Node2D>().Single(n => n.GetChildren().OfType<Sprite2D>().Any());
                end = rig.ToGlobal(new Vector2(711.2f, 0)); break;
            default: throw new Exception("Not a beam");
        }
        var origin = shot.GlobalPosition;
        var target = victim.GetCreatureNode()!;
        float beyond = end.DistanceTo(origin) - target.VfxSpawnPosition.DistanceTo(origin);
        if (beyond <= target.Visuals.Bounds.GetGlobalRect().Size.Length() * .5f + 100)
            throw new Exception($"Beam stops at victim: {style} beyond={beyond}");
        // Inspect the actual displayed rig, not the lane selector's return.
        // Sample an expanded footprint to include the bright beam core, not
        // just an infinitesimally thin line that could miss a nearby body.
        foreach (var other in others)
        {
            Rect2 bounds = other.GetCreatureNode()!.Visuals.Bounds.GetGlobalRect().Grow(55);
            int samples = (int)Math.Ceiling(origin.DistanceTo(end) / 4);
            for (int i = 0; i <= samples; i++)
                if (bounds.HasPoint(origin.Lerp(end, (float)i / samples)))
                    throw new Exception($"Single {style} visually crosses untouched {other.Monster!.Id}: {origin}->{end}, bounds={bounds}");
        }
        if (style == SaucerShotStyle.Hyperbeam && origin.Y >= target.VfxSpawnPosition.Y - 100)
            throw new Exception("Single Hyperbeam did not take overhead lane");
        GD.Print($"DOHNA_EMISSION_RAY_PASS style={style} origin={origin} end={end} victim={target.VfxSpawnPosition} beyond={beyond}");
    }
}
