using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Patches;
using HarmonyLib;
using STS2RitsuLib.Patching.Core;

namespace DohnaDohna.Scripts;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "DohnaDohna";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    public static void Initialize()
    {
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());
        RitsuLibFramework.EnsureGodotScriptsRegistered(Assembly.GetExecutingAssembly(), Logger);
        SquadStore.Register();
        DohnaDohna.Relics.SquadRoleRelic.RegisterStarters();
        DohnaDohna.Code.Visuals.PresentationSettings.Register();
        DohnaDohna.Powers.SquadTemporaryStrength.RegisterAssets();
        DohnaDohna.Code.Visuals.RoleVisuals.RegisterFactory();
        DohnaDohna.Code.Visuals.SquadAudio.Register();
        var patcher = RitsuLibFramework.CreatePatcher(ModId, "Squad");
        patcher.RegisterPatch<SquadSelectPatch>();
        patcher.RegisterPatch<SquadSelectDefaultFocusPatch>();
        patcher.RegisterPatch<SquadEmbarkPatch>();
        patcher.RegisterPatch<SquadSelectBackPatch>();
        patcher.RegisterPatch<SquadSelectClosePatch>();
        patcher.RegisterPatch<SquadStartPatch>();
        patcher.RegisterPatch<SquadFormationPatch>();
        patcher.RegisterPatch<SquadFormationChangedPatch>();
        patcher.RegisterPatch<SquadDamageTargetPatch>();
        patcher.RegisterPatch<SquadEnemyAttackTargetsPatch>();
        patcher.RegisterPatch<SquadNativeVfxTargetPatch>();
        patcher.RegisterPatch<SquadSlimeSpitTargetPatch>();
        patcher.RegisterPatch<SquadCardAttackerPatch>();
        patcher.RegisterPatch<SquadBlockReceiverPatch>();
        patcher.RegisterPatch<SquadTurnParticipantsPatch>();
        patcher.RegisterPatch<SquadGainBlockPatch>();
        patcher.RegisterPatch<SquadDexterityPatch>();
        patcher.RegisterPatch<SquadDamagePreviewPatch>();
        patcher.RegisterPatch<SquadBlockPreviewPatch>();
        patcher.RegisterPatch<SquadPotionTargetPatch>();
        patcher.RegisterPatch<SquadGigantificationPatch>();
        patcher.RegisterPatch<SquadBigMushroomPatch>();
        patcher.RegisterPatch<SquadTurnStartParticipantsPatch>();
        patcher.RegisterPatch<SquadRewardPoolPatch>();
        patcher.RegisterPatch<SquadMerchantPoolPatch>();
        patcher.RegisterPatch<SquadGeneratedPoolPatch>();
        patcher.RegisterPatch<SquadTransformPoolPatch>();
        patcher.RegisterPatch<SquadCardChoicePatch>();
        patcher.RegisterPatch<SquadPlaySeriesPatch>();
        patcher.RegisterPatch<SquadQueuedPlayCancelPatch>();
        patcher.RegisterPatch<SquadSubstitutionCostPatch>();
        patcher.RegisterPatch<SquadSubstitutionXPatch>();
        patcher.RegisterPatch<SquadSubstitutionClonePatch>();
        patcher.RegisterPatch<SquadSubstitutionDescriptionPatch>();
        patcher.RegisterPatch<SquadSubstitutionStarPatch>();
        patcher.RegisterPatch<SquadPersonalPowerPlayerPatch>();
        patcher.RegisterPatch<SquadPowerApplierNamePatch>();
        patcher.RegisterPatch<SquadTagTeamActorPatch>();
        patcher.RegisterPatch<SquadLethalityActorPatch>();
        patcher.RegisterPatch<SquadUnmovableActorPatch>();
        patcher.RegisterPatch<SquadBeaconTargetsPatch>();
        patcher.RegisterPatch<SquadTankTargetsPatch>();
        patcher.RegisterPatch<SquadGuardedLifetimePatch>();
        patcher.RegisterPatch<SquadPlatingOpeningPatch>();
        patcher.RegisterPatch<SquadOrbFocusPatch>();
        patcher.RegisterPatch<SquadOrbDamageOwnerPatch>();
        patcher.RegisterPatch<SquadOrbLayoutPatch>();
        patcher.RegisterPatch<SquadSharedHoverPatch>();
        patcher.RegisterPatch<SquadSharedUnhoverPatch>();
        patcher.RegisterPatch<SquadSharedSourcePatch>();
        patcher.RegisterPatch<SquadRefinementPreviewPatch>();
        patcher.RegisterPatch<SquadRefinementChoicePatch>();
        patcher.RegisterPatch<SquadFairyEligibilityPatch>();
        patcher.RegisterPatch<SquadLizardEligibilityPatch>();
        patcher.RegisterPatch<SquadHitCastPatch>();
        patcher.RegisterPatch<SquadDeathAnimationPatch>();
        patcher.RegisterPatch<SquadRoomExitPatch>();
        patcher.RegisterPatch<SquadHealTargetPatch>();
        patcher.RegisterPatch<SquadPowerTargetPatch>();
        patcher.RegisterPatch<SquadRestPatch>();
        patcher.RegisterPatch<SquadAncientRecoveryPatch>();
        patcher.RegisterPatch<SquadMaxHpGainPatch>();
        patcher.RegisterPatch<SquadMaxHpLossPatch>();
        patcher.RegisterPatch<SquadWorldSuccessionPatch>();
        patcher.RegisterPatch<SquadWorldDeathPresentationPatch>();
        patcher.RegisterPatch<SquadWorldHpSyncPatch>();
        try
        {
            if (!patcher.PatchAll() || patcher.AppliedPatchCount != patcher.RegisteredPatchCount)
                throw new InvalidOperationException("DohnaDohna required squad patches did not all install.");
        }
        catch
        {
            patcher.UnpatchAll();
            foreach (var patch in patcher.RegisteredPatches)
            {
                var target = STS2RitsuLib.Patching.Core.PatchTargetMethodResolver.Resolve(patch);
                if (target != null && Harmony.GetPatchInfo(target)?.Owners.Contains(patcher.PatcherId) == true)
                    throw new InvalidOperationException("DohnaDohna patch rollback retained a target.");
            }
            throw;
        }
        Logger.Info("DohnaDohna squad content and required integration registered.");
    }
}
