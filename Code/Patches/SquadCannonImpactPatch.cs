using Godot;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Patching.Models;

namespace DohnaDohna.Code.Patches;

// Yamoto's missile palette on the host's exact particle rig. No NinjaSlayer
// asset/assembly dependency, duplicate spark or global material mutation.
internal sealed class SquadCannonImpactPatch : IPatchMethod
{
    internal const string HitSound = "event:/sfx/enemy/enemy_attacks/living_fog/living_fog_explode";
    public static string PatchId => "dohnadohna_small_cannon_spark";
    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NHitSparkVfx), nameof(NHitSparkVfx.Create), [typeof(Creature), typeof(bool)])];

    public static void Postfix(Creature target, NHitSparkVfx? __result)
    {
        if (__result == null || SquadPlaySelection.Current is not { } play
            || play.ContactCommand.Value?.HitSfx != HitSound || target.Side == play.Actor.Side) return;
        Tint("vfx_common_specks_bright", new(.909804f, .0899267f, .12156871f),
            new(.909804f, .361765f, .687255f), new(1.4818365f, 1.1837975f, 1.5092875f));
        Tint("vfx_common_specks", new(.909804f, .2284317f, .437255f),
            new(.909804f, .611765f, .937255f, .5019608f), new(.909804f, .611765f, .937255f));
        Tint("vfx_common_glow", new(.909804f, .17579887f, .3912194f));
        Tint("vfx_common_outward_streaks", Colors.White,
            new(.909804f, .04705912f, .28627462f), new(.909804f, .611765f, .937255f),
            new(.909804f, .2563486f, .28627465f));

        void Tint(string path, Color modulate, Color? from = null, Color? to = null, Color? color = null)
        {
            var particles = __result.GetNode<GpuParticles2D>(path);
            particles.SelfModulate = modulate;
            var material = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate();
            particles.ProcessMaterial = material;
            if (color is { } tint) material.Color = tint;
            if (from is { } first && to is { } last)
                material.ColorInitialRamp = new GradientTexture1D
                {
                    UseHdr = true,
                    Gradient = new Gradient { Colors = [first, last], Offsets = [0, 1] }
                };
        }
    }
}
