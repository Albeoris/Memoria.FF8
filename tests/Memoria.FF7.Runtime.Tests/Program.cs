using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

if (args.Contains("--native-condor-only", StringComparer.Ordinal))
{
    NativeCondorResourceTests.Run();
    NativeCondorSimulationTests.Run();
    return 0;
}

if (args.Contains("--native-condor-original-only", StringComparer.Ordinal))
{
    NativeCondorOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-condor-tutorial-only", StringComparer.Ordinal))
{
    NativeCondorTutorialTests.Run();
    return 0;
}

if (args.Contains("--native-condor-tutorial-original-only", StringComparer.Ordinal))
{
    NativeCondorTutorialOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-rearmed-exploration-only", StringComparer.Ordinal))
{
    NativeRearmedExplorationPriorityTests.Run();
    return 0;
}

if (args.Contains("--native-rearmed-interaction-original-only", StringComparer.Ordinal))
{
    NativeRearmedInteractionOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-component-attempt-only", StringComparer.Ordinal))
{
    NativeComponentAttemptPriorityTests.Run();
    NativeRearmedStoryPriorityTests.Run();
    return 0;
}

if (args.Contains("--native-component-attempt-original-only", StringComparer.Ordinal))
{
    NativeComponentAttemptOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-boarding-suffix-only", StringComparer.Ordinal))
{
    NativeBoardingSuffixTests.Run();
    return 0;
}

if (args.Contains("--native-boarding-suffix-original-only", StringComparer.Ordinal))
{
    NativeBoardingSuffixOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-deferred-planning-only", StringComparer.Ordinal))
{
    NativeDeferredPlanningWatchdogTests.Run();
    return 0;
}

if (args.Contains("--native-deferred-planning-original-only", StringComparer.Ordinal))
{
    NativeDeferredPlanningOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-world-hop-push-original-only", StringComparer.Ordinal))
{
    NativeWorldHopPushOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-ordered-push-original-only", StringComparer.Ordinal))
{
    NativeOrderedPushOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-aircraft-boarding-only", StringComparer.Ordinal))
{
    NativeAircraftBoardingAdviceTests.Run();
    return 0;
}

if (args.Contains("--native-returned-world-original-only", StringComparer.Ordinal))
{
    AutoPlayWorldOwnershipTests.Run();
    NativeReturnedWorldOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-current-touch-original-only", StringComparer.Ordinal))
{
    NativeCurrentTouchOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-returned-line-only", StringComparer.Ordinal))
{
    NativeReturnedLineLifecycleTests.Run();
    NativePassiveNpcPollingTests.Run();
    NativeCurrentTouchCallbackTests.Run();
    return 0;
}
if (args.Contains("--native-returned-line-original-only", StringComparer.Ordinal))
{
    NativeReturnedLineOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-corel-train-only", StringComparer.Ordinal))
{
    NativeCorelTrainInputTests.Run();
    return 0;
}
if (args.Contains("--native-corel-train-original-only", StringComparer.Ordinal))
{
    NativeCorelTrainOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-word-multiply-only", StringComparer.Ordinal))
{
    NativeWordMultiplyTests.Run();
    return 0;
}
if (args.Contains("--native-word-multiply-original-only", StringComparer.Ordinal))
{
    NativeWordMultiplyOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-gateway-crossing-contact-only", StringComparer.Ordinal))
{
    NativeGatewayCrossingContactTests.Run();
    return 0;
}
if (args.Contains("--native-gateway-crossing-original-only", StringComparer.Ordinal))
{
    NativeGatewayCrossingOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-ahead-push-only", StringComparer.Ordinal))
{
    NativeAheadPushPrerequisiteTests.Run();
    return 0;
}
if (args.Contains("--native-ahead-push-original-only", StringComparer.Ordinal))
{
    NativeAheadPushOriginalTests.Run();
    return 0;
}
if (args.Contains("--native-intermediate-push-only", StringComparer.Ordinal))
{
    NativeIntermediatePushPrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-intermediate-push-original-only", StringComparer.Ordinal))
{
    NativeIntermediatePushOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-airborne-source-original-only", StringComparer.Ordinal))
{
    NativeAirborneSourceOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-mounted-rider-control-only", StringComparer.Ordinal))
{
    NativeMountedRiderControlTests.Run();
    return 0;
}

if (args.Contains("--native-mounted-rider-original-only", StringComparer.Ordinal))
{
    NativeMountedRiderOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-aircraft-original-only", StringComparer.Ordinal))
{
    NativeAircraftOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-aircraft-preview-only", StringComparer.Ordinal))
{
    NativeAircraftPreviewTests.Run();
    return 0;
}

if (args.Contains("--native-rearmed-story-priority-only", StringComparer.Ordinal))
{
    NativeRearmedStoryPriorityTests.Run();
    return 0;
}

if (args.Contains("--native-rearmed-story-original-only", StringComparer.Ordinal))
{
    NativeRearmedStoryOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-transfer-effect-alias-only", StringComparer.Ordinal))
{
    NativeTransferEffectAliasTests.Run();
    return 0;
}

if (args.Contains("--native-field-transfer-arrival-original-only", StringComparer.Ordinal))
{
    NativeFieldTransferArrivalOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-atomic-control-entry-only", StringComparer.Ordinal))
{
    NativeAtomicControlEntryTests.Run();
    return 0;
}

if (args.Contains("--native-inclined-gateway-original-only", StringComparer.Ordinal))
{
    NativeInclinedGatewayOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-material-random-only", StringComparer.Ordinal))
{
    NativeMaterialRandomExecutionTests.Run();
    return 0;
}

if (args.Contains("--native-material-random-original-only", StringComparer.Ordinal))
{
    NativeMaterialRandomOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-passive-poll-only", StringComparer.Ordinal))
{
    NativePassiveNpcPollingTests.Run();
    return 0;
}

if (args.Contains("--native-passive-poll-original-only", StringComparer.Ordinal))
{
    NativePassiveNpcPollingOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-junon-escape-input-only", StringComparer.Ordinal))
{
    NativeJunonEscapeInputTests.Run();
    return 0;
}

if (args.Contains("--native-junon-escape-original-only", StringComparer.Ordinal))
{
    NativeJunonEscapeOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-music-header-only", StringComparer.Ordinal))
{
    NativeMusicHeaderTests.Run();
    return 0;
}

if (args.Contains("--native-music-id-original-only", StringComparer.Ordinal))
{
    NativeMusicIdOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-synchronous-turn-readiness-only", StringComparer.Ordinal))
{
    NativeSynchronousTurnReadinessTests.Run();
    return 0;
}

if (args.Contains("--native-synchronous-turn-original-only", StringComparer.Ordinal))
{
    NativeSynchronousTurnOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-priority-transfer-only", StringComparer.Ordinal))
{
    NativePriorityTransferTests.Run();
    return 0;
}

if (args.Contains("--native-priority-transfer-original-only", StringComparer.Ordinal))
{
    NativePriorityTransferOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-byte-decrement-only", StringComparer.Ordinal))
{
    NativeSaturatingByteDecrementTests.Run();
    return 0;
}

if (args.Contains("--native-byte-decrement-original-only", StringComparer.Ordinal))
{
    NativeSaturatingByteDecrementOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-whirlwind-tutorial-only", StringComparer.Ordinal))
{
    NativeWhirlwindTutorialTests.Run();
    return 0;
}

if (args.Contains("--native-whirlwind-approach-original-only", StringComparer.Ordinal))
{
    NativeWhirlwindApproachOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-crater-scene-admission-original-only", StringComparer.Ordinal))
{
    NativeCraterBridgeOriginalTests.RunSceneAdmission();
    return 0;
}

if (args.Contains("--native-line-probe-isolation-only", StringComparer.Ordinal))
{
    NativeLineProbeIsolationTests.Run();
    return 0;
}

if (args.Contains("--native-gateway-bridge-preference-only", StringComparer.Ordinal))
{
    NativeGatewayBridgePreferenceTests.Run();
    return 0;
}

if (args.Contains("--native-crater-bridge-original-only", StringComparer.Ordinal))
{
    NativeCraterBridgeOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-kawai11-only", StringComparer.Ordinal))
{
    NativeKawai11Tests.Run();
    return 0;
}

if (args.Contains("--native-kawai11-original-only", StringComparer.Ordinal))
{
    NativeKawai11OriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-movie-prerequisite-only", StringComparer.Ordinal))
{
    NativeGaeaMoviePrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-movie-prerequisite-original-only", StringComparer.Ordinal))
{
    NativeGaeaMoviePrerequisiteOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-boss-prerequisite-only", StringComparer.Ordinal))
{
    NativeGaeaBossPrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-boss-prerequisite-original-only", StringComparer.Ordinal))
{
    NativeGaeaBossPrerequisiteOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-summit-recovery-only", StringComparer.Ordinal))
{
    NativeGaeaSummitRecoveryTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-summit-recovery-original-only", StringComparer.Ordinal))
{
    NativeGaeaSummitRecoveryOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-third-warmth-original-only", StringComparer.Ordinal))
{
    NativeGaeaWarmthOriginalTests.RunThirdCliff();
    return 0;
}

if (args.Contains("--native-line-enter-departure-only", StringComparer.Ordinal))
{
    NativeLineEnterDepartureTests.Run();
    return 0;
}

if (args.Contains("--native-battle-vertical-motion-only", StringComparer.Ordinal))
{
    NativeBattleVerticalMotionTests.Run();
    return 0;
}

if (args.Contains("--native-battle-uv-only", StringComparer.Ordinal))
{
    NativeBattleUvAnimationTests.Run();
    return 0;
}

if (args.Contains("--native-battle-uv-original-only", StringComparer.Ordinal))
{
    NativeBattleUvOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-icicle-original-only", StringComparer.Ordinal))
{
    NativeGaeaIcicleOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-icicle-only", StringComparer.Ordinal))
{
    NativeGaeaIciclePrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-icicle-inspect-only", StringComparer.Ordinal))
{
    NativeGaeaIcicleOriginalTests.Inspect();
    return 0;
}

if (args.Contains("--native-gaea-rock-only", StringComparer.Ordinal))
{
    NativeGaeaRockPrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-field-entry-advice-only", StringComparer.Ordinal))
{
    NativeFieldEntryAdviceTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-cave-original-only", StringComparer.Ordinal))
{
    NativeGaeaCaveOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-cave-inspect-only", StringComparer.Ordinal))
{
    NativeGaeaCaveOriginalTests.Inspect();
    return 0;
}

if (args.Contains("--native-interrupted-navigation-only", StringComparer.Ordinal))
{
    NativeInterruptedNavigationTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-third-ledge-original-only", StringComparer.Ordinal))
{
    NativeGaeaThirdLedgeOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-warmth-only", StringComparer.Ordinal))
{
    NativeGaeaWarmthInputTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-warmth-original-only", StringComparer.Ordinal))
{
    NativeGaeaWarmthOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gaea-warmth-resources-original-only", StringComparer.Ordinal))
{
    NativeGaeaWarmthOriginalTests.ReadProfiles();
    return 0;
}

if (args.Contains("--native-gateway-component-observation-only", StringComparer.Ordinal))
{
    NativeGatewayComponentObservationTests.Run();
    return 0;
}

if (args.Contains("--native-textless-script-only", StringComparer.Ordinal))
{
    NativeTextlessScriptTests.Run();
    return 0;
}

if (args.Contains("--native-textless-script-original-only", StringComparer.Ordinal))
{
    NativeTextlessScriptOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-snow-world-preview-only", StringComparer.Ordinal))
{
    NativeSnowWorldPreviewTests.Run();
    return 0;
}

if (args.Contains("--native-snow-world-preview-original-only", StringComparer.Ordinal))
{
    NativeSnowWorldPreviewOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-holzoff-repeat-only", StringComparer.Ordinal))
{
    NativeHolzoffRepeatChoiceTests.Run();
    return 0;
}

if (args.Contains("--native-holzoff-repeat-original-only", StringComparer.Ordinal))
{
    NativeHolzoffRepeatOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-autoplay-only", StringComparer.Ordinal))
{
    NativeSnowboardAutoPlayTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-module-original-only", StringComparer.Ordinal))
{
    NativeSnowboardModuleOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-object-only", StringComparer.Ordinal))
{
    NativeSnowboardObjectTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-simulation-original-only", StringComparer.Ordinal))
{
    NativeSnowboardSimulationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-camera-only", StringComparer.Ordinal))
{
    NativeSnowboardCameraTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-surface-only", StringComparer.Ordinal))
{
    NativeSnowboardSurfaceTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-pose-only", StringComparer.Ordinal))
{
    NativeSnowboardPoseTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-action-script-only", StringComparer.Ordinal))
{
    NativeSnowboardActionScriptTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-collision-only", StringComparer.Ordinal))
{
    NativeSnowboardCollisionResponseTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-course-navigation-only", StringComparer.Ordinal))
{
    NativeSnowboardCourseNavigationTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-rider-motion-only", StringComparer.Ordinal))
{
    NativeSnowboardRiderMotionTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-terrain-only", StringComparer.Ordinal))
{
    NativeSnowboardTerrainTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-resources-original-only", StringComparer.Ordinal))
{
    NativeSnowboardResourcesOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-model-only", StringComparer.Ordinal))
{
    NativeSnowboardModelTests.Run();
    return 0;
}

if (args.Contains("--native-physical-repair-progress-only", StringComparer.Ordinal))
{
    NativePhysicalRepairProgressTests.Run();
    return 0;
}

if (args.Contains("--native-line-push-semantic-only", StringComparer.Ordinal))
{
    NativeLinePushSemanticTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-departure-only", StringComparer.Ordinal))
{
    NativeSnowboardDeparturePreferenceTests.Run();
    return 0;
}

if (args.Contains("--native-snowboard-departure-original-only", StringComparer.Ordinal))
{
    NativeSnowboardDepartureOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-glacier-map-choice-only", StringComparer.Ordinal))
{
    NativeGlacierMapChoiceTests.Run();
    return 0;
}

if (args.Contains("--native-glacier-map-original-only", StringComparer.Ordinal))
{
    NativeGlacierMapOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-gateway-route-original-only", StringComparer.Ordinal))
{
    NativeGatewayRouteOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-looping-animation-debt-only", StringComparer.Ordinal))
{
    NativeLoopingAnimationDebtTests.Run();
    return 0;
}

if (args.Contains("--native-npc-animation-loop-original-only", StringComparer.Ordinal))
{
    NativeNpcAnimationLoopOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-guarded-animation-debt-only", StringComparer.Ordinal))
{
    NativeGuardedAnimationDebtTests.Run();
    return 0;
}

if (args.Contains("--native-inn-animation-original-only", StringComparer.Ordinal))
{
    NativeInnAnimationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-pending-restore-boundary-only", StringComparer.Ordinal))
{
    NativePendingRestoreBoundaryTests.Run();
    return 0;
}

if (args.Contains("--native-camera-original-only", StringComparer.Ordinal))
{
    NativeCameraOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-section-relative-entry-only", StringComparer.Ordinal))
{
    NativeSectionRelativeEntryTests.Run();
    return 0;
}

if (args.Contains("--native-section-entry-original-only", StringComparer.Ordinal))
{
    NativeSectionEntryOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-unlocked-animation-debt-only", StringComparer.Ordinal))
{
    NativeUnlockedAnimationDebtTests.Run();
    return 0;
}

if (args.Contains("--native-partial-animation-original-only", StringComparer.Ordinal))
{
    NativePartialAnimationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-choice-observation-restore-only", StringComparer.Ordinal))
{
    NativeChoiceObservationRestoreTests.Run();
    return 0;
}

if (args.Contains("--native-instruction-input-original-only", StringComparer.Ordinal))
{
    NativeInstructionInputOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-tutorial-repeat-only", StringComparer.Ordinal))
{
    NativeTutorialRepeatTests.Run();
    return 0;
}

if (args.Contains("--native-repeat-tutorial-original-only", StringComparer.Ordinal))
{
    NativeTutorialOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-vines-choice-only", StringComparer.Ordinal))
{
    NativeVinesChoiceTests.Run();
    return 0;
}

if (args.Contains("--native-local-component-route-only", StringComparer.Ordinal))
{
    NativeLocalComponentRouteTests.Run();
    return 0;
}

if (args.Contains("--native-upper-destination-original-only", StringComparer.Ordinal))
{
    NativeUpperDestinationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-jump-only", StringComparer.Ordinal))
{
    NativeJumpTests.Run();
    return 0;
}

if (args.Contains("--native-field-jump-destination-only", StringComparer.Ordinal))
{
    NativeFieldJumpDestinationTests.Run();
    return 0;
}

if (args.Contains("--native-local-destination-original-only", StringComparer.Ordinal))
{
    NativeLocalDestinationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-blocking-line-prerequisite-only", StringComparer.Ordinal))
{
    NativeBlockingLinePrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-physical-portal-prefix-only", StringComparer.Ordinal))
{
    NativePhysicalPortalPrefixTests.Run();
    return 0;
}

if (args.Contains("--native-corridor-original-only", StringComparer.Ordinal))
{
    NativeCorridorOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-disc-change-only", StringComparer.Ordinal))
{
    NativeDiscChangeTests.Run();
    return 0;
}

if (args.Contains("--native-disc-change-original-only", StringComparer.Ordinal))
{
    NativeDiscChangeOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-water-profile-original-only", StringComparer.Ordinal))
{
    NativeWaterProfileOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-main-party-read-only", StringComparer.Ordinal))
{
    NativeMainPartyReadTests.Run();
    return 0;
}

if (args.Contains("--native-main-party-read-original-only", StringComparer.Ordinal))
{
    NativeMainPartyReadOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-presentation-polling-input-only", StringComparer.Ordinal))
{
    NativePresentationPollingInputTests.Run();
    return 0;
}

if (args.Contains("--native-altar-input-only", StringComparer.Ordinal))
{
    NativeAltarInputTests.Run();
    return 0;
}

if (args.Contains("--native-altar-input-original-only", StringComparer.Ordinal))
{
    NativeAltarInputOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-line-crossing-only", StringComparer.Ordinal))
{
    NativeAutoPlayPhysicalAvailabilityTests.CheckNativeLineCrossing();
    return 0;
}

if (args.Contains("--native-altar-continuation-original-only", StringComparer.Ordinal))
{
    NativeAltarContinuationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-entry-input-branch-only", StringComparer.Ordinal))
{
    NativeEntryInputBranchTests.Run();
    return 0;
}

if (args.Contains("--native-stone-continuation-original-only", StringComparer.Ordinal))
{
    NativeStoneContinuationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-constant-fade-completion-only", StringComparer.Ordinal))
{
    NativeConstantFadeCompletionTests.Run();
    return 0;
}

if (args.Contains("--native-night-continuation-original-only", StringComparer.Ordinal))
{
    NativeNightContinuationOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-background-destination-readiness-only", StringComparer.Ordinal))
{
    NativeBackgroundDestinationReadinessTests.Run();
    return 0;
}

if (args.Contains("--native-background-destination-original-only", StringComparer.Ordinal))
{
    NativeDestinationReadinessOriginalTests.RunBackgroundContinuation();
    return 0;
}

if (args.Contains("--native-field-ladder-destination-only", StringComparer.Ordinal))
{
    NativeFieldLadderDestinationTests.Run();
    return 0;
}

if (args.Contains("--field-connectivity-original-only", StringComparer.Ordinal))
{
    FieldConnectivityOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-excavation-dig-strategy-only", StringComparer.Ordinal))
{
    NativeExcavationDigStrategyTests.Run();
    return 0;
}

if (args.Contains("--native-excavation-dig-original-only", StringComparer.Ordinal))
{
    NativePromptInputOriginalTests.RunDigReward();
    return 0;
}

if (args.Contains("--native-excavation-continuation-only", StringComparer.Ordinal))
{
    NativeExcavationContinuationTests.Run();
    return 0;
}

if (args.Contains("--native-excavation-continuation-original-only", StringComparer.Ordinal))
{
    NativePromptInputOriginalTests.RunBombOwnedContinuation();
    return 0;
}

if (args.Contains("--native-excavation-bomb-input-only", StringComparer.Ordinal))
{
    NativeExcavationBombInputTests.Run();
    return 0;
}

if (args.Contains("--native-excavation-bomb-original-only", StringComparer.Ordinal))
{
    NativePromptInputOriginalTests.RunBombContinuation();
    return 0;
}

if (args.Contains("--native-choice-source-accounting-only", StringComparer.Ordinal))
{
    NativeChoiceSourceAccountingTests.Run();
    return 0;
}

if (args.Contains("--native-choice-route-original-only", StringComparer.Ordinal))
{
    NativePromptInputOriginalTests.RunChoiceRouteContinuation();
    return 0;
}

if (args.Contains("--native-ambient-message-input-only", StringComparer.Ordinal))
{
    NativeAmbientMessageInputTests.Run();
    return 0;
}

if (args.Contains("--native-prompt-input-original-only", StringComparer.Ordinal))
{
    NativePromptInputOriginalTests.Run();
    return 0;
}

if (args.Contains("--world-physical-route-guide-identity-only", StringComparer.Ordinal))
{
    WorldPhysicalRouteGuideIdentityTests.Run();
    return 0;
}

if (args.Contains("--world-mode-landing-continuation-original-only", StringComparer.Ordinal))
{
    WorldDestinationTopologyOriginalTests.RunLandingContinuation();
    return 0;
}

if (args.Contains("--remote-current-eligibility-only", StringComparer.Ordinal))
{
    RemoteCurrentEligibilityTests.Run();
    return 0;
}

if (args.Contains("--remote-current-eligibility-original-only", StringComparer.Ordinal))
{
    RemoteCurrentEligibilityTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-destination-topology-pending-only", StringComparer.Ordinal))
{
    WorldDestinationTopologyPendingTests.Run();
    return 0;
}

if (args.Contains("--world-destination-topology-original-only", StringComparer.Ordinal))
{
    WorldDestinationTopologyOriginalTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-destination-readiness-original-only", StringComparer.Ordinal))
{
    NativeDestinationReadinessOriginalTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-destination-readiness-only", StringComparer.Ordinal))
{
    NativeDestinationReadinessTests.Run();
    return 0;
}

if (args.Contains("--remote-persistent-entry-only", StringComparer.Ordinal))
{
    RemotePersistentEntryTests.Run();
    return 0;
}

if (args.Contains("--native-synchronous-activation-only", StringComparer.Ordinal))
{
    NativeSynchronousActivationTests.Run();
    return 0;
}

if (args.Contains("--native-navigation-prerequisite-only", StringComparer.Ordinal))
{
    NativeNavigationPrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--native-navigation-prerequisite-original-only", StringComparer.Ordinal))
{
    NativeNavigationPrerequisiteOriginalTests.RunOriginal();
    return 0;
}

if (args.Contains("--owned-service-prompt-only", StringComparer.Ordinal))
{
    NativeOwnedServicePromptTests.Run();
    return 0;
}

if (args.Contains("--native-choice-service-audio-only", StringComparer.Ordinal))
{
    NativeChoiceServiceAudioTests.Run();
    return 0;
}

if (args.Contains("--owned-inn-continuation-original-only", StringComparer.Ordinal))
{
    NativeOwnedInnContinuationTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-execution-provenance-only", StringComparer.Ordinal))
{
    NativeAutoPlayExecutionProvenanceTests.Run();
    NativeAutoPlayTransitionOwnershipTests.Run();
    return 0;
}

if (args.Contains("--native-async-transition-original-only", StringComparer.Ordinal))
{
    NativeAsyncTransitionOriginalTests.Run();
    return 0;
}

if (args.Contains("--native-lift-completion-original-only", StringComparer.Ordinal))
{
    NativeCameraCompletionTests.RunLiftOriginal();
    return 0;
}

if (args.Contains("--transition-exploration-original-only", StringComparer.Ordinal))
{
    NativeAutoPlayTransitionGuardTests.RunExplorationOriginal();
    return 0;
}

if (args.Contains("--native-transition-guard-only", StringComparer.Ordinal))
{
    NativeAutoPlayTransitionGuardTests.Run();
    return 0;
}

if (args.Contains("--native-synchronous-triangle-setup-only", StringComparer.Ordinal))
{
    NativeSynchronousTriangleSetupTests.Run();
    return 0;
}

if (args.Contains("--native-synchronous-triangle-setup-original-only", StringComparer.Ordinal))
{
    NativeSynchronousTriangleSetupTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-camera-tracking-original-only", StringComparer.Ordinal))
{
    NativeCameraCompletionTests.RunTrackingOriginal();
    return 0;
}

if (args.Contains("--native-camera-completion-only", StringComparer.Ordinal))
{
    NativeCameraCompletionTests.Run();
    return 0;
}

if (args.Contains("--native-camera-completion-original-only", StringComparer.Ordinal))
{
    NativeCameraCompletionTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-gateway-semantic-original-only", StringComparer.Ordinal))
{
    NativeGatewayBoundaryTests.RunSemanticOriginal();
    return 0;
}

if (args.Contains("--autoplay-movie-progress-original-only", StringComparer.Ordinal))
{
    AutoPlayWatchdogTests.RunMovieOriginal();
    return 0;
}

if (args.Contains("--native-transport-choice-original-only", StringComparer.Ordinal))
{
    NativeTransportRegionTests.RunChoiceOriginal();
    return 0;
}

if (args.Contains("--native-line-component-original-only", StringComparer.Ordinal))
{
    NativeLineOwnershipTests.RunComponentOriginal();
    return 0;
}

if (args.Contains("--native-transport-region-only", StringComparer.Ordinal))
{
    NativeTransportRegionTests.Run();
    return 0;
}

if (args.Contains("--native-transport-region-original-only", StringComparer.Ordinal))
{
    NativeTransportRegionTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-movie-readiness-original-only", StringComparer.Ordinal))
{
    NativeLineOwnershipTests.RunMovieOriginal();
    return 0;
}

if (args.Contains("--native-line-ownership-original-only", StringComparer.Ordinal))
{
    NativeLineOwnershipTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-gateway-boundary-only", StringComparer.Ordinal))
{
    NativeGatewayBoundaryTests.Run();
    return 0;
}

if (args.Contains("--native-gateway-boundary-original-only", StringComparer.Ordinal))
{
    NativeGatewayBoundaryTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-inactive-bridge-terrain-original-only", StringComparer.Ordinal))
{
    WorldModeTerrainEligibilityTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-world-route-cycle-only", StringComparer.Ordinal))
{
    NativeWorldRouteCycleTests.Run();
    return 0;
}

if (args.Contains("--native-world-route-cycle-original-only", StringComparer.Ordinal))
{
    NativeWorldRouteCycleTests.RunOriginal();
    return 0;
}

if (args.Contains("--owned-local-alternative-original-only", StringComparer.Ordinal))
{
    NativeOwnedContactArrivalTests.RunAlternativeOriginal();
    return 0;
}

if (args.Contains("--native-contact-catalog-original-only", StringComparer.Ordinal))
{
    NativeOwnedContactArrivalTests.RunCatalogOriginal();
    return 0;
}

if (args.Contains("--native-owned-contact-arrival-only", StringComparer.Ordinal))
{
    NativeOwnedContactArrivalTests.Run();
    return 0;
}

if (args.Contains("--native-owned-contact-arrival-original-only", StringComparer.Ordinal))
{
    NativeOwnedContactArrivalTests.RunOriginal();
    return 0;
}

if (args.Contains("--actor-contact-region-only", StringComparer.Ordinal))
{
    ActorContactRegionTests.Run();
    return 0;
}

if (args.Contains("--actor-contact-policy-original-only", StringComparer.Ordinal))
{
    ActorContactRegionTests.RunPolicyOriginal();
    return 0;
}

if (args.Contains("--actor-contact-region-original-only", StringComparer.Ordinal))
{
    ActorContactRegionTests.RunOriginal();
    return 0;
}

if (args.Contains("--spatial-triangle-eligibility-only", StringComparer.Ordinal))
{
    SpatialTriangleEligibilityTests.Run();
    return 0;
}
if (args.Contains("--spatial-triangle-eligibility-original-only", StringComparer.Ordinal))
{
    SpatialTriangleEligibilityTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-observation-cache-only", StringComparer.Ordinal))
{
    WorldObservationAnalysisCacheTests.Run();
    return 0;
}

if (args.Contains("--world-catalog-continuation-original-only", StringComparer.Ordinal))
{
    WorldModeTopologyContinuationTests.RunCurrentCatalog();
    return 0;
}

if (args.Contains("--world-mode-terrain-only", StringComparer.Ordinal))
{
    WorldModeTerrainEligibilityTests.Run();
    return 0;
}
if (args.Contains("--world-mode-terrain-original-only", StringComparer.Ordinal))
{
    WorldModeTopologyContinuationTests.RunCurrentInertEntry(terrainMode: true);
    return 0;
}
if (args.Contains("--world-input-masks-only", StringComparer.Ordinal))
{
    WorldInputTests.Run();
    return 0;
}

if (args.Contains("--world-inert-entry-original-only", StringComparer.Ordinal))
{
    WorldModeTopologyContinuationTests.RunCurrentInertEntry();
    return 0;
}

if (args.Contains("--world-mode-topology-only", StringComparer.Ordinal))
{
    WorldModeTopologyContinuationTests.Run();
    return 0;
}
if (args.Contains("--world-mode-topology-original-only", StringComparer.Ordinal))
{
    WorldModeTopologyContinuationTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-physical-continuation-only", StringComparer.Ordinal))
{
    WorldPhysicalSearchContinuationTests.Run();
    return 0;
}

if (args.Contains("--world-physical-continuation-original-only", StringComparer.Ordinal))
{
    WorldPhysicalSearchContinuationTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-route-cycle-original-only", StringComparer.Ordinal))
{
    NativeRouteCycleTests.RunOriginal();
    return 0;
}

if (args.Contains("--navigation-mode-clock-original-only", StringComparer.Ordinal))
{
    NavigationModeClockTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-line-boundary-original-only", StringComparer.Ordinal))
{
    NativeLineBoundaryTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-actor-approach-only", StringComparer.Ordinal))
{
    NativeActorApproachTests.Run();
    return 0;
}

if (args.Contains("--native-pending-music-only", StringComparer.Ordinal))
{
    NativePendingMusicTransitionTests.Run();
    return 0;
}

if (args.Contains("--native-save-menu-only", StringComparer.Ordinal))
{
    NativeSaveMenuTests.Run();
    return 0;
}

if (args.Contains("--keyman-route-only", StringComparer.Ordinal))
{
    NativeKeymanRouteTests.Run();
    return 0;
}

if (args.Contains("--keyman-route-original-only", StringComparer.Ordinal))
{
    NativeKeymanRouteTests.RunOriginal();
    return 0;
}

if (args.Contains("--clock-route-only", StringComparer.Ordinal))
{
    NativeClockRouteTests.Run();
    return 0;
}

if (args.Contains("--clock-route-original-only", StringComparer.Ordinal))
{
    NativeClockRouteTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-equivalent-transport-only", StringComparer.Ordinal))
{
    NativeEquivalentTransportTests.Run();
    return 0;
}
if (args.Contains("--native-equivalent-transport-original-only", StringComparer.Ordinal))
{
    NativeEquivalentTransportTests.RunOriginal();
    return 0;
}
if (args.Contains("--world-source-choice-only", StringComparer.Ordinal))
{
    WorldSourceChoiceTests.Run();
    return 0;
}

if (args.Contains("--presentation-activation-only", StringComparer.Ordinal))
{
    PresentationActivationTests.Run();
    return 0;
}

if (args.Contains("--gateway-crossing-availability-only", StringComparer.Ordinal))
{
    GatewayCrossingAvailabilityTests.Run();
    return 0;
}

if (args.Contains("--productive-ordinary-search-only", StringComparer.Ordinal))
{
    ProductiveOrdinarySearchTests.Run();
    return 0;
}

if (args.Contains("--ordinary-alternative-only", StringComparer.Ordinal))
{
    OrdinaryAlternativeTests.Run();
    return 0;
}

if (args.Contains("--native-owned-ladder-graph-only", StringComparer.Ordinal))
{
    NativeOwnedLadderGraphTests.Run();
    return 0;
}

if (args.Contains("--native-actor-source-release-only", StringComparer.Ordinal))
{
    NativeActorRouteTests.RunSourceRelease();
    return 0;
}

if (args.Contains("--native-source-goal-fairness-only", StringComparer.Ordinal))
{
    NativeSourceGoalFairnessTests.Run();
    return 0;
}

if (args.Contains("--native-actor-route-only", StringComparer.Ordinal))
{
    NativeActorRouteTests.Run();
    return 0;
}

if (args.Contains("--native-goal-fairness-only", StringComparer.Ordinal))
{
    NativeGoalFairnessTests.Run();
    return 0;
}

if (args.Contains("--native-route-advisory-only", StringComparer.Ordinal))
{
    NativeRouteAdvisoryTests.Run();
    return 0;
}

if (args.Contains("--native-primitive-graph-only", StringComparer.Ordinal))
{
    NativePrimitiveGraphTests.Run();
    return 0;
}

if (args.Contains("--physical-failure-receipt-only", StringComparer.Ordinal))
{
    PhysicalFailureReceiptTests.Run();
    return 0;
}

if (args.Contains("--native-graph-ownership-only", StringComparer.Ordinal))
{
    NativeGraphOwnershipTests.Run();
    return 0;
}

if (args.Contains("--native-state-graph-only", StringComparer.Ordinal))
{
    NativeStateGraphTests.Run();
    return 0;
}

if (args.Contains("--approach-context-ownership-only", StringComparer.Ordinal))
{
    ApproachContextOwnershipTests.Run();
    return 0;
}


if (args.Contains("--native-temporal-planning-only", StringComparer.Ordinal))
{
    throw new ArgumentException("The fixed-ray temporal strategies were retired; use --native-state-graph-only and --native-planning-health-only.");
}

if (args.Contains("--native-planning-health-only", StringComparer.Ordinal))
{
    NativePlanningHealthTests.Run();
    return 0;
}

if (args.Contains("--active-native-planning-only", StringComparer.Ordinal))
{
    ActiveNativePlanningTests.Run();
    return 0;
}

if (args.Contains("--native-input-reservation-original-only", StringComparer.Ordinal))
{
    NativeInputReservationTests.RunOriginal();
    return 0;
}

if (args.Contains("--queued-native-line-only", StringComparer.Ordinal))
{
    NativeFieldFrontierTests.RunQueuedLineRequests();
    return 0;
}

if (args.Contains("--native-field-frontier-original-only", StringComparer.Ordinal))
{
    NativeFieldFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--frontier-input-accounting-only", StringComparer.Ordinal))
{
    FrontierInputAccountingTests.Run();
    return 0;
}

if (args.Contains("--native-custom-lighting-only", StringComparer.Ordinal))
{
    NativeCustomLightingTests.Run();
    return 0;
}

if (args.Contains("--native-custom-lighting-original-only", StringComparer.Ordinal))
{
    NativeCustomLightingTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-route-fork-resources-original-only", StringComparer.Ordinal))
{
    NativeRouteForkResourceTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-route-fair-only", StringComparer.Ordinal))
{
    NativeRouteFairSchedulingTests.Run();
    return 0;
}

if (args.Contains("--native-route-fair-original-only", StringComparer.Ordinal))
{
    NativeRouteFairSchedulingTests.RunOriginal();
    return 0;
}

if (args.Contains("--entry-fade-readiness-only", StringComparer.Ordinal))
{
    EntryFadeReadinessTests.Run();
    return 0;
}

if (args.Contains("--entry-fade-readiness-original-only", StringComparer.Ordinal))
{
    EntryFadeReadinessTests.RunOriginal();
    return 0;
}

if (args.Contains("--navigation-mode-scheduling-only", StringComparer.Ordinal))
{
    NavigationModeSchedulingTests.Run();
    return 0;
}

if (args.Contains("--physical-search-continuation-only", StringComparer.Ordinal))
{
    PhysicalSearchContinuationTests.Run();
    return 0;
}

if (args.Contains("--talk-region-geometry-original-only", StringComparer.Ordinal))
{
    TalkRegionGeometryTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-talk-floor-only", StringComparer.Ordinal))
{
    NativeTalkFloorContactTests.Run();
    return 0;
}

if (args.Contains("--native-talk-floor-original-only", StringComparer.Ordinal))
{
    NativeTalkFloorContactTests.RunOriginal();
    return 0;
}

if (args.Contains("--admitted-cycle-approach-only", StringComparer.Ordinal))
{
    AdmittedCycleApproachTests.Run();
    return 0;
}

if (args.Contains("--native-actor-frontier-original-only", StringComparer.Ordinal))
{
    NativeActorFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--bounded-movement-availability-only", StringComparer.Ordinal))
{
    BoundedMovementAvailabilityTests.Run();
    return 0;
}

if (args.Contains("--native-gateway-frontier-original-only", StringComparer.Ordinal))
{
    NativeGatewayFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-gateway-route-only", StringComparer.Ordinal))
{
    NativeGatewayRouteTests.Run();
    return 0;
}

if (args.Contains("--native-post-ivy-frontier-original-only", StringComparer.Ordinal))
{
    NativePostIvyFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-ivy-frontier-original-only", StringComparer.Ordinal))
{
    NativeIvyFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-event-frontier-original-only", StringComparer.Ordinal))
{
    throw new ArgumentException("The remote future-proof selector has been retired; use --native-source-revalidation-only or --native-upper-frontier-original-only.");
}

if (args.Contains("--native-source-revalidation-only", StringComparer.Ordinal))
{
    NativeSourceRevalidationTests.Run();
    return 0;
}

if (args.Contains("--native-planning-original-only", StringComparer.Ordinal))
{
    Console.WriteLine("SKIP retired remote-search scheduling fixture; use --native-planning-quantum-only for cooperative scheduling or --native-upper-frontier-original-only for current source-refresh continuation.");
    return 0;
}

if (args.Contains("--native-planning-quantum-only", StringComparer.Ordinal))
{
    NativePlanningQuantumTests.Run();
    return 0;
}

if (args.Contains("--native-upper-frontier-original-only", StringComparer.Ordinal))
{
    NativeUpperFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-cycle-frontier-original-only", StringComparer.Ordinal))
{
    NativeCycleFrontierTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-return-loop-original-only", StringComparer.Ordinal))
{
    NativeReturnLoopTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-owned-route-original-only", StringComparer.Ordinal))
{
    NativeOwnedRouteTests.Run();
    NativeOwnedRouteTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-owned-route-only", StringComparer.Ordinal))
{
    NativeOwnedRouteTests.Run();
    return 0;
}

if (args.Contains("--talk-condition-search-original-only", StringComparer.Ordinal))
{
    TalkConditionDomainTests.RunOriginal(searchOwnership: true);
    return 0;
}

if (args.Contains("--talk-condition-domain-original-only", StringComparer.Ordinal))
{
    TalkConditionDomainTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-choice-continuation-only", StringComparer.Ordinal))
{
    NativeChoiceContinuationTests.Run();
    return 0;
}

if (args.Contains("--native-choice-continuation-original-only", StringComparer.Ordinal))
{
    NativeChoiceContinuationTests.RunOriginal();
    return 0;
}

if (args.Contains("--owned-travel-continuation-only", StringComparer.Ordinal))
{
    OwnedTravelContinuationTests.Run();
    return 0;
}

if (args.Contains("--owned-travel-continuation-original-only", StringComparer.Ordinal))
{
    OwnedTravelContinuationTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-boarding-prerequisite-only", StringComparer.Ordinal))
{
    WorldBoardingPrerequisiteTests.Run();
    return 0;
}

if (args.Contains("--world-boarding-prerequisite-original-only", StringComparer.Ordinal))
{
    WorldBoardingPrerequisiteTests.RunOriginal();
    return 0;
}

if (args.Contains("--world-landing-continuation-original-only", StringComparer.Ordinal))
{
    WorldBoardingPrerequisiteTests.RunLandingOriginal();
    return 0;
}

if (args.Contains("--world-destination-goal-only", StringComparer.Ordinal))
{
    WorldDestinationGoalTests.Run();
    return 0;
}

if (args.Contains("--world-destination-original-only", StringComparer.Ordinal))
{
    WorldDestinationGoalTests.RunOriginal();
    return 0;
}

if (args.Contains("--field-destination-goal-only", StringComparer.Ordinal))
{
    AutoPlayFieldDestinationGoalTests.Run();
    return 0;
}

if (args.Contains("--field-destination-original-selection-only", StringComparer.Ordinal))
{
    AutoPlayFieldDestinationGoalTests.RunOriginalSelection();
    return 0;
}

if (args.Contains("--stochastic-presentation-only", StringComparer.Ordinal))
{
    AutoPlayStochasticPresentationTests.Run();
    return 0;
}

if (args.Contains("--world-mode-foot-only", StringComparer.Ordinal))
{
    AutoPlayWorldModeFootTests.Run();
    return 0;
}

if (args.Contains("--world-mode-navigation-only", StringComparer.Ordinal))
{
    AutoPlayWorldModeNavigationTests.Run();
    return 0;
}

if (args.Contains("--world-control-only", StringComparer.Ordinal))
{
    AutoPlayWorldControlTests.Run();
    return 0;
}

if (args.Contains("--autoplay-entry-readiness-only", StringComparer.Ordinal))
{
    AutoPlayEntryReadinessTests.Run();
    return 0;
}

if (args.Contains("--frog-status-only", StringComparer.Ordinal))
{
    FrogStatusTests.Run();
    return 0;
}

if (args.Contains("--world-interaction-only", StringComparer.Ordinal))
{
    AutoPlayWorldInteractionTests.Run();
    return 0;
}

if (args.Contains("--world-wait-analysis-only", StringComparer.Ordinal))
{
    AutoPlayWorldWaitAnalysisTests.Run();
    return 0;
}

if (args.Contains("--discovery-deadline-only", StringComparer.Ordinal))
{
    AutoPlayDiscoveryDeadlineTests.Run();
    return 0;
}

if (args.Contains("--world-field-goal-only", StringComparer.Ordinal))
{
    AutoPlayWorldFieldGoalTests.Run();
    return 0;
}

if (args.Contains("--line-waypoint-only", StringComparer.Ordinal))
{
    AutoPlayLineWaypointTests.Run();
    return 0;
}

if (args.Contains("--autoplay-restorative-benefit-only", StringComparer.Ordinal))
{
    NativeRestorativeBenefitTests.Run();
    return 0;
}

if (args.Contains("--navigation-action-modes-only", StringComparer.Ordinal))
{
    AutoPlayNavigationActionModeTests.Run();
    return 0;
}

if (args.Contains("--field-hp-subtract-only", StringComparer.Ordinal))
{
    NativeFieldHitPointSubtractionTests.Run();
    return 0;
}

if (args.Contains("--spatial-direct-effect-only", StringComparer.Ordinal))
{
    AutoPlayDirectSpatialEffectTests.Run();
    return 0;
}

if (args.Contains("--spatial-entry-input-only", StringComparer.Ordinal))
{
    AutoPlaySpatialEntryInputTests.Run();
    return 0;
}

if (args.Contains("--autoplay-spatial-navigation-only", StringComparer.Ordinal))
{
    AutoPlaySpatialNavigationTests.Run();
    return 0;
}

if (args.Contains("--spatial-navigation-only", StringComparer.Ordinal))
{
    AutoPlayDirectSpatialTransitionTests.Run(navigationOnly: true);
    return 0;
}

if (args.Contains("--direct-spatial-transition-only", StringComparer.Ordinal))
{
    AutoPlayDirectSpatialTransitionTests.Run();
    return 0;
}

if (args.Contains("--world-facing-search-only", StringComparer.Ordinal))
{
    AutoPlayWorldFacingSearchTests.Run();
    return 0;
}

if (args.Contains("--autoplay-watchdog-only", StringComparer.Ordinal))
{
    AutoPlayWatchdogTests.Run();
    return 0;
}

if (args.Contains("--world-source-resume-only", StringComparer.Ordinal))
{
    AutoPlayWorldSourceResumeTests.Run();
    return 0;
}

if (args.Contains("--chocobo-camera-only", StringComparer.Ordinal))
{
    NativeChocoboCameraTests.Run();
    return 0;
}

if (args.Contains("--minigame-return-only", StringComparer.Ordinal))
{
    NativeMinigameTests.RunReturnOnly();
    return 0;
}

if (args.Contains("--chocobo-session-only", StringComparer.Ordinal))
{
    NativeChocoboSessionTests.Run();
    return 0;
}

if (args.Contains("--chocobo-simulation-only", StringComparer.Ordinal))
{
    NativeChocoboSimulationTests.Run();
    return 0;
}

if (args.Contains("--chocobo-course-only", StringComparer.Ordinal))
{
    NativeChocoboCourseTests.Run();
    return 0;
}

if (args.Contains("--navigation-preparation-only", StringComparer.Ordinal))
{
    NativeNavigationPreparationTests.Run();
    return 0;
}

if (args.Contains("--preempted-readiness-only", StringComparer.Ordinal))
{
    NativePreemptedReadinessTests.Run();
    return 0;
}

if (args.Contains("--motion-preemption-only", StringComparer.Ordinal))
{
    NativeMotionPreemptionTests.Run();
    NativeJumpTests.Run();
    NativeLadderTests.Run();
    NativeAnimationTests.Run();
    return 0;
}

if (args.Contains("--constant-fade-only", StringComparer.Ordinal))
{
    NativeConstantFadeTests.Run();
    return 0;
}

if (args.Contains("--line-entry-avoidance-only", StringComparer.Ordinal))
{
    NativeLineEntryAvoidanceTests.Run();
    return 0;
}

if (args.Contains("--native-transparency-only", StringComparer.Ordinal))
{
    NativeTransparencyTests.Run();
    return 0;
}

if (args.Contains("--native-model-effect-original-only", StringComparer.Ordinal))
{
    NativeModelEffectTests.RunReachedCheckpoint();
    return 0;
}

if (args.Contains("--native-model-completion-only", StringComparer.Ordinal))
{
    NativeModelCompletionTests.Run();
    return 0;
}

if (args.Contains("--party-presentation-only", StringComparer.Ordinal))
{
    NativePartyPresentationTests.Run();
    return 0;
}

if (args.Contains("--actor-reaction-sound-gain-only", StringComparer.Ordinal))
{
    NativeReactionSoundGainTests.Run();
    return 0;
}

if (args.Contains("--actor-reaction-motion-only", StringComparer.Ordinal))
{
    NativeReactionMotionTests.Run();
    return 0;
}

if (args.Contains("--actor-hitmark-only", StringComparer.Ordinal))
{
    NativeActorHitmarkTests.Run();
    return 0;
}

if (args.Contains("--autoplay-world-battle-checkpoint-only", StringComparer.Ordinal))
{
    AutoPlayWorldBattleCheckpointTests.Run();
    return 0;
}

if (args.Contains("--autoplay-discovery-only", StringComparer.Ordinal))
{
    NativeAutoPlayDiscoveryTests.Run();
    return 0;
}

if (args.Contains("--autoplay-component-probes-only", StringComparer.Ordinal))
{
    NativeComponentProbeTests.Run();
    return 0;
}

if (args.Contains("--entry-dependencies-only", StringComparer.Ordinal))
{
    NativeEntryDependencyTests.Run();
    return 0;
}

if (args.Contains("--autoplay-readiness-only", StringComparer.Ordinal))
{
    NativeAutoPlayReadinessTests.Run();
    NativePreemptedReadinessTests.Run();
    return 0;
}

if (args.Contains("--autoplay-transition-ownership-only", StringComparer.Ordinal))
{
    NativeAutoPlayTransitionOwnershipTests.Run();
    return 0;
}

if (args.Contains("--blocked-source-navigation-only", StringComparer.Ordinal))
{
    NativeBlockedSourceNavigationTests.Run();
    return 0;
}

if (args.Contains("--world-model-contact-only", StringComparer.Ordinal))
{
    AutoPlayWorldModelContactTests.Run();
    return 0;
}

if (args.Contains("--world-model-policy-only", StringComparer.Ordinal))
{
    AutoPlayWorldOwnershipTests.Run();
    AutoPlayWorldModelPolicyTests.Run();
    return 0;
}

if (args.Contains("--world-dialogue-only", StringComparer.Ordinal))
{
    WorldDialogueTests.Run();
    return 0;
}

if (args.Contains("--autoplay-navigation-refresh-only", StringComparer.Ordinal))
{
    AutoPlayNavigationRefreshTests.Run();
    return 0;
}

if (args.Contains("--autoplay-gateway-topology-only", StringComparer.Ordinal))
{
    AutoPlayGatewayTopologyTests.Run();
    return 0;
}

if (args.Contains("--world-discovery-refresh-only", StringComparer.Ordinal))
{
    AutoPlayWorldDiscoveryRefreshTests.Run();
    return 0;
}

if (args.Contains("--world-model-navigation-only", StringComparer.Ordinal))
{
    AutoPlayWorldModelNavigationTests.Run();
    return 0;
}

if (args.Contains("--autoplay-spatial-only", StringComparer.Ordinal))
{
    NativeAutoPlaySpatialTests.Run();
    AutoPlayDirectSpatialTransitionTests.CheckChangedSpatialCatalog();
    return 0;
}

if (args.Contains("--autoplay-choice-only", StringComparer.Ordinal))
{
    AutoPlayChoiceWorkflowTests.Run();
    return 0;
}

if (args.Contains("--autoplay-input-only", StringComparer.Ordinal))
{
    NativeAutoPlayScriptedInputTests.Run();
    return 0;
}

if (args.Contains("--autoplay-host-reads-only", StringComparer.Ordinal))
{
    NativeAutoPlayHostReadTests.Run();
    return 0;
}

if (args.Contains("--autoplay-physical-only", StringComparer.Ordinal))
{
    NativeAutoPlayPhysicalAvailabilityTests.Run();
    return 0;
}

if (args.Contains("--autoplay-planning-only", StringComparer.Ordinal))
{
    NativeAutoPlayPolicyPlanningTests.Run();
    NativeAutoPlayDestinationPreferenceTests.Run();
    return 0;
}

if (args.Contains("--autoplay-visit-guards-only", StringComparer.Ordinal))
{
    AutoPlayVisitGuardTests.Run();
    return 0;
}

if (args.Contains("--autoplay-transitions-only", StringComparer.Ordinal))
{
    NativeAutoPlayTransitionGuardTests.Run();
    return 0;
}

if (args.Contains("--autoplay-world-terrain-only", StringComparer.Ordinal))
{
    AutoPlayWorldTerrainTests.Run();
    return 0;
}

if (args.Contains("--autoplay-world-movement-only", StringComparer.Ordinal))
{
    AutoPlayWorldMovementTests.Run();
    return 0;
}

if (args.Contains("--autoplay-world-frontiers-only", StringComparer.Ordinal))
{
    AutoPlayWorldFrontierCatalogTests.Run();
    return 0;
}

if (args.Contains("--autoplay-world-navigation-only", StringComparer.Ordinal))
{
    AutoPlayWorldNavigationTests.Run();
    return 0;
}

if (args.Contains("--world-semantic-availability-only", StringComparer.Ordinal))
{
    AutoPlayWorldSemanticAvailabilityTests.Run();
    return 0;
}

if (args.Contains("--native-effect-alias-only", StringComparer.Ordinal))
{
    NativeEffectAliasTests.Run();
    return 0;
}

if (args.Contains("--native-direct-transition-alias-only", StringComparer.Ordinal))
{
    NativeDirectTransitionAliasTests.Run();
    return 0;
}

if (args.Contains("--native-direct-transition-alias-original-only", StringComparer.Ordinal))
{
    NativeDirectTransitionAliasTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-line-touch-region-only", StringComparer.Ordinal))
{
    NativeLineTouchRegionTests.Run();
    return 0;
}

if (args.Contains("--native-remote-leaf-original-only", StringComparer.Ordinal))
{
    NativeRemoteLeafTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-town-exit-cycle-original-only", StringComparer.Ordinal))
{
    NativeTownExitCycleTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-post-pickup-cycle-original-only", StringComparer.Ordinal))
{
    NativePostPickupLineTests.RunCycleOriginal();
    return 0;
}

if (args.Contains("--native-post-pickup-line-original-only", StringComparer.Ordinal))
{
    NativePostPickupLineTests.RunOriginal();
    return 0;
}

if (args.Contains("--native-reactor-entry-original-only", StringComparer.Ordinal))
{
    NativeReactorEntryTests.RunOriginal();
    return 0;
}

if (args.Contains("--completed-native-choice-only", StringComparer.Ordinal))
{
    CompletedNativeChoiceTests.Run();
    return 0;
}

if (args.Contains("--native-effect-alias-original-only", StringComparer.Ordinal))
{
    NativeEffectAliasTests.RunOriginal();
    return 0;
}

if (args.Contains("--autoplay-world-policy-only", StringComparer.Ordinal))
{
    AutoPlayWorldPolicyTests.Run();
    return 0;
}

if (args.Contains("--world-transport-only", StringComparer.Ordinal))
{
    WorldTransportContinuationTests.Run();
    return 0;
}

if (args.Contains("--shared-message-only", StringComparer.Ordinal))
{
    NativeSharedMessageTests.Run();
    return 0;
}

if (args.Contains("--window-number-only", StringComparer.Ordinal))
{
    NativeWindowNumberTests.Run();
    return 0;
}

if (args.Contains("--battle-inventory-only", StringComparer.Ordinal))
{
    NativeBattleInventoryTests.Run();
    return 0;
}

if (args.Contains("--inventory-slots-only", StringComparer.Ordinal))
{
    NativeInventorySlotTests.Run();
    NativeBattleInventoryTests.Run();
    NativeItemTests.Run();
    NativeEquipmentRemovalTests.Run();
    NativeSaveBankTests.Run();
    return 0;
}

if (args.Contains("--battle-economy-queues-only", StringComparer.Ordinal))
{
    NativeBattleEconomyQueueTests.Run();
    return 0;
}

if (args.Contains("--battle-theft-only", StringComparer.Ordinal))
{
    NativeBattleTheftTests.Run();
    return 0;
}

if (args.Contains("--naming-growth-only", StringComparer.Ordinal))
{
    NativeNamingGrowthTests.Run();
    NativeGrowthTests.Run();
    return 0;
}

if (args.Contains("--naming-lifecycle-only", StringComparer.Ordinal))
{
    NativeNamingLifecycleTests.Run();
    return 0;
}

if (args.Contains("--battle-command-grants-only", StringComparer.Ordinal))
{
    NativeBattleCommandGrantTests.Run();
    return 0;
}

if (args.Contains("--command-projection-only", StringComparer.Ordinal))
{
    NativeCommandProjectionTests.Run();
    return 0;
}

if (args.Contains("--window-defaults-only", StringComparer.Ordinal))
{
    NativeWindowDefaultsTests.Run();
    NativeWindowParameterTests.Run();
    NativeWindowNumberTests.Run();
    NativeProcessTests.RunWindowSizeWhenFree();
    NativeProcessTests.RunSpecialWindow();
    return 0;
}

if (args.Contains("--movement-facing-only", StringComparer.Ordinal))
{
    NativeActorApproachTests.RunMovementFacing();
    Console.WriteLine("PASS native actor movement facing and cold continuation");
    return 0;
}

if (args.Contains("--movement-animation-only", StringComparer.Ordinal))
{
    NativeMovementAnimationTests.Run();
    return 0;
}

if (args.Contains("--water-effect-only", StringComparer.Ordinal))
{
    NativeWaterEffectTests.Run();
    return 0;
}

if (args.Contains("--background-scroll-only", StringComparer.Ordinal))
{
    NativeBackgroundScrollTests.Run();
    return 0;
}

if (args.Contains("--background-depth-only", StringComparer.Ordinal))
{
    NativeBackgroundDepthTests.Run();
    return 0;
}

if (args.Contains("--aura-limit-only", StringComparer.Ordinal))
{
    OriginAuraLimitTests.Run();
    return 0;
}

if (args.Contains("--origin-status-rules-only", StringComparer.Ordinal))
{
    OriginAuraLimitTests.Run();
    OriginBattleStatusTests.Run();
    OriginFloatPresentationTests.Run();
    OriginMagicSequenceTests.Run();
    return 0;
}

if (args.Contains("--magic-sequence-only", StringComparer.Ordinal))
{
    OriginMagicSequenceTests.Run();
    return 0;
}

if (args.Contains("--stop-clock-only", StringComparer.Ordinal))
{
    NativeStopClockTests.Run();
    OriginZombieStatusTests.Run();
    return 0;
}

if (args.Contains("--status-counterparts-only", StringComparer.Ordinal))
{
    GuestBattleConversionTests.Run();
    NativeStatusCounterpartClockTests.Run();
    NativeStatusConsumersTests.Run();
    NativeSpecialStatusTests.Run();
    NativeBattleActionTests.Run();
    return 0;
}

if (args.Contains("--special-enemies-only", StringComparer.Ordinal))
{
    NativeSpecialAiTests.RunContracts();
    NativeElixirActionTests.Run();
    NativeSpecialStatusTests.Run();
    GuestFractionRecoveryTests.Run();
    SpecialEnemyOutcomeTests.Run();
    SpecialEnemyInitializationTests.Run();
    return 0;
}

if (args.Contains("--enemy-extensions-only", StringComparer.Ordinal))
{
    EnemyExtensionTests.Run();
    return 0;
}

if (args.Contains("--guest-only", StringComparer.Ordinal))
{
    NativeGuestLimitTests.Run();
    GuestContinuationTests.Run();
    GuestCompositeActionTests.Run();
    GuestBattleConversionTests.Run();
    return 0;
}

if (args.Contains("--guest-continuation-only", StringComparer.Ordinal))
{
    GuestContinuationTests.Run();
    return 0;
}

if (args.Contains("--guest-composite-only", StringComparer.Ordinal))
{
    GuestCompositeActionTests.Run();
    return 0;
}

if (args.Contains("--minigame-only", StringComparer.Ordinal))
{
    NativeMinigameTests.Run();
    return 0;
}

if (args.Contains("--native-saves-only", StringComparer.Ordinal))
{
    NativeSaveBankTests.Run();
    return 0;
}

if (args.Contains("--pub-only", StringComparer.Ordinal))
{
    NativeWallMarketPubCoverageTests.Run();
    return 0;
}

if (args.Contains("--abandoned-item-store-only", StringComparer.Ordinal))
{
    NativeWallMarketAbandonedItemStoreCoverageTests.Run();
    return 0;
}

if (args.Contains("--meal-only", StringComparer.Ordinal))
{
    NativeMealTests.Run();
    return 0;
}

if (args.Contains("--restaurant-only", StringComparer.Ordinal))
{
    NativeWallMarketRestaurantCoverageTests.Run();
    return 0;
}

if (args.Contains("--restaurant-return-only", StringComparer.Ordinal))
{
    NativeWallMarketRestaurantCoverageTests.RunReturn();
    return 0;
}

if (args.Contains("--coupon-only", StringComparer.Ordinal))
{
    NativeWallMarketItemShopCoverageTests.RunCoupon();
    return 0;
}

if (args.Contains("--shop-only", StringComparer.Ordinal))
{
    NativeShopTests.Run();
    return 0;
}

if (args.Contains("--palette-only", StringComparer.Ordinal))
{
    NativeProcessTests.RunPalette();
    Console.WriteLine("PASS native palette buffer ranges, color arithmetic and cold continuation");
    return 0;
}

if (args.Contains("--world-chocobo-only", StringComparer.Ordinal))
{
    WorldChocoboTests.Run();
    return 0;
}

if (args.Contains("--world-only", StringComparer.Ordinal))
{
    WorldRuntimeTests.Run();
    return 0;
}

if (args.Contains("--flashback-party-only", StringComparer.Ordinal))
{
    FlashbackPartyTests.Run();
    return 0;
}

if (args.Contains("--ai-experience-reward-only", StringComparer.Ordinal))
{
    NativeAiExperienceRewardTests.Run();
    return 0;
}

if (args.Contains("--reward-suppression-only", StringComparer.Ordinal))
{
    RewardSuppressionTests.Run();
    return 0;
}

if (args.Contains("--character-names-only", StringComparer.Ordinal))
{
    CharacterDisplayNameTests.Run();
    return 0;
}

if (args.Contains("--field-battle-settlements-only", StringComparer.Ordinal))
{
    FieldBattleSettlementTests.Run();
    return 0;
}

if (args.Contains("--world-encounter-modifiers-only", StringComparer.Ordinal))
{
    WorldEncounterModifierTests.Run();
    return 0;
}

if (args.Contains("--field-encounter-modifiers-only", StringComparer.Ordinal))
{
    FieldEncounterModifierTests.Run();
    return 0;
}

if (args.Contains("--native-guest-fury-only", StringComparer.Ordinal))
{
    NativeGuestFuryTests.Run();
    return 0;
}

if (args.Contains("--native-fury-only", StringComparer.Ordinal))
{
    NativeFuryTests.Run();
    return 0;
}

if (args.Contains("--native-row-change-only", StringComparer.Ordinal))
{
    NativeRowChangeTests.Run();
    return 0;
}

if (args.Contains("--enemy-withdrawal-only", StringComparer.Ordinal))
{
    NativeEnemyWithdrawalTests.Run();
    return 0;
}

if (args.Contains("--chocobo-capture-ai-only", StringComparer.Ordinal))
{
    ChocoboCaptureAiTests.Run();
    return 0;
}

if (args.Contains("--stables-radius-only", StringComparer.Ordinal))
{
    NativeStablesRadiusTests.Run();
    return 0;
}

if (args.Contains("--native-music-status-only", StringComparer.Ordinal))
{
    NativeMusicStatusTests.Run();
    return 0;
}

if (args.Contains("--color-transition-only", StringComparer.Ordinal))
{
    NativeColorTransitionTests.Run();
    return 0;
}

if (args.Contains("--corel-ai-scale-only", StringComparer.Ordinal))
{
    NativeCorelAiScaleTests.Run();
    return 0;
}

if (args.Contains("--devil-ride-ai-flags-only", StringComparer.Ordinal))
{
    DevilRideAiFlagsTests.Run();
    return 0;
}

if (args.Contains("--condition-consumers-only", StringComparer.Ordinal))
{
    NativeConditionConsumerTests.Run();
    return 0;
}

if (args.Contains("--party-ai-alias-only", StringComparer.Ordinal))
{
    PartyAiAliasTests.Run();
    return 0;
}

if (args.Contains("--world-battle-only", StringComparer.Ordinal))
{
    WorldBattleTests.Run();
    return 0;
}

if (args.Contains("--enemy-command-banks-only", StringComparer.Ordinal))
{
    EnemyCommandBankTests.Run();
    return 0;
}

if (args.Contains("--stat-accessory-equipment-only", StringComparer.Ordinal))
{
    NativeAccessoryEquipmentTests.Run();
    return 0;
}

if (args.Contains("--native-menu-equipment-only", StringComparer.Ordinal))
{
    NativeMenuEquipmentTests.Run();
    return 0;
}

if (args.Contains("--world-return-regressions-only", StringComparer.Ordinal))
{
    NativeMenuEquipmentTests.Run();
    WorldTimedEncounterTests.Run();
    return 0;
}

if (args.Contains("--world-checkpoint-only", StringComparer.Ordinal))
{
    WorldCheckpointTests.Run();
    return 0;
}

if (args.Contains("--world-audio-only", StringComparer.Ordinal))
{
    WorldAudioTests.Run();
    return 0;
}

if (args.Contains("--world-controls-only", StringComparer.Ordinal))
{
    WorldInputTests.Run();
    WorldRandomTests.Run();
    WorldSubmarineTests.Run();
    WorldChocoboTests.Run();
    return 0;
}

if (args.Contains("--world-highwind-only", StringComparer.Ordinal))
{
    WorldHighwindTests.Run();
    return 0;
}

if (args.Contains("--world-marsh-only", StringComparer.Ordinal))
{
    WorldMarshTests.Run();
    return 0;
}

if (args.Contains("--world-effects-only", StringComparer.Ordinal))
{
    WorldEffectsTests.Run();
    return 0;
}

if (args.Contains("--world-chocobo-path-only", StringComparer.Ordinal))
{
    WorldChocoboPathControlTests.Run();
    return 0;
}

if (args.Contains("--world-vehicles-only", StringComparer.Ordinal))
{
    WorldVehicleTests.Run();
    return 0;
}

if (args.Contains("--world-snow-only", StringComparer.Ordinal))
{
    WorldSnowTests.Run();
    return 0;
}

if (args.Contains("--world-party-only", StringComparer.Ordinal))
{
    WorldPartyTests.Run();
    return 0;
}

if (args.Contains("--world-departure-only", StringComparer.Ordinal))
{
    WorldDepartureTests.Run();
    return 0;
}

if (args.Contains("--field-fade-only", StringComparer.Ordinal))
{
    NativeProcessTests.RunFade();
    NativeMovieFadeTests.Run();
    NativeNewFadeTests.Run();
    NativeEntryFadeTests.Run();
    NativeQuadFadeTests.Run();
    NativeConstantFadeTests.Run();
    return 0;
}

if (args.Contains("--eyes-only", StringComparer.Ordinal))
{
    NativeEyeControlTests.Run();
    NativeFixedEyeControlTests.Run();
    NativeBlinkTests.Run();
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("origin Aura Limit gauge", OriginAuraLimitTests.Run),
    ("native Frog mutation and command eligibility", FrogStatusTests.Run),
    ("origin source status ownership", OriginBattleStatusTests.Run),
    ("origin Float posed presentation", OriginFloatPresentationTests.Run),
    ("origin Magic repetition", OriginMagicSequenceTests.Run),
    ("native save bank projection and slot integrity", NativeSaveBankTests.Run),
    ("envelope preserves both game namespaces and exact FFVIII payload", EnvelopeRoundTrip),
    ("envelope corruption and missing active game rejected", InvalidEnvelope),
    ("world switch requires save permission and prepares before commit", SwitchGuard),
    ("world revisits resume each independent continuation", ResumeBothWorlds),
    ("native field color fade and cold continuation", NativeProcessTests.RunFade),
    ("native movie field hiding and cold continuation", NativeMovieFadeTests.Run),
    ("native new-fade endpoint and cold continuation", NativeNewFadeTests.Run),
    ("native entry fade table and cold continuation", NativeEntryFadeTests.Run),
    ("native fullscreen quad fade and cold continuation", NativeQuadFadeTests.Run),
    ("native field item acquisition and cold continuation", NativeItemTests.Run),
    ("native gil acquisition and cold continuation", NativeGilTests.Run),
    ("native fixed animation move and cold continuation", NativeProcessTests.RunFixedAnimationMove),
    ("native fixed facing move and cold continuation", NativeProcessTests.RunFixedFacingMove),
    ("native movement probe triangle carry and cold continuation", NativeMovementTests.RunProbeCarry),
    ("native movie camera gate and cold continuation", NativeProcessTests.RunMovieCameraGate),
    ("native arrow suppression and cold continuation", NativeArrowControlTests.Run),
    ("native movie camera default service and cold continuation", NativeMovieCameraServiceTests.Run),
    ("native instant entity facing and cold continuation", NativeProcessTests.RunImmediateFace),
    ("native turn wait and cold continuation", NativeTurnWaitTests.Run),
    ("native party facing and cold continuation", NativePartyTurnTests.Run),
    ("native party recovery and cold continuation", NativePartyRecoveryTests.Run),
    ("native all-character recovery and cold continuation", NativePartyRecoveryTests.RunAll),
    ("native wait-free window size and cold continuation", NativeProcessTests.RunWindowSizeWhenFree),
    ("native dynamic actor approach and cold continuation", NativeActorApproachTests.Run),
    ("native actor movement facing and cold continuation", NativeActorApproachTests.RunMovementFacing),
    ("native camera shake and cold continuation", NativeCameraShakeTests.Run),
    ("native player entry movement and cold continuation", NativePlayerEntryTests.Run),
    ("native player slot selection and cold continuation", NativePlayerSelectionTests.Run),
    ("native preload request and cold continuation", NativePreloadTests.Run),
    ("native previous map and cold continuation", NativePreviousMapTests.Run),
    ("native eye disable owner and cold continuation", NativeEyeControlTests.Run),
    ("native automatic blink eligibility and cold continuation", NativeBlinkTests.Run),
    ("native materia acquisition and cold continuation", NativeMateriaTests.Run),
    ("native equipment removal and cold continuation", NativeEquipmentRemovalTests.Run),
    ("native autoplay lifecycle and standalone continuation", NativeAutoPlayTests.Run),
    ("native generic autoplay discovery", NativeAutoPlayDiscoveryTests.Run),
    ("native autonomous spatial frontiers", NativeAutoPlaySpatialTests.Run),
    ("native field entry readiness", AutoPlayEntryReadinessTests.Run),
    ("native same-field spatial navigation ownership", AutoPlaySpatialNavigationTests.Run),
    ("native autoplay choice workflow", AutoPlayChoiceWorkflowTests.Run),
    ("native autoplay scripted input", NativeAutoPlayScriptedInputTests.Run),
    ("native autoplay host read dependencies", NativeAutoPlayHostReadTests.Run),
    ("native autoplay physical availability guards", NativeAutoPlayPhysicalAvailabilityTests.Run),
    ("native autoplay unexecuted frontier priority", NativeAutoPlayPolicyPlanningTests.Run),
    ("native autoplay gateway topology", AutoPlayGatewayTopologyTests.Run),
    ("native world semantic completion availability", AutoPlayWorldSemanticAvailabilityTests.Run),
    ("native world intrinsic control", AutoPlayWorldControlTests.Run),
    ("native world vehicle-to-rider route", AutoPlayWorldModeNavigationTests.Run),
    ("native world post-exit goal", AutoPlayWorldModeFootTests.Run),
    ("native autoplay destination novelty", NativeAutoPlayDestinationPreferenceTests.Run),
    ("native autoplay field destination goals", AutoPlayFieldDestinationGoalTests.Run),
    ("native autoplay world input preview", AutoPlayWorldMovementTests.Run),
    ("native autoplay world frontier catalog", AutoPlayWorldFrontierCatalogTests.Run),
    ("native autoplay world navigation", AutoPlayWorldNavigationTests.Run),
    ("native autoplay world policy lifecycle", AutoPlayWorldPolicyTests.Run),
    ("native autoplay pre-battle world checkpoints", AutoPlayWorldBattleCheckpointTests.Run),
    ("native actor reaction fixed-motion ownership", NativeReactionMotionTests.Run),
    ("native actor reaction sound gain ownership", NativeReactionSoundGainTests.Run),
    ("native delayed actor hitmark ownership", NativeActorHitmarkTests.Run),
    ("native autoplay global visit guards", AutoPlayVisitGuardTests.Run),
    ("native autoplay transition progress guard", NativeAutoPlayTransitionGuardTests.Run),
    ("native autoplay successful target completion", NativeAutoPlayTests.RunCompletion),
    ("native autoplay observed church target", NativeAutoPlayTests.RunGoal),
    ("native autoplay crowd escape and pure previews", NativeAutoPlayCrowdTests.Run),
    ("native autoplay occupied waypoint and pure previews", NativeAutoPlayCrowdTests.RunOccupiedWaypoint),
    ("native run button projection and pending input ownership", NativeRunInputTests.Run),
    ("native world encounters and battle return", WorldBattleTests.Run),
    ("native world entry, facing and animation ownership", WorldRuntimeTests.Run),
    ("native scripted world transport continuation", WorldTransportContinuationTests.Run),
    ("original standalone world movement and cold continuation", WorldCheckpointTests.Run),
    ("native surface vehicles and colored Chocobos", WorldVehicleTests.Run),
    ("native world PHS party synchronization", WorldPartyTests.Run),
    ("native snow world wind and markers", WorldSnowTests.Run),
    ("native Midgar Zolom pursuit and collision", WorldMarshTests.Run),
    ("native timed underwater encounter and world return", WorldTimedEncounterTests.Run),
    ("native submerged submarine controls and terrain", WorldSubmarineTests.Run),
    ("native Highwind flight and landing", WorldHighwindTests.Run),
    ("native world particle simulation and presentation", WorldEffectsTests.Run),
    ("native world audio commands and cold continuation", WorldAudioTests.Run),
    ("FFVII native ordinary yellow Chocobo transport", WorldChocoboTests.Run),
    ("native Chocobo path control and cold continuation", WorldChocoboPathControlTests.Run),
    ("FFVII native world shared random stream", WorldRandomTests.Run),
    ("native world input displacement and terrain arithmetic", WorldInputTests.Run),
    ("native autoplay event observations and coverage ledger", NativeEventCoverageTests.Run),
    ("native optional-area frontier and initialized field ownership", NativeTourFrontierTests.Run),
    ("native window parameter binding and numeric cursor", NativeWindowParameterTests.Run),
    ("native window defaults and legacy ownership", NativeWindowDefaultsTests.Run),
    ("native ordered battle inventory", NativeBattleInventoryTests.Run),
    ("native inventory slot ownership", NativeInventorySlotTests.Run),
    ("native post-initialization party presentation", NativePartyPresentationTests.Run),
    ("native shop autoplay cadence and clock ownership", NativeShopAutoPlayTests.Run),
    ("native optional inn production tour and return receipts", NativeOptionalInnTourTests.Run),
    ("native optional item shop production tour and return receipts", NativeOptionalInnTourTests.RunItemShop),
    ("native optional materia production tour and return receipts", NativeOptionalInnTourTests.RunMateria),
    ("native optional restaurant production tour and coupon availability", NativeOptionalInnTourTests.RunRestaurant),
    ("native optional coupon production tour and changed information", NativeOptionalInnTourTests.RunCoupon),
    ("native optional abandoned item store production tour", NativeOptionalInnTourTests.RunAbandonedItemStore),
    ("native main-street local subset and frontiers", NativeWallMarketMainStreetCoverageTests.Run),
    ("native main-street nested tour entry and gate", NativeWallMarketMainStreetCoverageTests.RunNested),
    ("native autoplay rail crossing and pure previews", NativeAutoPlayRailTests.Run),
    ("native train graveyard route and cold continuation", NativeTrainGraveyardTests.Run),
    ("native autoplay triangle handoff and pure previews", NativeAutoPlayBridgeTests.Run),
    ("native rejected movement binding and cold continuation", NativeMovementBindingTests.Run),
    ("native movement authored contracts", NativeMovementTests.Run),
    ("native animation authored contracts", NativeAnimationTests.Run),
    ("native dialogue continuation contracts", NativeDialogueTests.Run),
    ("native message lifecycle and cold continuation", NativeMessageLifecycleTests.Run),
    ("native queued message ownership and cold continuation", NativeQueuedMessageTests.Run),
    ("installed native message scroll and cold continuation", InstalledMessageScrollTests.Run),
    ("native turn, camera and global process contracts", NativeProcessTests.Run),
    ("native global clock and timer contracts", NativeClockTests.Run),
    ("native special window timer metadata", NativeProcessTests.RunSpecialWindow),
    ("native camera focus and tracking contracts", NativeProcessTests.RunViewOffset),
    ("native character growth contracts", NativeGrowthTests.Run),
    ("native naming join growth", NativeNamingGrowthTests.Run),
    ("native naming lifecycle and deferred statistics", NativeNamingLifecycleTests.Run),
    ("native command materia grants", NativeBattleCommandGrantTests.Run),
    ("native command passive statistics and admission", NativeCommandProjectionTests.Run),
    ("native debug victory and exactly-once rewards", NativeBattleTests.Run),
    ("native mutable AI experience and Gil rewards", NativeAiExperienceRewardTests.Run),
    ("FFVII Turn Based scheduling and cold continuation", NativeBattleTests.RunTurnBased),
    ("FFVII native battle speed and cold continuation", NativeBattleSpeedTests.Run),
    ("FFVII prepared world field departure", WorldDepartureTests.Run),
    ("FFVII Turn Based diagnostic encounter continuation", InstalledOpeningTests.RunTurnAdvanceContinuation),
    ("FFVII Order and PHS native party continuation", NativePartyRecoveryTests.RunMenuParty),
    ("FFVII native Equip transactions and cold continuation", NativeMenuEquipmentTests.Run),
    ("FFVII stat accessory equipment and cold continuation", NativeAccessoryEquipmentTests.Run),
    ("FFVII native Limit level selection and cold continuation", NativeLimitMenuTests.Run),
    ("FFVII native field Item and Magic recovery", NativeFieldRecoveryTests.Run),
    ("native MP Turbo materia in battle", NativeBattleMpTurboTests.Run),
    ("native later party battle roster", NativeBattlePartyRosterTests.Run),
    ("native Ruby Weapon phase", NativeBattleRubyPhaseTests.Run),
    ("native story boss phases", NativeBattleStoryBossTests.Run),
    ("native Hundred Gunner formation chain", NativeBattleFormationChainTests.Run),
    ("native Shinra Sample H0512", NativeBattleShinraTests.RunSample),
    ("native Shinra elevator bosses", NativeBattleShinraTests.RunElevator),
    ("native Shinra Rufus", NativeBattleShinraTests.RunRufus),
    ("installed opening arena and battle actor resources", NativeBattleTests.RunOpeningRoster),
    ("installed Hammer Blaster support-model packed frames", NativeBattleTests.RunHammerBlasterClips),
    ("native opening enemy command banks and Grasslands continuation", EnemyCommandBankTests.Run),
    ("native opening combat damage", NativeBattleDamageTests.Run),
    ("native opening flashback party storage and equipment", FlashbackPartyTests.Run),
    ("native opening request reward suppression", RewardSuppressionTests.Run),
    ("native opening character display names", CharacterDisplayNameTests.Run),
    ("durable field battle settlements and legacy continuation", FieldBattleSettlementTests.Run),
    ("native background layer depth state and field reset", NativeBackgroundDepthTests.Run),
    ("native background scroll phase and resource restoration", NativeBackgroundScrollTests.Run),
    ("native water initialization cache and cold continuation", NativeWaterEffectTests.Run),
    ("native transparency queue and cold continuation", NativeTransparencyTests.Run),
    ("native model completion queue and cold continuation", NativeModelCompletionTests.Run),
    ("native player movement animation configuration", NativeMovementAnimationTests.Run),
    ("native window number state and reset", NativeWindowNumberTests.Run),
    ("native shared message entity ownership", NativeSharedMessageTests.Run),
    ("native field encounter materia modifiers and cold continuation", FieldEncounterModifierTests.Run),
    ("native guest fury ownership and cold continuation", NativeGuestFuryTests.Run),
    ("native fury producer, opposition and cold continuation", NativeFuryTests.Run),
    ("native enemy withdrawal effect and cold continuation", NativeEnemyWithdrawalTests.Run),
    ("native row change effect and cold continuation", NativeRowChangeTests.Run),
    ("native chocobo capture AI mask timing and cold continuation", ChocoboCaptureAiTests.Run),
    ("native stable word-radius initialization and cold continuation", NativeStablesRadiusTests.Run),
    ("native music status, finite timing and local changes", NativeMusicStatusTests.Run),
    ("native model color transition", NativeColorTransitionTests.Run),
    ("native covered-character byte and Grasslands counters", DevilRideAiFlagsTests.Run),
    ("native size byte and original Corel growth counters", NativeCorelAiScaleTests.Run),
    ("native opening condition costs and status immunity", NativeConditionConsumerTests.Run),
    ("native opening party AI aliases and counters", PartyAiAliasTests.Run),
    ("native opening action transactions and Limit reel", NativeBattleActionTests.Run),
    ("native Cross-slash action and settlement", NativeBattleCrossSlashTests.Run),
    ("native Mindblow action and settlement", NativeBattleMindblowTests.Run),
    ("native Choco/Mog materia and camera", NativeBattleSummonTests.Run),
    ("native Hell House transformation fade", NativeBattleHellHouseTests.Run),
    ("native battle sound dispatch and cold continuation", NativeBattleSoundTests.Run),
    ("native battle actor model callbacks", NativeBattleActorModelTests.Run),
    ("native Emerald Weapon linked actor models", NativeBattleActorModelTests.RunEmeraldLinkedModels),
    ("native guest limit contact continuation", NativeGuestLimitTests.Run),
    ("guest selected continuation and deferred host responses", GuestContinuationTests.Run),
    ("guest composite action occurrences", GuestCompositeActionTests.Run),
    ("mixed battle element and status conversion", GuestBattleConversionTests.Run),
    ("native destroyed battle effect and cold continuation", NativeBattleDestroyedTests.Run),
    ("native battle camera archive and action selection", NativeBattleCameraTests.Run),
    ("native field encounter-step contracts", NativeEncounterTests.Run),
    ("native field audio commands and continuation", NativeAudioTests.Run),
    ("native field initializer audio binding", NativeAudioTransitionTests.Run),
    ("native pending transition and atomic field publication", NativePendingTransitionTests.Run),
    ("native partial tick prefix and cold continuation", NativePartialTickTests.Run),
    ("native music gain fade and cold continuation", NativeMusicFadeTests.Run),
    ("native resident sound gain ramp and cold continuation", NativeSoundRampTests.Run),
    ("native resident sound pan and cold continuation", NativeSoundPanTests.Run),
    ("native resident sound frequency ramp and cold continuation", NativeSoundFrequencyRampTests.Run),
    ("native resident sound frequency and cold continuation", NativeSoundFrequencyTests.Run),
    ("standalone FFVII cold checkpoint continuation", NativeCheckpointTests.Run),
    ("native procedural model color and visibility contracts", NativeModelEffectTests.Run),
    ("native model color offset and cold continuation", NativeColorOffsetTests.Run),
    ("native line talk physical facing and preview purity", NativeLineTalkInputTests.Run),
    ("native field catalog and physical event continuation", NativeFieldEventCatalogTests.Run),
    ("native road catalog and physical handoff continuation", NativeRoadCoverageTests.Run),
    ("native park catalog and physical scene continuation", NativeParkCoverageTests.Run),
    ("native market catalog and unvisited room frontier", NativeWallMarketCoverageTests.Run),
    ("native inn catalog and physical visit", NativeWallMarketInnCoverageTests.Run),
    ("native item-shop catalog and physical visit", NativeWallMarketItemShopCoverageTests.Run),
    ("native materia-room catalog and physical visit", NativeWallMarketMateriaCoverageTests.Run),
    ("native boutique researched subset and frontier", NativeWallMarketBoutiqueCoverageTests.Run),
    ("native room reset ownership and cold coverage", NativeRoomResetCoverageTests.Run),
    ("native part visibility and cold continuation", NativePartVisibilityTests.Run),
    ("native interactive ladder continuation contracts", NativeLadderTests.Run),
    ("native jump and actor position query contracts", NativeJumpTests.Run),
    ("installed native movement contracts", NativeMovementTests.RunInstalled),
    ("installed New Game and cold continuation", InstalledSession),
    ("installed opening guest instant victory", InstalledOpeningTests.RunGuestInstantVictory),
    ("installed opening deferred battle handoff", InstalledOpeningTests.RunDeferredBattle),
    ("installed opening playable battle handoff", InstalledOpeningTests.RunPlayableBattle),
    ("installed opening full combat matrix", InstalledOpeningCombatMatrixTests.Run),
    ("installed opening and cold process continuation", InstalledOpeningTests.Run)
};
var selected = tests.Where(test => args.Length == 0 || test.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();
int failed = 0;
foreach (var test in selected)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + error);
    }
}
Console.WriteLine($"{selected.Length - failed}/{selected.Length} groups passed");
return failed == 0 ? 0 : 1;

static Ff7SessionSave Fixture(long tick = 0)
{
    FieldProgram program = new([0, 0x5F, 0x12, 1], [new ushort[32]]);
    FieldMachine machine = new(program);
    machine.Initialize();
    return new Ff7SessionSave
    {
        ResourceProfile = "fixture",
        Language = "en",
        FieldName = "fixture",
        Progress = new Ff7Progress { InitialKernelData = [1], Characters = [new Ff7CharacterState(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new Ff7InventorySlot(0, 2)], Materia = [], Gil = 240 },
        Vm = machine.Capture() with { Tick = tick },
        Process = new Ff7FieldProcess { Tick = tick }
    };
}

static void EnvelopeRoundTrip()
{
    byte[] ff8 = [0x4D, 0x46, 0x46, 0x38, 1, 2, 3, 255];
    CombinedSessionSave save = new() { ActiveGame = SessionGame.Ff7, Ff8Payload = ff8, Ff8PayloadFormat = "fixture-ff8", Ff7 = Fixture(18), Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "standard-field-squall")] };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, save);
    stream.Position = 0;
    Check(CombinedSessionCodec.HasSignature(stream) && stream.Position == 0, "Signature inspection consumed input.");
    CombinedSessionSave restored = CombinedSessionCodec.Read(stream);
    Check(restored.Ff8Payload!.SequenceEqual(ff8), "FFVIII payload changed.");
    Check(restored.Ff7!.Vm.Tick == 18 && restored.Ff7.Progress.Gil == 240 && restored.Ff7.Progress.Inventory[0].Quantity == 2, "FFVII progression or PC clock changed.");
    Check(restored.Mappings.Single().AppearanceGame == SessionGame.Ff8, "Appearance identity was lost.");
    for (int appearance = 0; appearance <= 10; appearance++)
    {
        CharacterAppearanceMapping[] mappings = Enumerable.Range(0, 9).Select(story => new CharacterAppearanceMapping(SessionGame.Ff7, story, SessionGame.Ff8, appearance, "fixture-general-profile")).ToArray();
        using MemoryStream mappedStream = new();
        CombinedSessionCodec.Write(mappedStream, save with { Mappings = mappings });
        mappedStream.Position = 0;
        CombinedSessionSave mapped = CombinedSessionCodec.Read(mappedStream);
        Check(mapped.Mappings.SequenceEqual(mappings), "A general character association changed during its codec round trip.");
        Check(mapped.Ff8Payload!.SequenceEqual(ff8) && JsonSerializer.Serialize(mapped.Ff7) == JsonSerializer.Serialize(save.Ff7), "An appearance association changed native progress.");
    }
}

static void InvalidEnvelope()
{
    Throws<InvalidDataException>(() => (new CombinedSessionSave { ActiveGame = SessionGame.Ff7 }).Validate());
    CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = Fixture() };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, source);
    byte[] corrupt = stream.ToArray();
    corrupt[^1] ^= 127;
    Throws<InvalidDataException>(() => CombinedSessionCodec.Read(new MemoryStream(corrupt)));
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 9, SessionGame.Ff8, 0, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 11, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff8, 0, SessionGame.Ff7, 0, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "fixture"), new(SessionGame.Ff7, 0, SessionGame.Ff8, 1, "fixture")] }).Validate());
}

static void SwitchGuard()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [10], Ff8PayloadFormat = "fixture" });
    int created = 0;
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(false), [11], "fixture", () => { created++; return Fixture(); }, _ => { }));
    Check(created == 0 && coordinator.State.ActiveGame == SessionGame.Ff8, "Denied switch created or advanced a session.");
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(true), [11], "fixture", FixtureFactory, _ => throw new InvalidOperationException("unavailable destination")));
    Check(coordinator.State.ActiveGame == SessionGame.Ff8 && coordinator.State.Ff8Payload![0] == 10 && coordinator.State.Ff7 is null, "Failed preparation committed a partial world switch.");
}

static Ff7SessionSave FixtureFactory()
{
    return Fixture();
}

static void ResumeBothWorlds()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [1], Ff8PayloadFormat = "fixture" });
    int newGames = 0;
    coordinator.ToFf7(new(true), [2], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 0, "First visit did not start New Game."));
    coordinator.ToFf8(new(true), Fixture(123), (payload, _) => Check(payload[0] == 2, "FFVIII did not resume its outgoing state."));
    coordinator.ToFf7(new(true), [3], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 123, "FFVII revisit replayed New Game."));
    Check(newGames == 1 && coordinator.State.Ff8Payload![0] == 3 && coordinator.State.Ff7!.Vm.Tick == 123, "Independent world continuations changed.");
}

static void InstalledSession()
{
    string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
    if (string.IsNullOrWhiteSpace(root))
    {
        Console.WriteLine("SKIP installed New Game: MEMORIA_FF7_GAME_DIR is unset.");
        return;
    }
    Ff7Installation installation = Ff7Installation.Discover(root);
    using Ff7Session original = Ff7Session.NewGame(installation);
    Ff7SessionSave snapshot = original.Capture();
    Console.WriteLine($"initial-field={original.Field.Name} actors={original.Host.Process.Actors.Length} party={string.Join(',', original.Progress.Party)} movie={original.Host.Process.Movie?.PreparedId}");
    Check(original.Field.Name == "md1stin" && snapshot.Vm.PersistentVariables[768] == 1 && original.Progress.Party.SequenceEqual(new[] { 0, 255, 255 }), "Native cold New Game owners were not applied.");
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = snapshot });
    stream.Position = 0;
    using Ff7Session restored = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
    original.AdvanceTick();
    restored.AdvanceTick();
    Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Installed New Game cold restore changed the next tick.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
