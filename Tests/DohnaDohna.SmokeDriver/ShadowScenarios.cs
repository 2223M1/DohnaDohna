using System.Reflection;
using System.Text.Json;
using Godot;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyShadowPixels(Player owner)
    {
        await EnterBattle(owner);
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        var squad = SquadCombatState.Get(owner);
        var sprites = squad.Actors.SelectMany(actor => (List<Sprite2D>)typeof(RoleVisuals)
            .GetField("_shadows", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(actor.GetCreatureNode()!.Visuals)!).Where(s => s.Visible).ToArray();
        if (sprites.Length < 4) throw new Exception("Missing shadow poses");
        var initial = sprites.Select(s => s.ZIndex).ToArray();
        CombatManager.Instance.Pause();
        GetTree().Paused = true;
        try
        {
            using var original = await Capture("shadow-production.png");
            foreach (var s in sprites) s.Hide();
            using var absent = await Capture("shadow-hidden.png");
            using var noise = await Capture("shadow-hidden-repeat.png");
            // Reproduce the retired depth with the same current texture and pose.
            foreach (var s in sprites) { s.ZIndex = -100; s.Show(); }
            using var behind = await Capture("shadow-retired-depth.png");
            foreach (var s in sprites) { s.ZIndex = 0; s.Show(); }
            using var foreground = await Capture("shadow-depth-zero.png");
            var records = sprites.Select((s, i) =>
            {
                var bounds = GetViewport().GetStretchTransform() * s.GetGlobalTransformWithCanvas() * s.GetRect();
                var roi = new Rect2I((Vector2I)bounds.Position.Floor(), (Vector2I)bounds.Size.Ceil())
                    .Intersection(new Rect2I(0, 0, absent.GetWidth(), absent.GetHeight()));
                var baseline = Difference(original, absent, roi);
                var backgroundNoise = Difference(absent, noise, roi);
                var candidate = Difference(foreground, noise, roi);
                var retired = Difference(behind, absent, roi);
                if (baseline.pixels < 100 || retired.pixels != 0)
                    throw new Exception($"Production shadow visibility/depth regression: {baseline}, retired={retired}");
                if (candidate.pixels < 100 || candidate.darkening <= backgroundNoise.darkening + .5)
                    throw new Exception($"Shadow at native scene depth not visible: {roi}, {candidate}");
                return new { index = i, depth = initial[i], bounds = roi.ToString(), baseline, backgroundNoise, candidate, retired };
            }).ToArray();
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(
                System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "shadow-pixels.json"),
                JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
            GD.Print("DOHNA_SMOKE_SHADOW_PIXELS_DIAGNOSTIC " + JsonSerializer.Serialize(records,
                new JsonSerializerOptions { IncludeFields = true }));
        }
        finally
        {
            for (int i = 0; i < sprites.Length; i++) { sprites[i].ZIndex = initial[i]; sprites[i].Show(); }
            GetTree().Paused = false;
            CombatManager.Instance.Unpause();
        }
        // Same real-pixel proof for native blue motion trails. Negative Godot
        // Z escaped the character and put them beneath the opaque background.
        var kuma = (RoleVisuals)squad.GetActor("kuma").GetCreatureNode()!.Visuals;
        var action = kuma.Play("strike", null, squad.Cancellation.Token,
            [owner.Creature.CombatState!.Enemies.First(c => c.IsAlive)]);
        Node2D[] poses = [];
        while (!action.IsCompleted)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            poses = GetTree().Root.GetChildrenRecursive<RoleMotionEffects>().SelectMany(stage =>
                (List<(Node2D Pose, double Start)>)typeof(RoleMotionEffects)
                    .GetField("_previousPoses", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stage)!)
                .Select(p => p.Pose).ToArray();
            if (poses.Length >= 7) break;
        }
        if (poses.Length < 7) throw new Exception("No original previous-pose sequence captured");
        CombatManager.Instance.Pause();
        GetTree().Paused = true;
        try
        {
            using var visible = await Capture("trails-production.png");
            foreach (var pose in poses) pose.Hide();
            using var hidden = await Capture("trails-hidden.png");
            using var repeated = await Capture("trails-hidden-repeat.png");
            var depths = poses.Select(p => p.ZIndex).ToArray();
            foreach (var pose in poses) { pose.ZIndex = 0; pose.Show(); }
            using var raised = await Capture("trails-depth-zero.png");
            var roi = new Rect2I(150, 350, 600, 275);
            var diff = Difference(visible, hidden, roi);
            var noise = Difference(hidden, repeated, roi);
            var candidate = Difference(raised, repeated, roi);
            GD.Print($"DOHNA_SMOKE_TRAIL_PIXELS baseline={diff} noise={noise} candidate={candidate}");
            if (diff.darkening < noise.darkening + 100 || diff.pixels < 1000)
                throw new Exception("Original blue trails are not visibly rendered");
            for (int i = 0; i < poses.Length; i++) { poses[i].ZIndex = depths[i]; poses[i].Show(); }
        }
        finally { GetTree().Paused = false; CombatManager.Instance.Unpause(); }
        await action;
        // Stress the production draw order with a shadow crossing an opaque
        // native enemy torso. Only the test changes its position; live attack
        // depth ownership is active. Reproduce the old raised-shadow depth as
        // a positive control, so an empty/hidden shadow cannot pass.
        var enemy = owner.Creature.CombatState!.Enemies.Where(c => c.IsAlive)
            .OrderByDescending(c => c.GetCreatureNode()!.Visuals.Bounds.Size.X * c.GetCreatureNode()!.Visuals.Bounds.Size.Y).First();
        using var overlapCancellation = CancellationTokenSource.CreateLinkedTokenSource(squad.Cancellation.Token);
        var overlapAction = kuma.Play("strike", null, overlapCancellation.Token, [enemy]);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CombatManager.Instance.Pause(); GetTree().Paused = true;
        var ground = ((List<Sprite2D>)typeof(RoleVisuals).GetField("_shadows", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kuma)!).First(s => s.Visible);
        var beforeGround = ground.Transform;
        try
        {
            var art = kuma.GetNode<Node2D>("%Visuals/OriginalMotion");
            if (art.ZIndex != 1 || ((Node2D)ground.GetParent()).ZIndex != 0 || ground.ZIndex != 0)
                throw new Exception("Attack raised ground shadows with the body");
            var center = enemy.GetCreatureNode()!.Visuals.Bounds.GetGlobalRect().GetCenter();
            ground.GlobalPosition += center - (ground.GlobalTransform * ground.GetRect()).GetCenter();
            using var under = await Capture("shadow-enemy-under.png");
            ground.Hide();
            using var hidden = await Capture("shadow-enemy-hidden.png");
            ground.ZIndex = 1; ground.Show();
            using var above = await Capture("shadow-enemy-retired-above.png");
            var point = GetViewport().GetStretchTransform() * center;
            var roi = new Rect2I((Vector2I)point - new Vector2I(12, 2), new Vector2I(24, 4));
            var fixedDiff = Difference(under, hidden, roi);
            var retiredDiff = Difference(above, hidden, roi);
            if (fixedDiff.pixels != 0 || retiredDiff.pixels < 12 || retiredDiff.darkening < .5)
                throw new Exception($"Shadow enemy occlusion proof failed: fixed={fixedDiff}, retired={retiredDiff}");
            GD.Print($"DOHNA_SMOKE_SHADOW_ENEMY_OCCLUSION_PASS fixed={fixedDiff} retired={retiredDiff}");
        }
        finally
        {
            ground.Transform = beforeGround; ground.ZIndex = 0; ground.Show();
            overlapCancellation.Cancel();
            GetTree().Paused = false; CombatManager.Instance.Unpause();
        }
        try { await overlapAction; } catch (OperationCanceledException) when (overlapCancellation.IsCancellationRequested) { }
        if (System.Environment.GetEnvironmentVariable("DOHNA_CAPTURE_MOVIE") != "1")
        {
            // Actual realtime host intervals, not MovieWriter throughput and
            // not a GPU-only benchmark. Warm after texture loading/readbacks.
            for (int i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var intervals = new List<double>();
            long previous = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < 600; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                intervals.Add(System.Diagnostics.Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds);
                previous = now;
            }
            intervals.Sort();
            string performance = JsonSerializer.Serialize(new { samples = intervals.Count,
                medianMs = intervals[300], p95Ms = intervals[570], maxMs = intervals[^1],
                measurement = "four production actors with per-frame shadows; real Stopwatch process intervals; no movie or screenshot in measurement; NinjaSlayer co-loaded" });
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "shadow-performance.json"), performance);
            GD.Print("DOHNA_SMOKE_SHADOW_REALTIME " + performance);
        }
        await FinishCombat(owner);

        async Task<Image> Capture(string name)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, name)) != Error.Ok)
                throw new Exception("Shadow screenshot failed");
            return image;
        }
        static (int pixels, double darkening) Difference(Image on, Image off, Rect2I roi)
        {
            int changed = 0;
            double darkening = 0;
            for (int y = roi.Position.Y; y < roi.End.Y; y++)
                for (int x = roi.Position.X; x < roi.End.X; x++)
                {
                    var a = on.GetPixel(x, y);
                    var b = off.GetPixel(x, y);
                    double d = (b.R + b.G + b.B - a.R - a.G - a.B) / 3;
                    if (d > 2.0 / 255) { changed++; darkening += d; }
                }
            return (changed, darkening);
        }
    }
}
