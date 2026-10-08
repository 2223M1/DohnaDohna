using System.Reflection;
using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using STS2RitsuLib.Audio;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    // Test-only observation of the production action's owned handles. Do not
    // substitute another motion player or expose diagnostics in the runtime API.
    private static IAudioHandle[] ActionSounds(RoleVisuals visuals) =>
        ((List<IAudioHandle>)typeof(RoleVisuals).GetField("_sounds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(visuals)!).ToArray();

    private async Task VerifyVisualLifecycle(Player owner)
    {
        var gate = typeof(RoleVisuals).GetMethod("CanReplayHurtVoice", BindingFlags.Static | BindingFlags.NonPublic)!;
        if ((bool)gate.Invoke(null, [1299UL, (ulong?)1000UL])! || (bool)gate.Invoke(null, [1300UL, (ulong?)1000UL])!
            || !(bool)gate.Invoke(null, [1301UL, (ulong?)1000UL])!) throw new Exception("300 ms reaction voice gate");
        await EnterBattle(owner);
        var squad = SquadCombatState.Get(owner);
        var actor = squad.GetActor("alyce");
        var node = actor.GetCreatureNode()!;
        var visuals = (RoleVisuals)node.Visuals;
        var layers = visuals.GetNode<Node2D>("%Visuals");
        var art = layers.GetNode<Node2D>("OriginalMotion");
        var cutField = typeof(RoleVisuals).GetField("_cut", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var offsetField = typeof(RoleVisuals).GetField("_returnOffset", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalScale = visuals.Scale;
        var originalMode = SaveManager.Instance.PrefsSave.FastMode;
        visuals.Scale *= 1.2f;
        squad.Layout();
        var external = visuals.Transform;
        var root = node.GetTransform();
        var layerTransform = layers.Transform;
        int depth = art.ZIndex;
        var targets = owner.Creature.CombatState!.Enemies.Where(c => c.IsAlive).ToArray();
        int hits = 0;
        Task Hit() { hits++; return Task.CompletedTask; }
        async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Clean()
        {
            await Frame(); await Frame();
            if (visuals.IsPlaying || (Vector2)offsetField.GetValue(visuals)! != Vector2.Zero
                || visuals.Transform != external || node.GetTransform() != root || layers.Transform != layerTransform || art.ZIndex != depth
                || GetTree().Root.GetChildrenRecursive<RoleMotionEffects>().Any()
                || GetTree().Root.GetChildrenRecursive<CanvasLayer>().Any(n => n.Name == "DohnaMotionCamera")
                || GetTree().Root.GetChildrenRecursive<ColorRect>().Any(n => n.Name == "DohnaActionLighting"))
                throw new Exception("Owned presentation cleanup/external scale failed");
        }
        double Clock() { var c = cutField.GetValue(visuals)!; return (double)c.GetType().GetField("Time")!.GetValue(c)!; }
        try
        {
            foreach (var mode in new[] { FastModeType.Normal, FastModeType.Fast })
            {
                SaveManager.Instance.PrefsSave.FastMode = mode;
                hits = 0;
                await visuals.PlayHurt(squad.Cancellation.Token); // Independent real voice for FMOD pause verification.
                var action = visuals.Play("special", Hit, squad.Cancellation.Token, targets, cinematic: true);
                await Frame(); await Frame();
                CombatManager.Instance.Pause();
                await Frame(); await Frame();
                if (action.IsCompleted || hits != 0) throw new Exception("Pause fixture missed anticipation");
                double stopped = Clock();
                var sounds = ActionSounds(visuals).Where(h => h.IsValid && h.RawInstance!.Call("get_playback_state").AsInt32() != 2).ToArray();
                if (sounds.Length == 0 || sounds.Any(h => !h.RawInstance!.Call("get_paused").AsBool()))
                    throw new Exception("FMOD did not pause");
                await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
                if (Clock() != stopped || hits != 0) throw new Exception("Paused anticipation advanced");
                CombatManager.Instance.Unpause();
                await action;
                while (visuals.IsPlaying) await Frame();
                await Clean();
                if (hits != 1) throw new Exception("Paused attack damage count");
                GD.Print("DOHNA_SMOKE_ACTION_PAUSE_MODE_PASS mode=" + mode);
            }
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;
            hits = 0;
            await visuals.Play("special", Hit, squad.Cancellation.Token, targets, cinematic: true);
            if (hits != 1) throw new Exception("Instant damage count");
            await Clean();
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;

            using (var cancel = new CancellationTokenSource())
            {
                hits = 0;
                var priorSounds = ActionSounds(visuals).ToHashSet();
                var action = visuals.Play("special", Hit, cancel.Token, targets, cinematic: true);
                await Frame();
                var sounds = ActionSounds(visuals).Where(s => !priorSounds.Contains(s)).ToArray();
                cancel.Cancel();
                try { await action; throw new Exception("Canceled lead completed"); }
                catch (OperationCanceledException) { }
                await Clean();
                if (hits != 0 || sounds.Any(h => h.IsValid && !h.IsReleased)) throw new Exception("Canceled lead hit or kept sounds");
            }
            using (var cancel = new CancellationTokenSource())
            {
                hits = 0;
                await visuals.Play("special", Hit, cancel.Token, targets);
                if (!visuals.IsPlaying || hits != 1) throw new Exception("No owned tail after hit");
                CombatManager.Instance.Pause();
                await Frame(); await Frame();
                double stopped = Clock();
                await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
                if (Clock() != stopped) throw new Exception("Paused tail advanced");
                cancel.Cancel();
                CombatManager.Instance.Unpause();
                await Clean();
                if (hits != 1) throw new Exception("Tail cancel replayed damage");
            }
            hits = 0;
            var interrupted = visuals.Play("special", Hit, squad.Cancellation.Token, targets);
            await Frame();
            await visuals.PlayHurt(squad.Cancellation.Token);
            await interrupted;
            while (visuals.IsPlaying) await Frame();
            await Clean();
            if (hits != 1) throw new Exception("Reaction discarded or duplicated attack");
            GD.Print("DOHNA_SMOKE_ACTION_REACTION_PASS");

            hits = 0;
            var cast = visuals.Play("cast", null, squad.Cancellation.Token);
            await Frame();
            var nextAttack = visuals.Play("special", Hit, squad.Cancellation.Token, targets);
            await cast;
            await nextAttack;
            if (hits != 1 || !visuals.IsPlaying || cutField.GetValue(visuals) == null)
                throw new Exception("Old cast cleanup erased a newer attack");
            while (visuals.IsPlaying) await Frame();
            await Clean();
            GD.Print("DOHNA_SMOKE_CAST_ATTACK_HANDOFF_PASS");

            hits = 0;
            var exiting = visuals.Play("special", Hit, squad.Cancellation.Token, targets, cinematic: true);
            await FinishCombat(owner);
            try { await exiting; throw new Exception("Exit did not cancel lead"); }
            catch (OperationCanceledException) when (squad.Cancellation.IsCancellationRequested) { }
            await Clean();
            if (hits != 0) throw new Exception("Exited lead dealt damage");
            GD.Print("DOHNA_SMOKE_ACTION_EXIT_PASS");
        }
        finally
        {
            CombatManager.Instance.Unpause();
            SaveManager.Instance.PrefsSave.FastMode = originalMode;
            if (GodotObject.IsInstanceValid(visuals)) visuals.Scale = originalScale;
        }
    }
}
