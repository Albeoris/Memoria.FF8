using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "--scenario")
            {
                string selectedGameDirectory = args.Length >= 3 ? args[2] : @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
                Dictionary<string, Action> scenarios = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["causal-replay"] = CausalReplayContract,
                    ["message-variable"] = NativeMessageVariableContract,
                    ["native-event-random"] = () => NativeEventRandomContract(selectedGameDirectory),
                    ["native-party-hold"] = () => NativePartyHoldContract(selectedGameDirectory),
                    ["native-disc-save"] = () => NativeDiscSaveContract(selectedGameDirectory),
                    ["native-party-menu"] = () => NativePartyMenuContract(selectedGameDirectory),
                    ["native-party-slots"] = () => NativePartySlotContract(selectedGameDirectory),
                    ["native-train-control-byte"] = () => NativeTrainControlByteContract(selectedGameDirectory),
                    ["native-base-animation"] = NativeBaseAnimationContract,
                    ["native-animation-backup"] = () => NativeAnimationBackupContract(selectedGameDirectory),
                    ["native-actor-visibility"] = () => NativeActorVisibilityContract(selectedGameDirectory),
                    ["native-request-physical-alias"] = () => NativeRequestPhysicalAliasContract(selectedGameDirectory),
                    ["native-world-entry-memory"] = () => NativeWorldEntryMemoryContract(selectedGameDirectory),
                    ["native-maccel"] = NativeMovementAccelerationContract,
                    ["native-model-door"] = () => NativeModelDoorContract(selectedGameDirectory),
                    ["native-sethp"] = () => NativeSetHitPointsContract(selectedGameDirectory),
                    ["native-rest-membership"] = () => NativeRestAndMembershipContract(selectedGameDirectory),
                    ["native-split-control-caller"] = () => NativeSplitControlCallerContract(selectedGameDirectory),
                    ["native-field-party-binding"] = () => NativeCurrentFieldPartyBindingContract(selectedGameDirectory),
                    ["native-init-trace"] = () => NativeInitializationTraceContract(selectedGameDirectory),
                    ["native-junction"] = () => NativeDreamJunctionContract(selectedGameDirectory),
                    ["native-economy"] = () => NativeEconomyContract(selectedGameDirectory),
                    ["native-salary-payout"] = () => NativeSalaryPayoutContract(selectedGameDirectory),
                    ["native-jump-budget"] = NativeJumpBudgetContract,
                    ["map-jump-origin"] = NativeMapJumpOriginContract,
                    ["native-disc-jump"] = () => NativeDiscJumpContract(selectedGameDirectory),
                    ["linear-offset-start"] = NativeLinearOffsetExplicitStartContract,
                    ["cosine-offset-start"] = NativeCosineOffsetExplicitStartContract,
                    ["physical-motion-words"] = NativePhysicalMotionWordContract,
                    ["field-media"] = NativeFieldMediaContract,
                    ["global-sound"] = NativeGlobalSoundContract,
                    ["salary-presentation-effects"] = NativeSalaryPresentationEffectsContract,
                    ["exam-timer"] = NativeExamTimerContract,
                    ["native-process-clock"] = NativeProcessClockContract,
                    ["native-field-shake"] = () => NativeFieldShakeContract(selectedGameDirectory),
                    ["native-direct-scroll"] = NativeCurrentDirectScrollContract,
                    ["native-scalar-memory"] = NativeScalarMemoryContract,
                    ["native-scalar-calculation"] = NativeScalarCalculationContract,
                    ["linear-scroll"] = () => NativeLinearScrollAndSyncContract(selectedGameDirectory),
                    ["linear-scroll-zero"] = NativeLayerZeroLinearScrollContract,
                    ["scroll-related"] = () =>
                    {
                        NativeDirectScrollAndBoundary(selectedGameDirectory);
                        NativePartyScrollAndBoundary(selectedGameDirectory);
                        NativeCosineScrollAndSyncBoundary(selectedGameDirectory);
                    },
                    ["dialogue-channels"] = DialogueChannelContract,
                    ["native-script-move"] = () => NativeScriptMovementAndBoundary(selectedGameDirectory),
                    ["native-rfmove"] = () =>
                    {
                        NativeAsyncRfmMoveContract(selectedGameDirectory);
                        NativeScriptMovementAndBoundary(selectedGameDirectory);
                    },
                    ["native-player-contacts"] = () => NativePlayerGroundContactContract(selectedGameDirectory),
                    ["world-transition"] = () => NativeWorldTransitionTransaction(selectedGameDirectory),
                    ["minimal-menu"] = () => MinimalMenuCompletionContract(selectedGameDirectory),
                    ["party-query"] = () => NativePartyQueryContract(selectedGameDirectory),
                    ["root-translation"] = () => NativeRootTranslationAndBoundary(selectedGameDirectory),
                    ["native-player-retry"] = () => NativePlayerAcceptedRetryContract(selectedGameDirectory),
                    ["background-clear"] = () => NativeBackgroundClearContract(selectedGameDirectory),
                    ["background-shade-off"] = () => NativeBackgroundShadeOffContract(selectedGameDirectory),
                    ["background-shade-stop"] = () => NativeBackgroundShadeStopContract(selectedGameDirectory),
                    ["dying"] = () => NativeDyingContract(selectedGameDirectory),
                    ["reset-gf"] = () => NativeGuardianForceResetContract(selectedGameDirectory),
                    ["dress"] = () => NativeDressContract(selectedGameDirectory),
                    ["getcard-saveenable"] = () => NativeCardAndSaveEnableContract(selectedGameDirectory),
                    ["native-howmanycard"] = () => NativeHowManyCardContract(selectedGameDirectory),
                    ["native-card-location"] = () => NativeCardLocationContract(selectedGameDirectory),
                    ["native-party-menu-enable"] = NativePartyMenuEnableContract,
                    ["native-control-readiness"] = () => NativeControlReadinessContract(selectedGameDirectory),
                    ["key-on"] = () => NativeKeyOnContract(selectedGameDirectory),
                    ["key-scan"] = () => NativeKeyScanContract(selectedGameDirectory),
                    ["key-scan2"] = () => NativeKeyScan2Contract(selectedGameDirectory),
                    ["native-polygon-color"] = () => NativePolygonColorContract(selectedGameDirectory),
                    ["terminal-commands"] = NativeTerminalCommandsContract,
                    ["movie-timer"] = NativeMovieTimerClockContract,
                    ["car-commands"] = () => NativeCarCommandsContract(selectedGameDirectory),
                    ["actor-scroll"] = NativeActorScrollContract,
                    ["actor-cosine-scroll"] = NativeActorCosineScrollContract,
                    ["actor-linear-scroll"] = NativeActorLinearScrollContract,
                    ["party-linear-scroll"] = NativePartyLinearScrollContract,
                    ["party-cosine-scroll"] = NativePartyCosineScrollContract,
                    ["set-placement"] = NativeSetPlacementContract,
                    ["rcmove"] = NativeRcMoveContract,
                    ["door-line-toggle"] = NativeDoorLineToggleContract,
                    ["door-operand-suspension"] = NativeDoorOperandSuspensionContract,
                    ["native-door-raw-request"] = () => NativeDoorRawRequestContract(selectedGameDirectory),
                    ["native-door-request-wait"] = () => NativeDoorRequestWaitContract(selectedGameDirectory),
                    ["native-door-default-frame"] = () => NativeDoorDefaultFrameContract(selectedGameDirectory),
                    ["native-movie-mask-control"] = () => NativeMovieMaskControlContract(selectedGameDirectory),
                    ["native-music-status"] = () => NativeMusicStatusContract(selectedGameDirectory),
                    ["native-actor-info-unknown"] = NativeActorInfoUnknownReadContract,
                    ["native-actor-info"] = () => NativeActorInfoContract(selectedGameDirectory),
                    ["native-movement-request"] = NativeMovementRequestContract,
                    ["native-mode0-request-transfer"] = NativeModeZeroRequestTransferContract,
                    ["native-single-backup-overwrite"] = NativeSingleBackupOverwriteContract,
                    ["native-move-cancel"] = NativeMovementCancellationContract,
                    ["native-push-radius"] = NativePushRadiusContract,
                    ["native-empty-model-move"] = NativeEmptyModelMovementContract,
                    ["native-dialogue-busy"] = NativeDialogueBusyContract,
                    ["native-only-movement"] = NativeOnlyMovementContract,
                    ["native-join-saved-bindings"] = NativeJoinSavedBindingsContract,
                    ["native-facedir"] = NativeFacePointContract,
                    ["native-facedirp"] = NativePartyFaceContract,
                    ["native-ranimeloop-yield"] = NativeAnimationLoopYieldContract,
                    ["native-movement-animation"] = () => NativeMovementAnimationContract(selectedGameDirectory),
                    ["native-face-sync"] = NativeFaceSyncContract,
                    ["native-rfacedir"] = NativeAsyncFacePointContract,
                    ["native-rfacedira"] = () => NativeAsyncFaceActorContract(selectedGameDirectory),
                    ["native-ladder-mode3"] = () => NativeLadderMode3Contract(selectedGameDirectory),
                    ["native-ladder-short-clips"] = NativeLadderShortClipContract,
                    ["native-particle-set"] = () => NativeParticleSetContract(selectedGameDirectory),
                    ["native-rfacedir-off"] = NativeAsyncFaceReturnContract,
                    ["native-face-angles"] = FaceDirectionAnglesAndResume,
                    ["native-dirp"] = () => NativeDirectionPointContract(selectedGameDirectory),
                    ["native-join-direction-limit"] = () => NativeJoinDirectionLimitContract(selectedGameDirectory),
                    ["native-join-triangle-carry"] = () => NativeJoinTriangleCarryContract(selectedGameDirectory),
                    ["native-meaningful-contact"] = NativeMeaningfulContactContract,
                    ["native-rmovea"] = NativeRmMoveActorContract,
                    ["positive-cosine-turn"] = NativePositiveCosineTurnContract,
                    ["negative-cosine-turn"] = NativeNegativeCosineTurnContract,
                    ["salary-field-movement"] = () => NativeSalaryFieldMovementContract(selectedGameDirectory),
                    ["seed-rank"] = () => NativeSeeDContract(selectedGameDirectory),
                    ["party-actor-direction"] = NativePartyActorDirectionContract,
                    ["player-script-handoff"] = () => NativePlayerScriptHandoffContract(selectedGameDirectory),
                    ["empty-party-model"] = NativeEmptyPartyModelContract,
                    ["vibration"] = NativeVibrationContract,
                    ["actor-shadow"] = NativeActorShadowContract,
                    ["actor-shadow-form"] = NativeActorShadowFormContract,
                    ["footstep-pair"] = NativeFootstepPairContract,
                    ["footstep-off-all"] = NativeFootstepOffAllContract,
                    ["draw-point-setup"] = () => NativeDrawPointSetupContract(selectedGameDirectory),
                    ["native-drawpoint-source-byte"] = () => NativeDrawPointSourceByteContract(selectedGameDirectory),
                    ["native-menu-drawpoint-mask"] = () => NativeMenuDrawPointMaskContract(selectedGameDirectory),
                    ["native-drawpoint-no-eligible"] = () => NativeDrawPointNoEligibleContract(selectedGameDirectory),
                    ["draw-point-allocated-byte"] = NativeDrawPointAllocatedByteContract,
                    ["music-position"] = () => NativeMusicPositionContract(selectedGameDirectory),
                    ["music-load"] = NativeMusicLoadContract,
                    ["native-field-fade"] = NativeFadeNoneContract,
                    ["native-fade-player-gate"] = NativeFadeBlackPlayerGateContract,
                    ["native-fade-black-join"] = NativeFadeBlackScriptJoinContract,
                    ["cosine-scroll-zero"] = NativeLayerZeroCosineScrollContract,
                    ["native-jump"] = NativeJumpContract,
                    ["native-jump3"] = NativeJump3Contract,
                    ["native-party-jump"] = () => NativePartyJumpContract(selectedGameDirectory),
                    ["native-party-move"] = () => NativePartyMoveContract(selectedGameDirectory),
                    ["native-ladder"] = NativeLadderContract,
                    ["native-ladder-reverse"] = NativeLadderReverseAndRestoreContract,
                    ["native-ladder-followers"] = NativeLadderFollowerContract,
                    ["native-jump-request"] = NativeJumpMovementRequestContract,
                    ["native-cmove"] = NativeHeldFacingCmoveContract,
                    ["native-change-party"] = NativeChangePartyContract,
                    ["party-follow"] = NativePartyBackgroundScrollContract,
                    ["native-party-linear-turn"] = NativePartyLinearTurnContract,
                    ["native-directed-linear-turn"] = NativeDirectedLinearTurnContract,
                    ["native-base-animation-allocation"] = () => NativeBaseAnimationAllocationContract(selectedGameDirectory),
                    ["native-push-preemption"] = NativeEmptyPushPreemptionContract,
                    ["push-contact"] = () => GardenNativePushContactBoundary(selectedGameDirectory),
                    ["follow-on"] = NativeFollowOnContract,
                    ["music-fade"] = NativeMusicFadeContract,
                    ["music-volume-sync"] = NativeMusicVolumeSyncContract,
                    ["scroll-ratio"] = NativeScrollRatioContract,
                    ["all-sound-volume"] = NativeAllSoundVolumeContract,
                    ["sound-volume"] = NativeSoundVolumeContract,
                    ["sound-position"] = NativeSoundPositionContract,
                    ["battle-mode"] = NativeBattleModeContract,
                    ["sound-initialization"] = () => NativeSoundInitializationContract(selectedGameDirectory),
                    ["sound-stop"] = NativeSoundStopContract,
                    ["native-talk"] = NativeTalkSelectionContract,
                    ["previous-field"] = NativePreviousFieldContract,
                    ["previous-world-entity"] = NativePreviousWorldEntityContract,
                    ["native-sloped-line"] = () => NativeSlopedLineContract(selectedGameDirectory),
                    ["native-split-saved-bindings"] = NativeSplitSavedBindingsContract,
                    ["native-sloped-split"] = () =>
                    {
                        NativeSoloSlopedSplitContract(selectedGameDirectory);
                        GardenNativeSplitBoundary(selectedGameDirectory);
                    },
                    ["mlimit"] = () => NativeMovementLimitAndBoundary(selectedGameDirectory),
                    ["background-loop"] = () => NativeBackgroundLoopAndBoundary(selectedGameDirectory)
                };
                if (!scenarios.TryGetValue(args[1], out Action? run))
                {
                    throw new ArgumentException($"Unknown scenario '{args[1]}'. Available: {string.Join(", ", scenarios.Keys)}.");
                }
                run();
                Console.WriteLine($"Event scenario {args[1]} passed.");
                return 0;
            }
            WaitAndCorruptInput();
            NativeExamTimerContract();
            NativeScalarMemoryContract();
            DialogueChannelContract();
            LineContactAndResume();
            MovieAxisAndFaultResume();
            ArrivalInitializationOrder();
            BackgroundLoopResume();
            FaceDirectionAnglesAndResume();
            string gameDirectory = args.Length == 0 ? @"C:\Steam\steamapps\common\FINAL FANTASY VIII" : args[0];
            InitialInputDirectionAndRestore(gameDirectory);
            NativeMovementLimitAndBoundary(gameDirectory);
            NativeBackgroundLoopAndBoundary(gameDirectory);
            NativeScriptMovementAndBoundary(gameDirectory);
            NativeAsyncRfmMoveContract(gameDirectory);
            NativePlayerGroundContactContract(gameDirectory);
            NativeWorldTransitionTransaction(gameDirectory);
            MinimalMenuCompletionContract(gameDirectory);
            NativePartyQueryContract(gameDirectory);
            NativeRootTranslationAndBoundary(gameDirectory);
            NativePlayerAcceptedRetryContract(gameDirectory);
            NativeBackgroundClearContract(gameDirectory);
            NativeBackgroundShadeOffContract(gameDirectory);
            NativeDyingContract(gameDirectory);
            NativeDressContract(gameDirectory);
            NativeKeyOnContract(gameDirectory);
            NativeKeyScanContract(gameDirectory);
            NativeTerminalCommandsContract();
            NativeMovieTimerClockContract();
            NativeCarCommandsContract(gameDirectory);
            NativeActorScrollContract();
            NativeSetPlacementContract();
            NativeRcMoveContract();
            NativePositiveCosineTurnContract();
            NativeNegativeCosineTurnContract();
            NativeSeeDContract(gameDirectory);
            NativeDoorLineToggleContract();
            NativePartyActorDirectionContract();
            NativePartyFaceContract();
            NativeAnimationLoopYieldContract();
            NativeMovementAnimationContract(gameDirectory);
            NativePlayerScriptHandoffContract(gameDirectory);
            NativeEmptyPartyModelContract();
            NativeVibrationContract();
            NativeActorShadowContract();
            NativeFootstepPairContract();
            NativeDrawPointSetupContract(gameDirectory);
            NativeMusicPositionContract(gameDirectory);
            NativeMusicLoadContract();
            NativeMusicFadeContract();
            NativeSoundInitializationContract(gameDirectory);
            NativeSoundStopContract();
            NativeTalkSelectionContract();
            NativePreviousFieldContract();
            NativePreviousWorldEntityContract();
            NativeSlopedLineContract(gameDirectory);
            NativeSoloSlopedSplitContract(gameDirectory);
            string uninterrupted = RunInstalledCorpus(gameDirectory, false);
            string resumed = RunInstalledCorpus(gameDirectory, true);
            Require(uninterrupted == resumed, "Restoring animation/dialogue waits changed the final VM state or executed path.");
            PlayerReplayCorpus(gameDirectory);
            CorridorLineCorpus(gameDirectory);
            MovieArrivalCorpus(gameDirectory);
            ClassroomFaceFaultRetry(gameDirectory);
            ClassroomMoveCancelBoundary(gameDirectory);
            MoveCancelTargetAndWait();
            SetPcPartyActivation();
            PartyRequestWaitAndGuard();
            ClassroomPartyRequestBoundary(gameDirectory);
            PartyTurnAnglesAndResume();
            ClassroomPartyTurnBoundary(gameDirectory);
            BackgroundModuleInitialSelection(gameDirectory);
            PartyRequestNoWaitAndGuard();
            ClassroomPartyRequestNoWaitBoundary(gameDirectory);
            TutorialUnlockPersistence();
            ClassroomTutorialUnlockBoundary(gameDirectory);
            ClassroomExitTransition(gameDirectory);
            MapJump3PacketAndArrival();
            AdditionalActorControlAndGuard();
            GardenAdditionalActorControl(gameDirectory);
            NativeAreaRequestAndGuard();
            GardenAreaRequestBoundary(gameDirectory);
            DoorLineGateAndGuard();
            InfMovementAndContactPersistence();
            RefreshPartyBindingsAndGuard();
            NativeActorCompactionAndNullRequests();
            RefreshedPartyTurnTarget();
            GardenRefreshPartyBoundary(gameDirectory);
            NativeBaseAnimationContract();
            GardenJoinBoundary(gameDirectory);
            GardenHarmlessPartyContactBoundary(gameDirectory);
            GardenAutomaticFieldPartyLoad(gameDirectory);
            NativeGlobalSoundContract();
            NativeFieldMediaContract();
            GardenGlobalSoundBoundary(gameDirectory);
            NativePartyRequestStartAndGuard();
            GardenPartyRequestStartBoundary(gameDirectory);
            GardenLandingJoinBoundary(gameDirectory);
            GardenNativePushContactBoundary(gameDirectory);
            GardenFractionalLeaderJoinBoundary(gameDirectory);
            GardenNativeSplitBoundary(gameDirectory);
            NativePartyPowerAndFlagAliases(gameDirectory);
            NativeCardAndSaveEnableContract(gameDirectory);
            NativeBackgroundScrollAndGuard(gameDirectory);
            NativeDirectScrollAndBoundary(gameDirectory);
            NativePartyScrollAndBoundary(gameDirectory);
            NativeCosineScrollAndSyncBoundary(gameDirectory);
            NativeLinearScrollAndSyncContract(gameDirectory);
            GardenSlopeJoinBoundary(gameDirectory);
            GardenNativePlayerMovementBoundary(gameDirectory);
            NativeDivisionAndRemainder();
            NativeBackgroundAnimationTiming();
            InfirmaryCurtainCycles(gameDirectory);
            FrontInfirmaryCurtainCycles(gameDirectory);
            Console.WriteLine("Event scenarios passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void WaitAndCorruptInput()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 256), (Jsm.Opcode.RET, 0));
        engine.LoadField(100, content, []);
        engine.Step();
        Require(!session.State.Variables.ContainsKey(256), "WAIT resumed in its starting frame.");
        engine.Step();
        Require(!session.State.Variables.ContainsKey(256), "WAIT resumed one frame too early.");
        engine.Step();
        Require(session.State.Variables[256] == 7, "WAIT failed to resume on the expected frame.");
        content[4] = 255;
        try
        {
            JsmProgram.Read(content);
            throw new InvalidOperationException("A corrupt script table was accepted.");
        }
        catch (InvalidDataException)
        {
        }
    }

    private static string RunInstalledCorpus(string gameDirectory, bool testRestore)
    {
        using GameAssets assets = new(gameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session, session.State.Variables);
        List<int> visitedFields = [];
        List<int> movies = [];
        List<int> messages = [];
        int loadedField = -1;
        int priorMessage = -2;
        bool choiceSeen = false;
        bool nameSeen = false;
        bool curtainChanged = false;
        int firstCurtain = -1;
        bool restoredAnimation = false;
        bool restoredDialogue = false;
        FieldAssets? loadedAssets = null;
        for (int step = 0; step < 30000; step++)
        {
            if (session.State.Scene == SceneKind.Field && session.State.FieldId != loadedField)
            {
                loadedField = session.State.FieldId;
                FieldAssets field = assets.LoadField(loadedField);
                loadedAssets = field;
                engine.AnimationFrameCount = field.GetAnimationFrameCount;
                engine.LoadField(field.Id, field.Jsm, field.Messages);
                visitedFields.Add(field.Id);
                priorMessage = -2;
            }
            engine.Step();
            if (testRestore && !restoredAnimation && loadedField == 180 && engine.Frame == 30)
            {
                engine = RoundTrip(engine, session, loadedAssets!);
                restoredAnimation = true;
            }
            if (loadedField == 180 && visitedFields.Count == 2 && engine.Frame is 1 or 2 or 30)
            {
                string snapshot = System.Text.Json.JsonSerializer.Serialize(new { engine.FieldId, engine.Frame, engine.Actors, engine.BackgroundStates }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"actors-180-frame{engine.Frame}.json"), snapshot);
            }
            if (session.IsMoviePlaying)
            {
                movies.Add(session.State.MovieIndex);
                int fieldBefore = session.State.FieldId;
                engine.Step();
                Require(session.State.FieldId == fieldBefore, "A movie wait transitioned before completion.");
                session.CompleteMovie();
            }
            if (engine.CurrentDialogue is DialogueState { CanConfirm: true } dialogue)
            {
                if (testRestore && !restoredDialogue && dialogue.FirstChoice is not null)
                {
                    engine = RoundTrip(engine, session, loadedAssets!);
                    restoredDialogue = true;
                }
                if (dialogue.MessageId != priorMessage)
                {
                    messages.Add(dialogue.MessageId);
                    priorMessage = dialogue.MessageId;
                }
                choiceSeen |= dialogue.FirstChoice is not null;
                nameSeen |= dialogue.IsNamePrompt;
                engine.ConfirmDialogue(dialogue.InitialChoice ?? dialogue.FirstChoice, dialogue.IsNamePrompt ? "Research" : null);
                session.DismissDialog();
            }
            if (engine.BackgroundStates.TryGetValue(0, out int curtain))
            {
                if (firstCurtain < 0)
                {
                    firstCurtain = curtain;
                }
                curtainChanged |= firstCurtain != curtain;
            }
            if (loadedField == 229 && engine.Frame >= 10 && engine.IsUserControlEnabled)
            {
                break;
            }
        }
        Require(visitedFields.SequenceEqual(new[] { 74, 180, 181, 180, 229 }), $"Unexpected field sequence: {string.Join(',', visitedFields)}.");
        Require(movies.SequenceEqual(new[] { 30, 1 }), $"Unexpected movie sequence: {string.Join(',', movies)}.");
        Require(choiceSeen && nameSeen, "The real first-field choice/name branch did not execute.");
        Require(messages.Contains(2) && messages.Contains(18), "Expected first-field dialogues were not visited.");
        Require(session.State.Variables.GetValueOrDefault(256) == 7, "Field movie did not advance story variable W256.");
        Require(curtainChanged, "The real curtain background script did not change a state.");
        Require(session.State.FieldId == 229, "The real post-movie Quistis branch did not request its next field.");
        Require(engine.IsUserControlEnabled, "The real corridor scripts did not leave user control enabled.");
        Require(!testRestore || restoredAnimation && restoredDialogue, "Corpus did not reach the requested save/resume checkpoints.");
        Console.WriteLine($"Installed corpus {(testRestore ? "with resume" : "uninterrupted")}: fields {string.Join(',', visitedFields)}, movies {string.Join(',', movies)}, {messages.Count} dialogue observations, story W256=7; next field 229.");
        return engine.Capture().GetRawText() + string.Join(',', messages);
    }

    private static EventEngine RoundTrip(EventEngine original, GameSession session, FieldAssets field)
    {
        System.Text.Json.JsonElement checkpoint = original.Capture();
        EventEngine restored = new(session, session.State.Variables);
        restored.AnimationFrameCount = field.GetAnimationFrameCount;
        restored.LoadField(field.Id, field.Jsm, field.Messages);
        restored.Restore(checkpoint);
        Require(checkpoint.GetRawText() == restored.Capture().GetRawText(), "Checkpoint failed to preserve the complete VM state before advancing.");
        return restored;
    }

    private static void PlayerReplayCorpus(string gameDirectory)
    {
        ActionCorpus first = RunActionChain(gameDirectory, false);
        ActionCorpus alternate = RunActionChain(gameDirectory, true);
        ActionCorpus replay = RunActionChain(gameDirectory, false, first.Actions);
        Require(first.Checkpoint == replay.Checkpoint, "Replaying ordinary player actions changed the final VM state.");
        Require(first.Actions.SequenceEqual(replay.Actions), "Replayed actions observed different fields, frames or triggers.");
        Require(first.Decisions.SequenceEqual(replay.Decisions), "Replayed actions evaluated different script conditions.");
        Require(first.Dialogues.SequenceEqual(replay.Dialogues), "Replayed actions produced different dialogue outcomes.");
        DialogueOutcome initial = first.Dialogues.First(dialogue => dialogue.Choice is not null);
        DialogueOutcome other = alternate.Dialogues.First(dialogue => dialogue.Choice is not null);
        Require(initial.Operation == other.Operation && initial.Choice != other.Choice, "Two legitimate runs did not cover both answers to the first AASK.");
        int initialIndex = Array.IndexOf(first.Dialogues, initial);
        int alternateIndex = Array.IndexOf(alternate.Dialogues, other);
        Require(first.Dialogues[initialIndex + 1].MessageId == 3, "The first displayed AASK answer did not take its original result-zero message branch.");
        Require(alternate.Dialogues[alternateIndex + 1].MessageId == 5, "The second displayed AASK answer did not take its original result-one message branch.");
        Require(first.Decisions.Any(decision => decision.Predicate == "(B[1024] == 0)" && decision.ConditionPassed) && alternate.Decisions.Any(decision => decision.Predicate == "(B[1024] == 0)" && !decision.ConditionPassed), "Choosing the two visible answers failed to produce distinct source predicate outcomes.");
        Require(first.Decisions.Any(decision => decision.Predicate.Contains("W[256]")), "Branch tracing lost the source story predicate.");
        Require(first.Coverage.Any(branch => branch.TrueCount > 0 && branch.FalseCount > 0), "The actual revisit did not observe both outcomes of any source condition.");
        Require(first.Decisions.All(decision => decision.ActionIndex > 0 && decision.Operation >= 0), "A branch decision lost its source/action evidence.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "player-trace-default.json"), System.Text.Json.JsonSerializer.Serialize(new { first.Actions, first.Decisions, first.Dialogues, first.Coverage }));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "player-trace-alternate.json"), System.Text.Json.JsonSerializer.Serialize(new { alternate.Actions, alternate.Decisions, alternate.Dialogues, alternate.Coverage }));
        Console.WriteLine($"Player replay corpus: {first.Actions.Length} actions, {first.Coverage.Length} branch sites, both first AASK answers; exact replay passed.");
    }

    private static ActionCorpus RunActionChain(string gameDirectory, bool alternateChoice, IReadOnlyList<PlayerActionObservation>? replay = null, bool continueCorridor = false, bool continueTo232 = false)
    {
        using GameAssets assets = new(gameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session, session.State.Variables);
        if (continueTo232)
        {
            engine.MoviePlayback = () => session.State.MoviePlayback;
            engine.MovieSkipRequested += () =>
            {
                // movie0 actual ffprobe count_frames:326, FPS15, last zero-based frame325.
                session.SetMoviePlayback(new MoviePlaybackState(0, session.State.MovieIndex == 0 ? 325 : 0, true, false, false));
                session.CompleteMovie(skipped: true);
            };
        }
        else
        {
            engine.MovieSkipRequested += session.CompleteMovie;
        }
        List<object> commandTrace = [];
        if (continueCorridor)
        {
            engine.CommandExecuted += command =>
            {
                if (command.FieldId == 229 && command.ObjectId is 0 or 1 or 8 or 9)
                {
                    commandTrace.Add(new { engine.Frame, Command = command });
                }
            };
        }
        List<int> fields = [];
        int loadedField = -1;
        bool chosen = false;
        FieldAssets? field = null;
        void LoadPendingField()
        {
            if (session.State.Scene == SceneKind.Field && loadedField != session.State.FieldId)
            {
                field = assets.LoadField(session.State.FieldId);
                loadedField = field.Id;
                fields.Add(field.Id);
                engine.AnimationFrameCount = field.GetAnimationFrameCount;
                engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out System.Numerics.Vector3 position, out int triangle) ? new WalkmeshPosition(position, triangle) : null;
                engine.LoadField(field.Id, field.Jsm, field.Messages);
            }
        }
        if (replay is not null)
        {
            IEnumerable<PlayerAction> Actions()
            {
                foreach (PlayerActionObservation observation in replay)
                {
                    LoadPendingField();
                    Require(engine.FieldId == observation.FieldId && engine.Frame == observation.Frame, "Replay diverged before applying the next ordinary action.");
                    yield return observation.Action;
                }
            }
            Require(engine.Replay(Actions()).All(result => result.Accepted), "A recorded player action was rejected during replay.");
        }
        else
        {
            for (int index = 0; index < 200; index++)
            {
                if (continueTo232 ? session.State.FieldId == 232 : continueCorridor && session.IsMoviePlaying && session.State.MovieIndex == 0)
                {
                    break;
                }
                LoadPendingField();
                PlayerAction action;
                if (engine.NextTrigger.Kind == PlayerTriggerKind.Movie)
                {
                    action = new PlayerAction(PlayerActionKind.SkipMovie);
                }
                else if (engine.CurrentDialogue is DialogueState { CanConfirm: true } dialogue)
                {
                    if (dialogue.FirstChoice is int firstChoice)
                    {
                        int choice = alternateChoice && !chosen ? dialogue.LastChoice!.Value : dialogue.InitialChoice ?? firstChoice;
                        chosen = true;
                        action = new PlayerAction(PlayerActionKind.Choose, Value: choice);
                    }
                    else
                    {
                        action = dialogue.IsNamePrompt ? new PlayerAction(PlayerActionKind.EnterName, Text: "Research") : new PlayerAction(PlayerActionKind.Confirm);
                    }
                }
                else if (loadedField == 229 && engine.NextTrigger.Kind == PlayerTriggerKind.Playable)
                {
                    if (continueCorridor)
                    {
                        Require(engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 30)).Accepted, "Natural corridor movement was rejected.");
                        continue;
                    }
                    ActorState player = engine.Actors.First(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
                    System.Numerics.Vector2 before = new(player.X, player.Y);
                    PlayerActionResult movement = engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: 1));
                    Require(movement.Accepted && movement.FramesAdvanced == 1, "Ordinary corridor movement did not advance exactly one tick.");
                    float distance = System.Numerics.Vector2.Distance(before, new(player.X, player.Y));
                    Require(distance > 0 && Math.Abs(distance - player.MovementSpeed / 256f) < 0.001f, "Diagonal movement did not preserve original MSPEED magnitude.");
                    break;
                }
                else
                {
                    action = new PlayerAction(PlayerActionKind.Wait, Frames: continueCorridor ? 3000 : 30000);
                }
                PlayerActionResult applied = engine.Apply(action);
                Require(applied.Accepted, "The real script chain rejected a normal player action.");
                if (continueCorridor && applied.Detail is not null)
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "corridor-wait-checkpoint.json"), engine.Capture().GetRawText());
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "corridor-command-trace.json"), System.Text.Json.JsonSerializer.Serialize(commandTrace));
                    throw new InvalidOperationException($"Corridor wait budget exhausted at frame {engine.Frame}; checkpoint persisted for source diagnosis.");
                }
            }
        }
        Require(fields.SequenceEqual(new[] { 74, 180, 181, 180, 229 }) && (continueTo232 ? session.State.FieldArrival == new FieldTransition(232, 1389, -3580, 50, 0) : continueCorridor ? session.IsMoviePlaying && session.State.MovieIndex == 0 : engine.IsUserControlEnabled), "Action traversal failed to reach its genuine next trigger.");
        Require(engine.Frame > 1 && engine.FieldId == 229, "Action traversal ended before corridor initialization/movement.");
        return new ActionCorpus(engine.Capture().GetRawText(), engine.ActionLog.ToArray(), engine.Decisions.ToArray(), engine.DialogueOutcomes.ToArray(), engine.Coverage.ToArray(), engine.LineCrossings.ToArray());
    }

    private static void CorridorLineCorpus(string gameDirectory)
    {
        ActionCorpus first = RunActionChain(gameDirectory, false, continueCorridor: true);
        ActionCorpus replay = RunActionChain(gameDirectory, false, first.Actions, continueCorridor: true);
        Require(first.Checkpoint == replay.Checkpoint && first.Decisions.SequenceEqual(replay.Decisions), "Natural corridor crossing replay diverged.");
        Require(first.Lines.Select(line => line.ObjectId).SequenceEqual(new[] { 10, 11 }), "Natural movement did not execute both original corridor crossing scripts exactly once.");
        Require(first.Lines.Select(line => line.ScriptId).SequenceEqual(new[] { 59, 67 }), "Crossing dispatch used the wrong source scripts.");
        Require(first.Dialogues.Any(dialogue => dialogue.FieldId == 229), "Crossing failed to reach the original corridor conversations.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "player-trace-corridor.json"), System.Text.Json.JsonSerializer.Serialize(new { first.Actions, first.Decisions, first.Dialogues, first.Coverage, first.Lines }));
        Console.WriteLine($"Natural corridor corpus: {first.Actions.Length} ordinary actions, lines 10/59 and 11/67 -> original movie0 trigger; exact replay passed.");
    }

    private static void MovieArrivalCorpus(string gameDirectory)
    {
        ActionCorpus first = RunActionChain(gameDirectory, false, continueCorridor: true, continueTo232: true);
        ActionCorpus replay = RunActionChain(gameDirectory, false, first.Actions, continueCorridor: true, continueTo232: true);
        Require(first.Checkpoint == replay.Checkpoint && first.Decisions.SequenceEqual(replay.Decisions), "Real movie-frame continuation changed on action replay.");
        Require(first.Decisions.Any(item => item.Predicate.Contains("L[80]") && item.ConditionPassed), "Decoded terminal frame did not satisfy the original movie-frame predicate.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "player-trace-movie0-arrival232.json"), System.Text.Json.JsonSerializer.Serialize(new { first.Actions, first.Decisions, first.Dialogues, first.Coverage, first.Lines, Arrival = new FieldTransition(232, 1389, -3580, 50, 0) }));
        using GameAssets assets = new(gameDirectory);
        FieldAssets source = assets.LoadField(229);
        FieldAssets destination = assets.LoadField(232);
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 232 });
        EventEngine arrivalEngine = new(session, session.State.Variables);
        arrivalEngine.LoadField(229, source.Jsm, source.Messages);
        using var saved = System.Text.Json.JsonDocument.Parse(first.Checkpoint);
        arrivalEngine.Restore(saved.RootElement);
        HashSet<int> otherTriangles = Enumerable.Range(0, destination.Walkmesh.Triangles.Count).Where(index => index != 50).ToHashSet();
        arrivalEngine.ResolveFieldArrival = arrival => destination.Walkmesh.TryPosition(arrival.X, arrival.Y, out System.Numerics.Vector3 position, out int triangle, otherTriangles) ? new WalkmeshPosition(position, triangle) : null;
        arrivalEngine.AnimationFrameCount = destination.GetAnimationFrameCount;
        JsmProgram destinationProgram = JsmProgram.Read(destination.Jsm);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "field232-selected-scripts.json"), System.Text.Json.JsonSerializer.Serialize(new { Header = destination.Jsm.Take(4).ToArray(), RawGroups = Enumerable.Range(0, destination.Jsm.Take(4).Sum(value => (int)value)).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(destination.Jsm.AsSpan(8 + index * 2)) >> 7), Objects = destinationProgram.Objects.Select(obj => new { obj.Index, obj.Label, Scripts = obj.Scripts.Select(script => script.Id) }), Operations = destinationProgram.Operations.Skip(7348).Take(38).Select(op => new { op.Index, Opcode = op.Opcode.ToString(), op.Parameter }) }));
        arrivalEngine.ReadFaceBaseline = actor =>
        {
            FieldModel model = destination.Models[actor.ModelId];
            ModelAnimation animation = model.Animations[actor.AnimationId];
            System.Numerics.Vector3 angles = model.ReadFirstControlRotation(actor.AnimationId, 0);
            return new FaceDirectionBaseline((int)MathF.Round(angles.Y * 4096 / MathF.Tau) / 16, (int)MathF.Round(angles.Z * 4096 / MathF.Tau) / 16, (int)MathF.Round(angles.X * 4096 / MathF.Tau) / 16);
        };
        arrivalEngine.LoadSoundResource = _ => SoundResourceStatus.Muted;
        arrivalEngine.LoadField(232, destination.Jsm, destination.Messages, new FieldTransition(232, 1389, -3580, 50, 0));
        Require(arrivalEngine.BackgroundStates.Count == 7 && arrivalEngine.BackgroundStates.Values.All(state => state == -1), "Actual classroom background modules were selected before any init script executed.");
        HashSet<string> classroomOpcodes = [];
        arrivalEngine.CommandExecuted += command =>
        {
            classroomOpcodes.Add(command.Name);
            if (command.Name == "POLYCOLORALL")
            {
                Console.WriteLine("Field232 POLYCOLORALL RGB: " + string.Join(",", command.Arguments));
            }
        };
        bool spawned = false;
        bool reachedTrigger = false;
        for (int index = 0; index < 3000; index++)
        {
            try
            {
                arrivalEngine.Step();
            }
            catch (EventScriptException exception)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "field232-first-boundary.json"), System.Text.Json.JsonSerializer.Serialize(new { arrivalEngine.LastFault, Error = exception.Message, arrivalEngine.Frame, Actors = arrivalEngine.Actors.Where(actor => actor.CharacterId == 0) }));
                Console.WriteLine($"Field232 first boundary: {exception.Message}");
                if (arrivalEngine.LastFault is EventFault eventFault)
                {
                    Console.WriteLine("Boundary operands: " + arrivalEngine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == eventFault.ObjectId && fiber.GetProperty("ScriptId").GetInt32() == eventFault.ScriptId).GetProperty("Stack"));
                }
                break;
            }
            if (!spawned && arrivalEngine.Capture().GetProperty("PendingArrival").ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                ActorState player = arrivalEngine.Actors.First(actor => actor.Active && actor.CharacterId == 0);
                Require(player.WalkmeshTriangle == 50 && player.X == 1389 && player.Y == -3580 && player.Direction == 0, "Actual destination init lost its native arrival before defaults.");
                Require(arrivalEngine.Scene.LoadedSoundEffects[11] is { NativeResourceName: "wave0811.dat", Status: SoundResourceStatus.Muted }, "Classroom sound-resource request lost its authorized muted backend status.");
                spawned = true;
                Console.WriteLine($"Field232 spawn applied at frame{arrivalEngine.Frame}: Z{player.Z}, triangle{player.WalkmeshTriangle}.");
            }
            if (arrivalEngine.NextTrigger.Kind is PlayerTriggerKind.Dialogue or PlayerTriggerKind.Playable)
            {
                reachedTrigger = true;
                break;
            }
        }
        Require(spawned && reachedTrigger && arrivalEngine.LastFault is null, "Actual classroom setup failed to reach its first genuine dialogue/control trigger.");
        Require(arrivalEngine.Scene.CameraIndex == 1 && arrivalEngine.Scene.BackgroundShadeLoops.ContainsKey(3) && arrivalEngine.Scene.PartyTrace?.FirstTrail.Length == 64, "Classroom camera, module shade loop or native party trace initialization was lost.");
        Require(arrivalEngine.BackgroundStates[0] == 1 && arrivalEngine.BackgroundStates[6] == -1, "Actual classroom init lost its selected camera background or displayed never-selected desk sprites.");
        Require(arrivalEngine.Scene.Party.SequenceEqual([0]) && new[] { 3, 6, 10, 12, 13 }.All(index => !arrivalEngine.Actors[index].Active), "Actual classroom SETPC retained absent party placeholders as active actors.");
        Require(arrivalEngine.Actors[1].Active && arrivalEngine.Actors[1].CharacterId == 0 && arrivalEngine.Actors[11].Active && arrivalEngine.Actors[11].CharacterId == -2, "Classroom party activation disabled Squall or the separate Quistis NPC.");
        var arrivalSnapshot = arrivalEngine.Capture();
        arrivalEngine.Restore(arrivalSnapshot);
        Require(arrivalSnapshot.GetRawText() == arrivalEngine.Capture().GetRawText(), "Classroom face/audio/shade/trace/arrival snapshot did not round-trip exactly.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "field232-first-trigger-checkpoint.json"), arrivalEngine.Capture().GetRawText());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "field232-first-trigger.json"), System.Text.Json.JsonSerializer.Serialize(new { arrivalEngine.FieldId, arrivalEngine.Frame, arrivalEngine.NextTrigger, arrivalEngine.IsUserControlEnabled, arrivalEngine.Scene.CameraIndex, arrivalEngine.Scene.LoadedSoundEffects, arrivalEngine.Scene.LockedTriangles, Shades = arrivalEngine.GetBackgroundModuleShades(), Opcodes = classroomOpcodes.Order().ToArray(), Players = arrivalEngine.Actors.Where(actor => actor.CharacterId == 0).Select(actor => new { actor.X, actor.Y, actor.Z, actor.Direction, actor.WalkmeshTriangle, actor.Visible }) }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
        Console.WriteLine($"Field232 state at frame{arrivalEngine.Frame}: {arrivalEngine.NextTrigger.Kind}, control{arrivalEngine.IsUserControlEnabled}, dialogue{arrivalEngine.CurrentDialogue?.MessageId}.");
        Console.WriteLine($"Movie0 arrival corpus: {first.Actions.Length} ordinary actions, skip terminal325 -> native field232 (1389,-3580), triangle50, direction0; exact replay passed.");
    }

    private static void FaceDirectionAnglesAndResume()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        (Jsm.Opcode, int)[] source = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] face = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.FACEDIRA, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)];
        byte[] program = SyntheticObjects([1, (16 << 7) | 1], [source, face, target, [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(GameSession current)
        {
            EventEngine result = new(current, current.State.Variables) { AnimationFrameCount = (_, _) => 4, ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0), ReadFaceAnchorHeight = _ => 0, ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) };
            result.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return result;
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = Create(session);
        while (engine.Actors[0].FaceTargetPoint is null && engine.Frame < 8)
        {
            engine.Step();
        }
        Require(engine.Actors[0].FaceProgress == 1 && !session.State.Variables.ContainsKey(512), "FACEDIRA did not block its source fiber or advance the control-node driver.");
        Require(engine.Actors[0].FirstControlRotation is System.Numerics.Vector3 firstRotation && Math.Abs(firstRotation.Y - MathF.Tau / 256) < 0.00001 && Math.Abs(firstRotation.Z + MathF.Tau / 256) < 0.00001, "Native first cosine sample did not truncate to pitch1/yaw1.");
        var snapshot = engine.Capture();
        GameSession otherSession = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine restored = Create(otherSession);
        restored.Restore(snapshot);
        Require(snapshot.GetRawText() == restored.Capture().GetRawText(), "Face transition snapshot changed its captured point, override or cosine phase.");
        while (engine.Actors[0].FaceProgress < 4)
        {
            engine.Step();
            restored.Step();
            if (engine.Actors[0].FaceProgress == 2)
            {
                System.Numerics.Vector3 midpoint = engine.Actors[0].FirstControlRotation!.Value;
                Require(Math.Abs(midpoint.Y - 4 * MathF.Tau / 256) < 0.00001 && Math.Abs(midpoint.Z + 5 * MathF.Tau / 256) < 0.00001, "Native cosine midpoint differs from pitch4/yaw5.");
            }
        }
        Require(engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Resuming a face transition changed its endpoint or script wait.");
        Require(!session.State.Variables.ContainsKey(512), "FACEDIRA closing successor ran before its next native VM callback.");
        engine.Step();
        restored.Step();
        ActorState actor = engine.Actors[0];
        Require(actor.FaceYawTarget == 10 && actor.FacePitchTarget == 8 && actor.FaceSecondAngle == 10 && actor.FaceElapsed == 8 && session.State.Variables[512] == 7, "Native head target clamping or duration completion failed.");
        Require(actor.FirstControlRotation is System.Numerics.Vector3 rotation && Math.Abs(rotation.Y - 8 * MathF.Tau / 256) < 0.00001 && Math.Abs(rotation.Z + 10 * MathF.Tau / 256) < 0.00001, "Native pitch/yaw signs did not reach the declared control-node override.");
    }

    private static void ClassroomFaceFaultRetry(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        string fixture = Path.Combine("../fixtures/external-fixture.json");
        GameState saved = GameSession.Load(fixture);
        GameSession session = new(saved);
        EventEngine Create(GameSession current)
        {
            EventEngine engine = new(current, current.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.ReadFaceBaseline = actor =>
            {
                System.Numerics.Vector3 angles = field.Models[actor.ModelId].ReadFirstControlRotation(actor.AnimationId, 0);
                return new FaceDirectionBaseline((int)MathF.Round(angles.Y * 4096 / MathF.Tau) / 16, (int)MathF.Round(angles.Z * 4096 / MathF.Tau) / 16, (int)MathF.Round(angles.X * 4096 / MathF.Tau) / 16);
            };
            engine.ReadFaceAnchorHeight = actor => field.Models[actor.ModelId].GetFirstControlBoneHeight(actor.AnimationId, actor.AnimationFrame, 0, actor.FirstControlRotation);
            engine.ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY);
            engine.LoadField(232, field.Jsm, field.Messages);
            return engine;
        }
        EventEngine original = Create(session);
        original.Restore(saved.EventState!.Value);
        Require(original.LastFault is null && original.Frame == 177, "Source upgrade failed to clear the exact saved FACEDIRA fault.");
        var preserved = original.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 15 && fiber.GetProperty("ScriptId").GetInt32() == 137);
        Require(preserved.GetProperty("Position").GetInt32() == 3011 && preserved.GetProperty("Stack")[0].GetInt32() == 15 && preserved.GetProperty("Stack")[1].GetInt32() == 11, "Source upgrade consumed the original FACEDIRA operands or changed its retry PC.");
        FieldModel model = field.Models[14];
        var baselines = model.Animations[0].Frames.Select((frame, index) => model.ReadFirstControlRotation(0, index)).ToArray();
        Console.WriteLine($"FACEDIRA actor15 control bone{model.ControlBones.First}, baseline Y/Z distinct: {string.Join(';', baselines.Select(rotation => $"{rotation.Y},{rotation.Z}").Distinct())}.");
        Require(model.ControlBones.First == 4 && baselines.Length == 1 && (int)MathF.Round(baselines[0].Y * 4096 / MathF.Tau) / 16 == 229 && (int)MathF.Round(baselines[0].Z * 4096 / MathF.Tau) / 16 == 0, "The proved legacy baseline migration fixture no longer has a single declared-control pose229/0.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "classroom-face-native-pose.json"), System.Text.Json.JsonSerializer.Serialize(new { Model = model.Name, model.ControlBones, Baselines = baselines, Operations = JsmProgram.Read(field.Jsm).Operations.Skip(3011).Take(25).Select(op => new { op.Index, Opcode = op.Opcode.ToString(), op.Parameter }) }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
        original.Step();
        Require(original.Actors[15].FaceDuration == 15 && original.Actors[15].FaceProgress == 0 && original.Actors[15].FaceTargetPoint is not null, "Retry did not execute the saved original target/duration.");
        original.Step();
        Require(original.Actors[15].FirstControlRotation is not null && original.Actors[15].FaceProgress == 1, "FACEDIRA did not issue the native declared-node override on its first driver frame.");
        var midwait = original.Capture();
        GameSession resumedSession = new(session.State with { Variables = new Dictionary<int, int>(session.State.Variables) });
        EventEngine resumed = Create(resumedSession);
        resumed.Restore(midwait);
        Require(midwait.GetRawText() == resumed.Capture().GetRawText(), "Actual FACEDIRA midwait snapshot failed to round-trip.");
        for (int tick = 0; tick < 1000 && !original.IsDialogueOpen && !original.IsUserControlEnabled; tick++)
        {
            original.Step();
            resumed.Step();
        }
        Require(original.Capture().GetRawText() == resumed.Capture().GetRawText(), "Actual FACEDIRA resume diverged before its next original trigger.");
        Require(original.NextTrigger.Kind is PlayerTriggerKind.Dialogue or PlayerTriggerKind.Playable, "Actual FACEDIRA retry failed to reach the next observable trigger.");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "classroom-face-next-trigger.json"), System.Text.Json.JsonSerializer.Serialize(new { original.Frame, original.NextTrigger, Dialogue = original.CurrentDialogue, Actor = original.Actors[15] }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
        Console.WriteLine($"FACEDIRA retry reached {original.NextTrigger.Kind}, message{original.CurrentDialogue?.MessageId}, frame{original.Frame}.");
    }

    private static void ClassroomMoveCancelBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        Require(program.NativeObjects[14] == 14 && program.Operations[1337].Opcode == Jsm.Opcode.MOVECANCEL, "The actual classroom MOVECANCEL native target or operation changed.");
        EventEngine Create(GameState state)
        {
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(232, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        string evidenceDirectory = Path.Combine("../fixtures");
        string beforePath = Path.Combine(evidenceDirectory, "player-performance-movecancel-preboundary-checkpoint.json");
        EventEngine original = Create(GameSession.Load(beforePath));
        EventEngine replayed = Create(GameSession.Load(beforePath));
        Require(original.Frame == 363 && original.NextTrigger.Kind == PlayerTriggerKind.Waiting, "MOVECANCEL replay did not start at the actual immediately preceding frame.");
        ActorState target = original.Actors[14];
        Require(target.ModelId == 13 && target.MovementTarget is null && target.MovementActorId is null, "The reached MOVECANCEL target is no longer the proved stationary actor.");
        float targetX = target.X;
        float targetY = target.Y;
        float expectedFrame = target.AnimationFirst + (target.AnimationFrame + Math.Max(1, target.AnimationSpeed) / 16f - target.AnimationFirst) % Math.Max(1, target.AnimationLast - target.AnimationFirst + 1);
        int cancellations = 0;
        original.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.MOVECANCEL) && command.Operation == 1337)
            {
                cancellations++;
                Require(target.X == targetX && target.Y == targetY && target.AnimationFrame == expectedFrame && original.Actors[11].MovementTarget is not null, "Stationary-target MOVECANCEL reset its animation or cancelled the moving caller.");
            }
        };
        original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(cancellations == 1 && original.Frame == 364 && original.LastFault is null, $"Exact-before MOVECANCEL action failed: count{cancellations}, frame{original.Frame}, fault{original.LastFault}.");
        Require(original.Capture().GetRawText() == replayed.Capture().GetRawText(), "Exact-before MOVECANCEL action replay changed the VM.");
        EventEngine retry = Create(GameSession.Load(Path.Combine(evidenceDirectory, "player-performance-movecancel-exact-fault-checkpoint.json")));
        var preserved = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 90);
        Require(retry.LastFault is null && preserved.GetProperty("Position").GetInt32() == 1337 && preserved.GetProperty("Stack")[0].GetInt32() == 14, "MOVECANCEL source upgrade changed the saved retry PC or native operand.");
        int retries = 0;
        retry.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.MOVECANCEL) && command.Operation == 1337)
            {
                retries++;
            }
        };
        retry.Step();
        Require(retries == 1 && retry.LastFault is null, "The preserved MOVECANCEL fault failed to retry exactly once.");
        Console.WriteLine("MOVECANCEL: actual frame363 -> 364 ordinary Wait replay matches; native14 -> actor14 stationary no-op, saved fault retries unchanged.");
    }

    private static void MoveCancelTargetAndWait()
    {
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.MOVECANCEL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([0, 16 << 7], [caller, target]);
        content[2] = 1;
        content[3] = 1;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 9 };
        engine.LoadField(100, content, []);
        engine.Step();
        var moving = engine.Capture();
        Require(engine.Actors[1].MovementTarget is not null && !session.State.Variables.ContainsKey(512), "Target MOVESYNC did not wait for its real scripted movement.");
        engine.Step();
        Require(engine.Actors[1].X > 0 && engine.Actors[1].X < 1000 && engine.Actors[1].MovementTarget is null && session.State.Variables[512] == 7, "MOVECANCEL failed to stop the native target in place and release its movement wait.");
        Require(engine.Actors[1].AnimationFrame == 0 && engine.Actors[1].AnimationFirst == 0 && engine.Actors[1].AnimationLast == 8 && engine.Actors[1].AnimationLoop, "MOVECANCEL did not restart the target base animation's complete native frame range.");
        string cancelled = engine.Capture().GetRawText();
        engine.Restore(moving);
        engine.Step();
        Require(cancelled == engine.Capture().GetRawText(), "Restoring the moving target changed cancellation or movement-wait completion.");
    }

    private static void SetPcPartyActivation()
    {
        EventEngine Run(params (Jsm.Opcode Opcode, int Parameter)[] commands)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, SyntheticProgram(commands), []);
            engine.Step();
            return engine;
        }
        EventEngine absent = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(absent.Actors[0].CharacterId == 5 && !absent.Actors[0].Active && absent.Actors[0].Visible, "SETPC absent-party registration changed visibility instead of clearing active state.");
        EventEngine present = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(present.Actors[0].CharacterId == 1 && present.Actors[0].Active, "SETPC did not preserve an active party member or truncate character ID to a byte.");
        EventEngine inactive = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0));
        Require(!inactive.Actors[0].Active, "SETPC implicitly activated a party member instead of preserving UNUSE.");
        // This independent synthetic world models the late NPC USE branch; it is not an action trace through that story branch.
        EventEngine npc = Run((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 9), (Jsm.Opcode.JPF, 2), (Jsm.Opcode.USE, 0), (Jsm.Opcode.RET, 0));
        Require(npc.Scene.Party.Count == 0 && npc.Actors[0].CharacterId == 5 && npc.Actors[0].Active, "The late conditional USE branch failed to activate a registered character as an NPC.");
    }

    private static EventEngine CreatePartyRequestFixture(int slot, int priority, bool targetBusy, Jsm.Opcode opcode)
    {
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, priority), (Jsm.Opcode.PSHN_L, 1), (opcode, slot), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)> targetInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0)];
        if (targetBusy)
        {
            targetInit.Add((Jsm.Opcode.PSHN_L, 5));
            targetInit.Add((Jsm.Opcode.WAIT, 0));
        }
        targetInit.Add((Jsm.Opcode.RET, 0));
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 513), (Jsm.Opcode.RET, 0)];
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, SyntheticObjects([0, (16 << 7) | 1], [caller, targetInit.ToArray(), requested]), []);
        return engine;
    }

    private static void PartyRequestWaitAndGuard()
    {
        EventEngine Create(int slot, int priority, bool targetBusy) => CreatePartyRequestFixture(slot, priority, targetBusy, Jsm.Opcode.PREQEW);
        EventEngine accepted = Create(0, 1, false);
        accepted.Step();
        accepted.Step();
        var waiting = accepted.Capture();
        Require(waiting.GetProperty("Variables").TryGetProperty("512", out _) == false && waiting.GetProperty("Fibers")[0].GetProperty("WaitReason").GetString() == nameof(WaitReason.RequestEnd), "PREQEW caller resumed before its registered party actor completed the requested script.");
        accepted.Step();
        Require(!accepted.Capture().GetProperty("Variables").TryGetProperty("512", out _), "PREQEW end wait released before the target WAIT duration.");
        accepted.Step();
        Require(accepted.Capture().GetProperty("Variables").GetProperty("513").GetInt32() == 7 && !accepted.Capture().GetProperty("Variables").TryGetProperty("512", out _), "The target completion did not precede its waiting caller's next scheduler turn.");
        accepted.Step();
        string completed = accepted.Capture().GetRawText();
        Require(accepted.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 11, "PREQEW end wait failed to release after target completion.");
        accepted.Restore(waiting);
        accepted.Step();
        accepted.Step();
        accepted.Step();
        Require(completed == accepted.Capture().GetRawText(), "Restoring PREQEW's pending relationship changed target/caller completion order.");
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = Create(slot, priority, true);
            ignored.Step();
            ignored.Step();
            Require(ignored.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 11 && ignored.Capture().GetProperty("Fibers").GetArrayLength() == 2, "PREQEW absent slot or occupied same-priority request was not consumed as a no-op.");
        }
        EventEngine conflicting = Create(0, 1, true);
        conflicting.Step();
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Unsupported PREQEW preemption executed.");
        }
        catch (EventScriptException)
        {
        }
        var fault = conflicting.Capture();
        var callerFault = fault.GetProperty("Fibers")[0];
        Require(conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQEW) && callerFault.GetProperty("Position").GetInt32() == 7 && callerFault.GetProperty("Stack")[0].GetInt32() == 1 && callerFault.GetProperty("Stack")[1].GetInt32() == 1, "Unsupported PREQEW preemption changed its PC or consumed original operands.");
        conflicting.Restore(fault);
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("A restored PREQEW preemption fault resumed.");
        }
        catch (EventScriptException)
        {
        }
        Require(fault.GetRawText() == conflicting.Capture().GetRawText(), "Unsupported PREQEW preemption did not remain frozen after restore.");
    }

    private static void PartyRequestNoWaitAndGuard()
    {
        EventEngine accepted = CreatePartyRequestFixture(0, 1, false, Jsm.Opcode.PREQ);
        accepted.Step();
        accepted.Step();
        var running = accepted.Capture();
        Require(running.GetProperty("Variables").GetProperty("512").GetInt32() == 11 && !running.GetProperty("Variables").TryGetProperty("513", out _), "PREQ caller waited for its target instead of continuing immediately.");
        var caller = running.GetProperty("Fibers")[0];
        Require(caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.None) && caller.GetProperty("RequestSequence").ValueKind == System.Text.Json.JsonValueKind.Null, "PREQ installed a caller wait relationship.");
        accepted.Step();
        accepted.Step();
        string completed = accepted.Capture().GetRawText();
        Require(accepted.Capture().GetProperty("Variables").GetProperty("513").GetInt32() == 7, "PREQ target failed to finish independently of its caller.");
        accepted.Restore(running);
        accepted.Step();
        accepted.Step();
        Require(completed == accepted.Capture().GetRawText(), "Restoring the independent PREQ target changed its completion.");
        foreach ((int slot, int priority) in new[] { (1, 1), (0, 0) })
        {
            EventEngine ignored = CreatePartyRequestFixture(slot, priority, true, Jsm.Opcode.PREQ);
            ignored.Step();
            ignored.Step();
            Require(ignored.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 11 && ignored.Capture().GetProperty("Fibers").GetArrayLength() == 2, "PREQ absent slot or occupied same-priority request was not consumed as a no-op.");
        }
        EventEngine conflicting = CreatePartyRequestFixture(0, 1, true, Jsm.Opcode.PREQ);
        conflicting.Step();
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Unsupported PREQ preemption executed.");
        }
        catch (EventScriptException)
        {
        }
        var fault = conflicting.Capture();
        var preserved = fault.GetProperty("Fibers")[0];
        Require(conflicting.LastFault?.Opcode == nameof(Jsm.Opcode.PREQ) && preserved.GetProperty("Position").GetInt32() == 7 && preserved.GetProperty("Stack")[0].GetInt32() == 1 && preserved.GetProperty("Stack")[1].GetInt32() == 1, "Unsupported PREQ preemption consumed operands or changed its PC.");
        conflicting.Restore(fault);
        try
        {
            conflicting.Step();
            throw new InvalidOperationException("Restored unsupported PREQ preemption resumed.");
        }
        catch (EventScriptException)
        {
        }
        Require(fault.GetRawText() == conflicting.Capture().GetRawText(), "Unsupported PREQ preemption did not stay frozen across restore.");

        (Jsm.Opcode, int)[] signalCaller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PREQ, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 13), (Jsm.Opcode.POPM_B, 1029), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] registered = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] dormant = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] facingLoop = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHM_B, 1029), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.CAL, 12), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 4), (Jsm.Opcode.PSHAC, 0), (Jsm.Opcode.DIRA, 0), (Jsm.Opcode.JMP, -8), (Jsm.Opcode.RET, 0)];
        GameSession signalSession = new(new GameState());
        EventEngine signaled = new(signalSession, signalSession.State.Variables);
        signaled.LoadField(100, SyntheticObjects([0, (16 << 7) | 2], [signalCaller, registered, dormant, facingLoop]), []);
        int faceSamples = 0;
        signaled.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.DIRA))
            {
                faceSamples++;
            }
        };
        for (int frame = 0; frame < 5; frame++)
        {
            signaled.Step();
        }
        Require(faceSamples == 3 && signalSession.State.Variables[1029] == 128 && !signaled.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 18 && !fiber.GetProperty("Complete").GetBoolean()), "Command-driven bit128 signal failed to release the independent PREQ facing loop.");
    }

    private static void ClassroomPartyRequestNoWaitBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        EventEngine Create(string leaf)
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures", leaf));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(232, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        const string beforeLeaf = "player-performance-preq-preboundary-checkpoint.json";
        EventEngine original = Create(beforeLeaf);
        EventEngine replayed = Create(beforeLeaf);
        Require(original.Frame == 2241 && original.Scene.Party.SequenceEqual([0]), "PREQ fixture lost its exact preceding frame or party.");
        original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(original.Capture().GetRawText() == replayed.Capture().GetRawText(), "Exact-before PREQ action replay changed the full VM state.");
        var running = original.Capture();
        var caller = running.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 91);
        var target = running.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 19);
        Require(target.GetProperty("Priority").GetInt32() == 1 && !target.GetProperty("Started").GetBoolean() && caller.GetProperty("RequestSequence").ValueKind == System.Text.Json.JsonValueKind.Null && caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.Animation), "PREQ did not queue Squall script15/label19 while continuing Quistis to her original animation wait.");
        Require(original.Actors[11].MovementTarget is not null && caller.GetProperty("Position").GetInt32() == 1701, "Quistis failed to execute original RFMOVE1699 after PREQ.");
        EventEngine resumed = Create(beforeLeaf);
        resumed.Restore(running);
        while ((original.Capture().GetProperty("Variables").GetProperty("1029").GetInt32() & 128) == 0 && original.Frame < 4000)
        {
            original.Step();
            resumed.Step();
            Require(original.Capture().GetRawText() == resumed.Capture().GetRawText(), "PREQ's independent facing loop or original caller progression changed after restore.");
        }
        Require((original.Capture().GetProperty("Variables").GetProperty("1029").GetInt32() & 128) != 0, "Original Quistis caller never released Squall's requested facing loop.");
        Require(original.Frame == 2334 && original.LastFault is null && original.Scene.UnlockedTutorials.SetEquals([0]) && original.IsUserControlEnabled && !original.Actors[11].Visible, "Original caller failed to unlock tutorial0 and return control after signaling bit128.");
        original.Step();
        resumed.Step();
        Require(original.Capture().GetRawText() == resumed.Capture().GetRawText() && !original.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 19 && !fiber.GetProperty("Complete").GetBoolean()), "Actual Squall facing loop did not finish on its next scheduler turn after the original bit128 signal.");
        EventEngine retry = Create("player-performance-preq-exact-fault-checkpoint.json");
        var preserved = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 91);
        Require(retry.LastFault is null && preserved.GetProperty("Position").GetInt32() == 1693 && preserved.GetProperty("Stack")[0].GetInt32() == 15 && preserved.GetProperty("Stack")[1].GetInt32() == 1, "PREQ source upgrade changed the exact fault PC or operands.");
        retry.Step();
        Require(retry.Actors[11].MovementTarget is not null && retry.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 19), "PREQ saved-fault retry failed to queue its original independent script and continue RFMOVE.");
        Console.WriteLine($"PREQ: exact2241 -> 2242 replay; idle Squall1 script15/label19, no caller request wait; original RFMOVE, bit128 signal and facing-loop completion -> playable/frame{original.Frame}; independent resume and fault retry passed.");
    }

    private static void NativeDivisionAndRemainder()
    {
        foreach ((int left, int right, int quotient, int remainder) in new[] { (17, 6, 2, 5), (-17, 6, -2, -5), (17, -6, -2, 5), (0, 6, 0, 0) })
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, left), (Jsm.Opcode.PSHN_L, right), (Jsm.Opcode.CAL, 3), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, left), (Jsm.Opcode.PSHN_L, right), (Jsm.Opcode.CAL, 4), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.RET, 0)), []);
            engine.Step();
            Require(session.State.Variables[512] == (quotient & 65535) && session.State.Variables[514] == (remainder & 65535), "Native CAL3 quotient/CAL4 remainder signed or zero result changed.");
        }
        GameSession invalidSession = new(new GameState());
        EventEngine invalid = new(invalidSession, invalidSession.State.Variables);
        invalid.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHM_W, 600), (Jsm.Opcode.CAL, 3), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)), []);
        try
        {
            invalid.Step();
            throw new InvalidOperationException("Native CAL division by zero produced a value.");
        }
        catch (EventScriptException)
        {
        }
    }

    private static void NativeBackgroundAnimationTiming()
    {
        (Jsm.Opcode, int)[] forward = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.BGANIME, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] reverse = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RBGANIME, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.BGANIMESYNC, 0), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPM_W, 516), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([0, 16 << 7], [forward, reverse]);
        program[2] = 2;
        program[3] = 0;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.BackgroundStates[0] == 1 && engine.BackgroundStates[1] == 2 && !session.State.Variables.ContainsKey(512) && session.State.Variables[514] == 9, "BGANIME did not block, RBGANIME did not continue, or initial inclusive frame order changed.");
        EventEngine? resumed = null;
        for (int frame = 2; frame <= 13; frame++)
        {
            engine.Step();
            resumed?.Step();
            int expectedForward = Math.Min(3, 1 + frame / 4);
            int expectedReverse = Math.Max(0, 2 - frame / 2);
            Require(engine.BackgroundStates[0] == expectedForward && engine.BackgroundStates[1] == expectedReverse, "Native per-module divider or reverse63-subunit starting phase changed an exact frame.");
            if (frame < 13)
            {
                Require(!session.State.Variables.ContainsKey(512), "BGANIME resumed before the inclusive last frame's complete hold.");
            }
            if (frame < 7)
            {
                Require(!session.State.Variables.ContainsKey(516), "BGANIMESYNC released before the reverse endpoint completed.");
            }
            if (frame == 5)
            {
                GameSession resumedSession = new(new GameState());
                resumed = new EventEngine(resumedSession, resumedSession.State.Variables);
                resumed.LoadField(100, program, []);
                resumed.Restore(engine.Capture());
            }
            if (resumed is not null)
            {
                Require(engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "Native background fixed position/divider phase changed across mid-animation restore.");
            }
        }
        Require(session.State.Variables[512] == 7 && session.State.Variables[516] == 11, "Native inclusive background animations failed their caller completion waits.");
        foreach (int rawSpeed in new[] { 0, -1, 65536 })
        {
            GameSession fastSession = new(new GameState());
            EventEngine fast = new(fastSession, fastSession.State.Variables);
            byte[] fastProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, rawSpeed), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BGANIME, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
            fastProgram[2] = 1;
            fastProgram[3] = 0;
            fast.LoadField(100, fastProgram, []);
            fast.Step();
            Require(fast.BackgroundStates[0] == 1, "Native signed16 zero/negative divider failed to advance on its starting module tick.");
            fast.Step();
            fast.Step();
            Require(fast.BackgroundStates[0] == 2 && !fastSession.State.Variables.ContainsKey(512), "Native zero/negative divider lost its inclusive endpoint hold.");
            fast.Step();
            Require(fastSession.State.Variables[512] == 7, "Native zero/negative divider failed to release BGANIME after its completion tick.");
        }
        GameSession holdSession = new(new GameState());
        EventEngine hold = new(holdSession, holdSession.State.Variables);
        byte[] holdProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.RBGANIME, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BGDRAW, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.BGOFF, 0), (Jsm.Opcode.RET, 0));
        holdProgram[2] = 1;
        holdProgram[3] = 0;
        hold.LoadField(100, holdProgram, []);
        hold.Step();
        hold.Step();
        var held = hold.Capture().GetProperty("BackgroundAnimations")[0];
        Require(hold.BackgroundStates[0] == 0 && !held.GetProperty("Active").GetBoolean() && held.GetProperty("Speed").GetInt32() == 1, "BGDRAW did not replace an active animation with native held-frame/divider1 state.");
        hold.Step();
        for (int tick = 0; tick < 10; tick++)
        {
            hold.Step();
            Require(hold.BackgroundStates[0] == -1, "BGOFF failed to hold nativeFFFF without an old animation restarting it.");
        }
    }

    private static void FrontInfirmaryCurtainCycles(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        GameSession entrySession = GameSession.CreateColdProcess();
        entrySession.StartNewGame();
        EventEngine entry = new(entrySession, entrySession.State.Variables);
        entry.MovieSkipRequested += entrySession.CompleteMovie;
        FieldAssets start = assets.LoadField(74);
        entry.AnimationFrameCount = start.GetAnimationFrameCount;
        entry.LoadField(74, start.Jsm, start.Messages);
        for (int action = 0; entrySession.State.FieldId == 74 && action < 20; action++)
        {
            PlayerAction next = entry.NextTrigger.Kind == PlayerTriggerKind.Movie ? new PlayerAction(PlayerActionKind.SkipMovie) : new PlayerAction(PlayerActionKind.Wait, Frames: 900);
            Require(entry.Apply(next).Accepted, "Original New Game entry rejected ordinary Wait/SkipMovie before field180.");
        }
        Require(entrySession.State.FieldId == 180 && entrySession.State.Scene == SceneKind.Field, "Original New Game source failed to request front infirmary180.");
        FieldAssets field = assets.LoadField(180);
        entry.AnimationFrameCount = field.GetAnimationFrameCount;
        entry.LoadField(180, field.Jsm, field.Messages);
        Require(entry.Frame == 0 && entry.Scene.Party.SequenceEqual([0]) && entry.Capture().GetProperty("BackgroundAnimations").EnumerateArray().All(animation => animation.GetProperty("NativeVersion").GetInt32() == 1 && !animation.GetProperty("Active").GetBoolean()), "Fresh ordinary field180 entry already contained a legacy curtain phase.");
        entrySession.SetEventState(entry.Capture());
        string beforeInitPath = Path.Combine(AppContext.BaseDirectory, "field180-native-before-init.json");
        entrySession.Save(beforeInitPath);
        EventEngine Create()
        {
            GameState state = GameSession.Load(beforeInitPath);
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount };
            engine.LoadField(180, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        EventEngine original = Create();
        var beforeInvalidRestore = original.Capture();
        GameState oldFront = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        foreach ((int last, bool complete) in new[] { (12, false), (9, false), (9, true) })
        {
            var diagnostic = System.Text.Json.Nodes.JsonNode.Parse(oldFront.EventState!.Value.GetRawText())!;
            diagnostic["BackgroundAnimations"]![0]!["Last"] = last;
            diagnostic["BackgroundAnimations"]![0]!["Complete"] = complete;
            using var document = System.Text.Json.JsonDocument.Parse(diagnostic.ToJsonString());
            try
            {
                original.Restore(document.RootElement);
                throw new InvalidOperationException("Legacy front-view curtain arithmetic/timing was silently converted.");
            }
            catch (InvalidDataException exception) when (exception.Message.Contains("field180-native-before-init.json", StringComparison.Ordinal))
            {
            }
            Require(beforeInvalidRestore.GetRawText() == original.Capture().GetRawText(), "Rejecting a legacy front-view curtain mutated the ordinary replay world.");
        }
        BackgroundTile[] curtain = field.Background.Where(tile => tile.AnimationId == 0).ToArray();
        Require(curtain.Length == 423 && curtain.Select(tile => tile.AnimationState).Distinct().Order().SequenceEqual(Enumerable.Range(0, 10)), "Actual front curtain MAP0..9 frames changed or contain a missing state.");
        EventObject module = JsmProgram.Read(field.Jsm).Objects.Single(obj => obj.BackgroundModuleIndex == 0);
        Require(module.Index == 13 && module.Label == 71 && module.Scripts[1].Id == 72, "Front curtain no longer maps module0 to original object13/script72.");
        int cycles = 0;
        bool reversed = false;
        HashSet<int> observed = [];
        original.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.BGANIME) && command.ObjectId == 13)
            {
                Require(command.Arguments.All(frame => frame is >= 0 and <= 9), "Original front curtain script selected a nonexistent native frame.");
                reversed |= command.Arguments[0] > command.Arguments[1];
                if (command.Operation == 1916)
                {
                    cycles++;
                }
            }
        };
        EventEngine? resumed = null;
        for (int tick = 0; tick < 5000; tick++)
        {
            original.Step();
            resumed?.Step();
            int frame = original.BackgroundStates[0];
            if (frame >= 0)
            {
                Require(frame <= 9, "Ordinary front-infirmary Steps selected a nonexistent curtain state.");
                observed.Add(frame);
            }
            if (resumed is null && original.Capture().GetProperty("BackgroundAnimations").EnumerateArray().Any(animation => animation.GetProperty("Id").GetInt32() == 0 && animation.GetProperty("Active").GetBoolean() && animation.GetProperty("Counter").GetInt32() > 0))
            {
                resumed = Create();
                resumed.Restore(original.Capture());
            }
            if (resumed is not null)
            {
                Require(original.Capture().GetRawText() == resumed.Capture().GetRawText(), "Front curtain native phase or subsequent cycle diverged across restore.");
            }
        }
        Require(cycles >= 32 && reversed && observed.SetEquals(Enumerable.Range(0, 10)) && original.FieldId == 180 && original.LastFault is null, "Front infirmary regression failed to cover32 native cycles without leaving its ordinary world.");
        Console.WriteLine($"Curtain180: New Game74 ordinary Wait/SkipMovie -> nativebeforeinit180/frame0; {cycles} ordinary cycles/allMAPstates0..9/exactmidrestore; legacy active/completed rejected.");
    }

    private static void InfirmaryCurtainCycles(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(181);
        EventEngine Create()
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount };
            engine.LoadField(181, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        EventEngine engine = Create();
        var beforeInvalidRestore = engine.Capture();
        GameState invalidLegacy = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        try
        {
            engine.Restore(invalidLegacy.EventState!.Value);
            throw new InvalidOperationException("The known legacy curtain0..29 animation was silently converted or clamped.");
        }
        catch (InvalidDataException exception) when (exception.Message.Contains("field181-side-start.json", StringComparison.Ordinal))
        {
        }
        Require(beforeInvalidRestore.GetRawText() == engine.Capture().GetRawText(), "Rejecting invalid legacy curtain state changed the existing VM.");
        foreach (bool complete in new[] { false, true })
        {
            // Construct diagnostic inputs only; the ordinary cycle world is never changed.
            var plausibleLegacy = System.Text.Json.Nodes.JsonNode.Parse(invalidLegacy.EventState!.Value.GetRawText())!;
            plausibleLegacy["BackgroundAnimations"]![0]!["First"] = 0;
            plausibleLegacy["BackgroundAnimations"]![0]!["Last"] = 9;
            plausibleLegacy["BackgroundAnimations"]![0]!["Complete"] = complete;
            using var plausibleDocument = System.Text.Json.JsonDocument.Parse(plausibleLegacy.ToJsonString());
            try
            {
                engine.Restore(plausibleDocument.RootElement);
                throw new InvalidOperationException("A plausible legacy curtain range retained arithmetic state from before the native fix.");
            }
            catch (InvalidDataException exception) when (exception.Message.Contains("field181-side-start.json", StringComparison.Ordinal))
            {
            }
            Require(beforeInvalidRestore.GetRawText() == engine.Capture().GetRawText(), "Rejecting a valid-range or completed legacy curtain changed the existing VM.");
        }
        int cycles = 0;
        bool reversed = false;
        HashSet<int> observed = [];
        engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.BGANIME))
            {
                Require(command.Arguments.All(frame => frame is >= 0 and <= 9), "Actual curtain script selected a frame beyond its native MAP0..9 states.");
                reversed |= command.Arguments[0] > command.Arguments[1];
                if (command.Operation == 190)
                {
                    cycles++;
                }
            }
        };
        EventEngine? resumed = null;
        for (int tick = 0; tick < 3000; tick++)
        {
            engine.Step();
            resumed?.Step();
            int frame = engine.BackgroundStates[0];
            Require(frame is >= 0 and <= 9, "Ordinary infirmary Steps selected a nonexistent curtain frame.");
            observed.Add(frame);
            if (resumed is null && engine.Capture().GetProperty("BackgroundAnimations").EnumerateArray().Any(animation => animation.GetProperty("Id").GetInt32() == 0 && animation.GetProperty("Active").GetBoolean() && animation.GetProperty("Counter").GetInt32() > 0))
            {
                resumed = Create();
                resumed.Restore(engine.Capture());
            }
            if (resumed is not null)
            {
                Require(engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "Actual curtain cycle diverged after restoring native mid-animation phase.");
            }
        }
        Require(cycles >= 3 && reversed && observed.SetEquals(Enumerable.Range(0, 10)), "Actual curtain regression failed to cover repeated forward/reverse cycles and all ten real MAP states.");
        Console.WriteLine($"Curtain181: {cycles} ordinary cycles, MAPstates0..9 only, forward/reverse native divider waits and exact mid-animation restore.");
    }

    private static void ClassroomExitTransition(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        GameState state = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        GameSession session = new(state);
        EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
        engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out System.Numerics.Vector3 position, out int triangle, engine.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
        engine.LoadField(232, field.Jsm, field.Messages);
        engine.Restore(state.EventState!.Value);
        Require(engine.Frame == 2420 && engine.IsUserControlEnabled && engine.LastFault is null && engine.Scene.UnlockedTutorials.SetEquals([0]), "Classroom exit fixture lost its exact ordinary playable state.");
        var legacyBackground = engine.Capture().GetProperty("BackgroundAnimations")[0];
        Require(legacyBackground.GetProperty("NativeVersion").GetInt32() == 0 && legacyBackground.GetProperty("Progress").GetSingle() == 1.2f, "Classroom legacy background was silently converted instead of retaining its explicit compatibility phase.");
        engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 1));
        var requested = engine.Capture();
        var caller = requested.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 22 && fiber.GetProperty("ScriptId").GetInt32() == 207);
        FieldTransition expected = new(139, -3172, -5705, 130, 72, 8341);
        Require(engine.Frame == 2421 && engine.LastFault is null && caller.GetProperty("Position").GetInt32() == 3815 && caller.GetProperty("Stack").GetArrayLength() == 0 && requested.GetProperty("TransitionRequested").GetBoolean(), "MAPJUMP3 failed to consume its packet once and stop the departing VM.");
        Require(session.State.FieldId == 139 && session.State.FieldArrival == expected && session.State.EntryPoint == 0 && engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition && engine.Scene.PreparedField == 234, "MAPJUMP3 mistook its extra word or cached prepared field for its native destination.");
        Require(state.Variables[256] == 17 && requested.GetProperty("Variables").EnumerateObject().Where(variable => variable.Name != "256").Select(variable => (variable.Name, variable.Value.GetInt32())).SequenceEqual(state.EventState.Value.GetProperty("Variables").EnumerateObject().Where(variable => variable.Name != "256").Select(variable => (variable.Name, variable.Value.GetInt32()))) && engine.Scene.UnlockedTutorials.SetEquals([0]), "The transition changed globals beyond the original exit script's POPM_W256=17.");
        engine.Step();
        Require(requested.GetRawText() == engine.Capture().GetRawText(), "Departing VM executed again after requesting its field transition.");
        GameState restoredState = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(session.State))!;
        Require(restoredState.FieldArrival == expected, "MAPJUMP3 native packet failed to survive the host checkpoint.");
        FieldAssets destination = assets.LoadField(139);
        HashSet<int> otherTriangles = Enumerable.Range(0, destination.Walkmesh.Triangles.Count).Where(index => index != expected.Triangle).ToHashSet();
        bool resolved = destination.Walkmesh.TryPosition(expected.X, expected.Y, out System.Numerics.Vector3 spawn, out int triangle, otherTriangles);
        Require(destination.Name == "bg2f_2" && resolved && triangle == 130 && float.IsFinite(spawn.Z), "Actual MAPJUMP3 XY does not lie on its specified destination triangle.");
        engine.AnimationFrameCount = destination.GetAnimationFrameCount;
        engine.ResolveFieldArrival = arrival => destination.Walkmesh.TryPosition(arrival.X, arrival.Y, out System.Numerics.Vector3 position, out int selected, otherTriangles) ? new WalkmeshPosition(position, selected) : null;
        engine.LoadField(139, destination.Jsm, destination.Messages, session.State.FieldArrival);
        var pending = engine.Capture();
        engine.Restore(pending);
        Require(pending.GetRawText() == engine.Capture().GetRawText(), "Actual destination arrival changed before its original initialization.");
        while (engine.Capture().GetProperty("PendingArrival").ValueKind != System.Text.Json.JsonValueKind.Null && engine.Frame < 30)
        {
            engine.Step();
        }
        ActorState player = engine.Actors.Single(actor => actor.Active && actor.CharacterId == 0);
        Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 5, 16, 18, 19, 20, 21, 22 }), "Original field139 init retained the wrong compact actor order or included doors/modules/areas.");
        Require(engine.Capture().GetProperty("PendingArrival").ValueKind == System.Text.Json.JsonValueKind.Null && player.X == -3172 && player.Y == -5705 && player.Z == spawn.Z && player.WalkmeshTriangle == 130 && player.Direction == 72 && engine.Scene.UnlockedTutorials.SetEquals([0]) && session.State.Variables[256] == 17, "Actual destination init failed to apply native arrival before default scripts or preserve persistent scene state.");
        Console.WriteLine($"Field139 original init arrival applied at frame{engine.Frame}; player{player.ObjectId} XY({player.X},{player.Y}) Z{player.Z}/triangle{player.WalkmeshTriangle}/dir{player.Direction}.");
        GameState faultState = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        GameSession retrySession = new(faultState);
        EventEngine retry = new(retrySession, faultState.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
        retry.LoadField(232, field.Jsm, field.Messages);
        retry.Restore(faultState.EventState!.Value);
        var retryCaller = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 22 && fiber.GetProperty("ScriptId").GetInt32() == 207);
        Require(retry.LastFault is null && retryCaller.GetProperty("Position").GetInt32() == 3814 && retryCaller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 72, 8341, -5705, -3172, 139 }), "Source-upgrade retry altered the saved MAPJUMP3 PC or operands.");
        retry.Step();
        Require(retrySession.State.FieldArrival == expected, "Saved MAPJUMP3 boundary retry requested a different arrival packet.");
        Console.WriteLine($"MAPJUMP3: ordinary Move(0,-1,1)2420 -> field139/bg2f_2 XY(-3172,-5705) triangle130 dir72 extra8341; destination planeZ={spawn.Z}; saved fault retry preserves source operands.");
    }

    private static void MapJump3PacketAndArrival()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field });
        EventEngine engine = new(session, session.State.Variables);
        byte[] departure = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, 65526), (Jsm.Opcode.PSHN_L, -65516), (Jsm.Opcode.PSHN_L, 98305), (Jsm.Opcode.PSHN_L, 288), (Jsm.Opcode.MAPJUMP3, 50), (Jsm.Opcode.PSHN_L, 999), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        engine.LoadField(101, departure, []);
        engine.Step();
        FieldTransition arrival = new(100, -10, 20, 50, 32, -32767);
        Require(session.State.FieldArrival == arrival && !session.State.Variables.ContainsKey(512), "MAPJUMP3 packet truncation or stop-before-next-instruction failed.");
        byte[] destination = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 111), (Jsm.Opcode.PSHN_L, 222), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0));
        engine.ResolveFieldArrival = request => new WalkmeshPosition(new System.Numerics.Vector3(request.X, request.Y, -7), request.Triangle);
        engine.LoadField(100, destination, [], arrival);
        var pending = engine.Capture();
        Require(pending.GetProperty("PendingArrival").GetProperty("NativeExtraWord").GetInt32() == -32767, "Pending arrival discarded its native packet word.");
        engine.Restore(pending);
        Require(pending.GetRawText() == engine.Capture().GetRawText(), "Pending MAPJUMP3 arrival changed during restore.");
        engine.Step();
        Require(engine.Actors[0].X == -10 && engine.Actors[0].Y == 20 && engine.Actors[0].Z == -7 && engine.Actors[0].WalkmeshTriangle == 50 && engine.Actors[0].Direction == 32 && engine.Capture().GetProperty("PendingArrival").ValueKind == System.Text.Json.JsonValueKind.Null, "MAPJUMP3 arrival used its extra word as height or failed to override init before defaults.");
    }

    private static void AdditionalActorControlAndGuard()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { CanDisableAdditionalControl = _ => true };
        byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.RET, 0));
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.Actors[0].AdditionalControlDisabled && engine.Actors[0].Active && engine.Actors[0].Visible && engine.Actors[0].CollisionEnabled && engine.IsUserControlEnabled, "ACTORMODE3 changed ordinary actor visibility, collision or control instead of its additional-control flag.");
        var disabled = engine.Capture();
        engine.Restore(disabled);
        Require(disabled.GetRawText() == engine.Capture().GetRawText(), "ACTORMODE3 actor state did not round-trip exactly.");
        engine.Step();
        Require(!engine.Actors[0].AdditionalControlDisabled, "ACTORMODE2 failed to clear the additional-control flag.");
        foreach (int mode in new[] { 0, 1, 4, -1, 65539, 3 })
        {
            byte[] guarded = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, mode), (Jsm.Opcode.ACTORMODE, 0), (Jsm.Opcode.RET, 0));
            engine.CanDisableAdditionalControl = _ => false;
            engine.LoadField(101, guarded, []);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Unproved ACTORMODE semantics were accepted.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.ACTORMODE))
            {
            }
            var frozen = engine.Capture();
            var caller = frozen.GetProperty("Fibers")[0];
            Require(caller.GetProperty("Position").GetInt32() == 2 && caller.GetProperty("Stack")[0].GetInt32() == mode && !engine.Actors[0].AdditionalControlDisabled, "Unsupported ACTORMODE consumed or truncated its original32-bit operand before failing.");
            engine.Restore(frozen);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Restored unproved ACTORMODE continued.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.ACTORMODE))
            {
            }
            Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Contextually unsupported ACTORMODE did not remain frozen through Restore/Step.");
        }
        engine.CanDisableAdditionalControl = _ => true;
        engine.Restore(engine.Capture());
        Require(engine.LastFault is null, "Proved ACTORMODE3 metadata failed to unlock the preserved boundary.");
        engine.Step();
        Require(engine.Actors[0].AdditionalControlDisabled, "ACTORMODE3 metadata retry failed to apply its preserved operand.");
    }

    private static void GardenAdditionalActorControl(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        GameState state = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        (GameSession Session, EventEngine Engine) Create(string checkpoint = "player-garden-139-postinit-preboundary-checkpoint.json")
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures", checkpoint)));
            EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.CanDisableAdditionalControl = actor =>
            {
                FieldModel model = field.Models[actor.ModelId];
                int bone = model.ControlBones.Additional;
                return bone < 0 || !model.Bones.Any(item => item.Parent == bone) && model.Faces.All(face => face.Vertices.Take(face.Quad ? 4 : 3).All(vertex => model.Vertices[vertex].Bone == bone) || face.Vertices.Take(face.Quad ? 4 : 3).All(vertex => model.Vertices[vertex].Bone != bone));
            };
            engine.LoadField(139, field.Jsm, field.Messages);
            engine.Restore(session.State.EventState!.Value);
            return (session, engine);
        }
        var first = Create();
        var replayed = Create();
        Require(first.Engine.Frame == 3 && first.Engine.LastFault is null && first.Engine.Actors[0].ModelId == 0 && !first.Engine.Actors[0].AdditionalControlDisabled && field.Models[0].ControlBones.Additional == 24 && field.Models[0].Vertices.Count(vertex => vertex.Bone == 24) == 49, "Actual ACTORMODE source or selected additional control group changed.");
        first.Engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Engine.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(first.Engine.Capture().GetRawText() == replayed.Engine.Capture().GetRawText() && first.Engine.Actors[0].AdditionalControlDisabled && first.Engine.LastFault is null, "Ordinary ACTORMODE3 Wait1 and replay diverged or failed to preserve native actor state.");
        var applied = first.Engine.Capture();
        first.Engine.Restore(applied);
        Require(applied.GetRawText() == first.Engine.Capture().GetRawText(), "Actual ACTORMODE state changed during restoration.");
        Require(applied.GetProperty("Variables").EnumerateObject().Select(variable => (variable.Name, variable.Value.GetInt32())).SequenceEqual(state.EventState!.Value.GetProperty("Variables").EnumerateObject().Select(variable => (variable.Name, variable.Value.GetInt32()))), "ACTORMODE3 changed story or temporary variables.");
        var retry = Create("player-garden-139-postinit-checkpoint.json");
        var retryCaller = retry.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(retry.Engine.LastFault is null && retryCaller.GetProperty("Position").GetInt32() == 15 && retryCaller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 3 }), "ACTORMODE source-upgrade retry changed its exact saved PC or operand.");
        retry.Engine.Step();
        Require(retry.Engine.Actors[0].AdditionalControlDisabled && retry.Engine.LastFault is null, "Exact saved ACTORMODE fault retry failed to apply its original mode3.");
        Console.WriteLine($"ACTORMODE3: genuine field139 Wait1/frame3 -> frame{first.Engine.Frame}; Additional24 leaf/49vertices suppressed; exact replay/restore, no control or story-variable change.");
    }

    private static void NativeAreaRequestAndGuard()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.REQ, 3), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 5), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([2 << 7, (3 << 7) | 2, 6 << 7, 1], [init, caller, init, init, init, requested, init]);
        program[0] = 1;
        program[1] = 1;
        program[2] = 1;
        program[3] = 1;
        JsmProgram parsed = JsmProgram.Read(program);
        Require(parsed.NativeObjects.SequenceEqual(new[] { 0, 1, 3, 2 }) && parsed.Objects.Single(obj => obj.IsArea).Label == 3, "Native objects/doors/modules/areas permutation disagrees with the original doors-before-areas raw group consumption.");
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        engine.Step();
        engine.Step();
        var pending = engine.Capture();
        engine.Restore(pending);
        engine.Step();
        Require(session.State.Variables[512] == 7 && engine.LastFault is null, "REQ did not resolve its global script label on the native area object.");
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 99), (opcode, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Invalid REQ target script consumed its operands before failing.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            var frozen = engine.Capture();
            var fiber = frozen.GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 99, 3 }), "Invalid REQ target script did not preserve its original PC/operands.");
            engine.Restore(frozen);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Invalid REQ target script resumed after restore.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Invalid REQ target script did not remain exactly frozen.");
        }
    }

    private static void GardenAreaRequestBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        EventEngine Create()
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, CanDisableAdditionalControl = _ => true };
            engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out System.Numerics.Vector3 position, out int triangle, engine.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            engine.LoadField(139, field.Jsm, field.Messages);
            FieldInformation information = field.Information ?? throw new InvalidDataException("Selected original field139 INF was not decoded.");
            engine.InfGateways = information.Gateways.Where(gateway => gateway.Defined).Select(gateway => new InfGateway(gateway.Start, gateway.End, new FieldTransition(gateway.DestinationField, gateway.DestinationX, gateway.DestinationY, (ushort)gateway.DestinationTriangle, gateway.Direction))).ToArray();
            engine.InfAreaTriggers = information.AreaTriggers.Select(trigger => new InfAreaTrigger(trigger.Start, trigger.End, trigger.AreaId, trigger.Side)).ToArray();
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        EventEngine engine = Create();
        Require(engine.Frame == 5 && engine.LastFault is null && engine.NextTrigger.Kind == PlayerTriggerKind.Playable, "Actual area-request source is not its untouched ordinary frame5 checkpoint.");
        engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 1));
        EventEngine replay = Create();
        replay.Replay([new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 1)]);
        Require(engine.Frame == 6 && engine.LastFault is null && !engine.Actors[23].DoorLineEnabled && engine.Actors[23].LineEnabled && engine.Capture().GetRawText() == replay.Capture().GetRawText(), "Corrected ordinary area request/gate failed its native target, independent line state or exact replay.");
        var applied = engine.Capture();
        engine.Restore(applied);
        Require(applied.GetRawText() == engine.Capture().GetRawText(), "Actual area gate state changed after restore.");
        using var frozenDocument = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine("../fixtures/external-fixture.json")));
        var frozen = frozenDocument.RootElement;
        Require(frozen.GetProperty("LastFault").GetProperty("Operation").GetInt32() == 10673 && frozen.GetProperty("LastFault").GetProperty("Opcode").GetString() == nameof(Jsm.Opcode.DOORLINEOFF), "Selected frozen component proof is not the genuine next area command.");
        EventEngine retry = Create();
        retry.Restore(frozen);
        var pending = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 23 && fiber.GetProperty("ScriptId").GetInt32() == 243);
        Require(retry.LastFault is null && pending.GetProperty("Position").GetInt32() == 10673 && pending.GetProperty("Stack").GetArrayLength() == 0 && retry.Actors[23].DoorLineEnabled, "DOORLINEOFF source-upgrade retry edited its original gate, PC or empty stack.");
        retry.Step();
        Require(!retry.Actors[23].DoorLineEnabled && retry.LastFault is null, "Frozen genuine DOORLINEOFF boundary failed to execute its exact gate write.");
        Console.WriteLine("REQ/DOORLINEOFF: untouched field139 ordinary Move1/frame5 -> native29/area23/script243/frame6 disables INF gate; exact replay/restore/component fault retry, SETLINE gate unchanged.");
    }

    private static void RefreshPartyBindingsAndGuard()
    {
        (Jsm.Opcode, int)[] first = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] door = [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] duplicate = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([8 << 7, 16 << 7, 0, 32 << 7, (48 << 7) | 1], [first, door, duplicate, unused, callerInit, caller]);
        program[1] = 1;
        program[3] = 4;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        engine.Step();
        engine.Step();
        string actors = engine.Capture().GetProperty("Actors").GetRawText();
        engine.Step();
        Require(engine.Scene.Party.SequenceEqual(new[] { 5 }) && engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 3, 4 }) && engine.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 255, 255 }) && session.State.Variables[194] == 0 && session.State.Variables[195] == 255 && session.State.Variables[196] == 255, "REFRESHPARTY used raw/door identity, retained an init-disabled duplicate, or matched defaultFE to emptyFF.");
        Require(session.State.Variables[512] == 123 && engine.Capture().GetProperty("Actors").GetRawText() == actors, "REFRESHPARTY consumed operands or changed actor flags, movement or geometry.");
        var snapshot = engine.Capture();
        engine.Restore(snapshot);
        Require(snapshot.GetRawText() == engine.Capture().GetRawText(), "REFRESHPARTY's typed and script-visible binding bytes failed exact restore.");
        foreach ((int address, int value) in new[] { (104, 0x800), (105, 8) })
        {
            GameSession guarded = new(new GameState());
            guarded.State.Variables[address] = value;
            EventEngine blocked = new(guarded, guarded.State.Variables);
            blocked.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)), []);
            try
            {
                blocked.Step();
                throw new InvalidOperationException("REFRESHPARTY silently entered its unproved auxiliary-party branch.");
            }
            catch (EventScriptException) when (blocked.LastFault?.Opcode == nameof(Jsm.Opcode.REFRESHPARTY))
            {
            }
            var frozen = blocked.Capture();
            Require(frozen.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && frozen.GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 123 && blocked.Scene.FieldPartyNativeActorIds.Count == 0, "Unsupported REFRESHPARTY advanced PC, consumed stack or rebuilt its bindings.");
            blocked.Restore(frozen);
            try
            {
                blocked.Step();
                throw new InvalidOperationException("Unsupported auxiliary-party branch continued after restore.");
            }
            catch (EventScriptException)
            {
            }
            Require(frozen.GetRawText() == blocked.Capture().GetRawText(), "Unsupported auxiliary-party refresh failed to remain exactly frozen.");
        }
    }

    private static void NativeActorCompactionAndNullRequests()
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            (Jsm.Opcode, int)[] removed = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 999), (opcode, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
            byte[] program = SyntheticObjects([16 << 7, 1], [callerInit, caller, removed]);
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, program, []);
            engine.Step();
            Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0 }) && engine.Actors.All(actor => actor.CharacterId == -2 && actor.PushRadius == 48 && actor.TalkRadius == 128), "Native compaction lost its raw null slot or native FE/push48/talk128 defaults.");
            var initialized = engine.Capture();
            engine.Step();
            Require(engine.LastFault is null && session.State.Variables[512] == 123 && engine.Capture().GetProperty("Fibers").EnumerateArray().All(fiber => fiber.GetProperty("Initialization").GetBoolean()), "Null native request did not consume exactly two operands and return without a request/wait.");
            string completed = engine.Capture().GetRawText();
            engine.Restore(initialized);
            engine.Step();
            Require(completed == engine.Capture().GetRawText(), "Raw null request changed after retained-layout restoration.");
        }
        List<(Jsm.Opcode, int)> delayed = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UNUSE, 0)];
        delayed.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 20));
        delayed.Add((Jsm.Opcode.USE, 0));
        delayed.Add((Jsm.Opcode.RET, 0));
        GameSession delayedSession = new(new GameState());
        EventEngine delayedEngine = new(delayedSession, delayedSession.State.Variables);
        delayedEngine.LoadField(100, SyntheticProgram(delayed.ToArray()), []);
        delayedEngine.Step();
        Require(!delayedEngine.Actors[0].Active && delayedEngine.Scene.NativeActorLayoutVersion == 0, "Budget-limited init compacted before its original RET.");
        var midInit = delayedEngine.Capture();
        delayedEngine.Restore(midInit);
        delayedEngine.Step();
        Require(delayedEngine.Actors[0].Active && delayedEngine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0 }) && delayedEngine.Scene.NativeActorLayoutVersion == 1, "Post-init compaction missed later original USE after exact mid-init restoration.");
    }

    private static void RefreshedPartyTurnTarget()
    {
        (Jsm.Opcode, int)[] active = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] inactive = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] disable = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([(16 << 7) | 1, 0, (32 << 7) | 1], [active, inactive, disable, callerInit, caller]);
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 1, 0, 2 }) && engine.Actors[1].Active, "Post-init compaction failed to retain the original first native actor before later UNUSE.");
        engine.Step();
        Require(!engine.Actors[1].Active && engine.Actors[0].Active && engine.Scene.FieldPartyNativeActorIds[0] == 0 && session.State.Variables[512] == 10 && engine.Actors[2].RotationTarget == 64, "Refreshed PGETINFO/PCTURN re-compacted a later UNUSED actor or selected the active sorted duplicate.");
        var pending = engine.Capture();
        engine.Step();
        string completed = engine.Capture().GetRawText();
        Require(engine.Actors[2].Direction == 64 && !engine.Actors[1].Active && engine.Actors[1].X == 10 && engine.Actors[0].X == -10, "Refreshed PCTURN changed its target or failed its native fixed angle.");
        engine.Restore(pending);
        engine.Step();
        Require(completed == engine.Capture().GetRawText(), "Refreshed duplicate-target PCTURN changed its fixed target after exact restore.");
    }

    private static void GardenRefreshPartyBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(139);
        GameState state = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        GameSession session = new(state);
        EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, CanDisableAdditionalControl = _ => true };
        engine.LoadField(139, field.Jsm, field.Messages);
        engine.Restore(state.EventState!.Value);
        Require(engine.Frame == 1832 && engine.LastFault is null && engine.Scene.Party.SequenceEqual(new[] { 0 }) && engine.Scene.NativeActorLayoutVersion == 0, "Legacy tour-ending source was changed or falsely assigned a compact actor history.");
        try
        {
            engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
            throw new InvalidOperationException("Legacy field139 silently inferred original retention from current actor flags.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.REFRESHPARTY))
        {
        }
        var frozen = engine.Capture();
        var caller = frozen.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 17 && fiber.GetProperty("ScriptId").GetInt32() == 119);
        Require(caller.GetProperty("Position").GetInt32() == 4384 && caller.GetProperty("Stack").GetArrayLength() == 0 && engine.Scene.FieldPartyNativeActorIds.Count == 0 && engine.Scene.Party.SequenceEqual(new[] { 0, 5 }), "Legacy compaction boundary changed REFRESHPARTY PC/operands/bindings or lost the original preceding party addition.");
        engine.Restore(frozen);
        try
        {
            engine.Step();
            throw new InvalidOperationException("Legacy compaction boundary continued after restore.");
        }
        catch (EventScriptException)
        {
        }
        Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Legacy missing-layout boundary failed exact frozen restoration.");
        GameState incorrect = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        try
        {
            engine.Restore(incorrect.EventState!.Value);
            throw new InvalidOperationException("Old raw-index refreshed bindings were accepted as compact native indices.");
        }
        catch (InvalidDataException exception) when (exception.Message.Contains("Legacy refreshed party bindings"))
        {
        }
        Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Rejected old refreshed checkpoint partially mutated the live VM.");
        Console.WriteLine("Legacy REFRESHPARTY snapshots: original frame1832 stops at unchanged PC4384 until replay before field139 init; incorrect raw bindings in1833 are rejected without mutation.");
    }

    private static void CommitComponentPlayer(EventEngine engine, float x, float y, float z, int triangle)
    {
        // Component-only INF/commit-boundary tests; no second production input API.
        try
        {
            typeof(EventEngine).GetMethod("MovePlayerCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(engine, [x, y, z, triangle, null, null, false, null]);
        }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static void InfMovementAndContactPersistence()
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaDefault = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaEntry = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([(16 << 7) | 2, 0], [player, areaInit, areaDefault, areaEntry]);
        program[0] = 1;
        program[3] = 1;
        GameSession session = new(new GameState());
        EventEngine Create()
        {
            EventEngine result = new(session, session.State.Variables);
            result.LoadField(100, program, []);
            return result;
        }
        InfAreaTrigger trigger = new(new System.Numerics.Vector3(0, -20, 0), new System.Numerics.Vector3(0, 20, 0), 0, 0);
        EventEngine engine = Create();
        engine.Step();
        engine.InfAreaTriggers = [trigger];
        CommitComponentPlayer(engine, -1, 0, 0, 0);
        Require(engine.Actors[1].DoorLineContact && engine.Actors[1].DoorLineEntryPending && !session.State.Variables.ContainsKey(512), "INF entry failed to use current proximity/facing and defer its original script.");
        var pending = engine.Capture();
        EventEngine restored = Create();
        restored.InfAreaTriggers = [trigger];
        restored.Restore(pending);
        Require(pending.GetRawText() == restored.Capture().GetRawText(), "INF per-area contact/pending entry did not survive exact restore.");
        engine.Step();
        restored.Step();
        Require(session.State.Variables[512] == 7 && !engine.Actors[1].DoorLineEntryPending && engine.Capture().GetRawText() == restored.Capture().GetRawText(), "INF entry failed to execute original local2 at native priority6 after restore.");
        long sequence = restored.Capture().GetProperty("Sequence").GetInt64();
        CommitComponentPlayer(restored, 0, 0, 0, 0);
        restored.Step();
        Require(restored.Capture().GetProperty("Sequence").GetInt64() == sequence, "INF side0 contact incorrectly rearmed while already contacted.");

        foreach (byte side in new byte[] { 0, 1, 2, 3, 4, 5 })
        {
            engine = Create();
            engine.Step();
            engine.InfAreaTriggers = [trigger];
            CommitComponentPlayer(engine, -1, 0, 0, 0);
            engine.Step();
            engine.InfAreaTriggers = [trigger with { Side = side }];
            CommitComponentPlayer(engine, 0, 0, 0, 0);
            Require(engine.Actors[1].DoorLineContact == (side % 2 == 0), $"INF near mode{side} failed native even-entry/odd-clear contact semantics.");
        }
        foreach (byte side in new byte[] { 0, 1, 2, 3, 4, 5 })
        {
            engine = Create();
            engine.Step();
            engine.InfAreaTriggers = [trigger with { Start = new System.Numerics.Vector3(-200, -20, 0), End = new System.Numerics.Vector3(-200, 20, 0), Side = side }];
            CommitComponentPlayer(engine, -1, 0, 0, 0);
            Require(engine.Actors[1].DoorLineContact == (side is 3 or 5), $"INF far mode{side} failed native inverse/contact-side behavior.");
        }
        engine = Create();
        engine.Step();
        engine.InfAreaTriggers = [trigger with { Start = new System.Numerics.Vector3(200, -20, 0), End = new System.Numerics.Vector3(200, 20, 0), Side = 5 }];
        CommitComponentPlayer(engine, -1, 0, 0, 0);
        Require(!engine.Actors[1].DoorLineContact, "INF far mode5 ignored its signed-side condition.");

        FieldTransition packet = new(143, -2466, -5997, 37, 185);
        InfGateway gateway = new(new System.Numerics.Vector3(0, -20, 0), new System.Numerics.Vector3(0, 20, 0), packet);
        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(session.State.FieldArrival == packet && engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "INF gateway failed to preserve its native field/XY/triangle/direction packet.");
        long departureFrame = engine.Frame;
        engine.Step();
        Require(engine.Frame == departureFrame, "INF gateway executed the departing VM after its packet request.");

        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway];
        CommitComponentPlayer(engine, 1, 0, 200, 0);
        Require(engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "INF gateway used proposed walkmesh Z instead of native current actor Z.");

        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway with { Start = new System.Numerics.Vector3(0, -20, 100), End = new System.Numerics.Vector3(0, 20, 100) }];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(engine.NextTrigger.Kind != PlayerTriggerKind.FieldTransition, "INF strict 3D distance accepted a gateway exactly one push radius away.");
        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway with { Start = new System.Numerics.Vector3(0, 10, 0), End = new System.Numerics.Vector3(0, 20, 0) }];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(engine.NextTrigger.Kind != PlayerTriggerKind.FieldTransition, "INF line distance clamped an out-of-segment projection into a crossing.");

        foreach (int fieldId in new[] { 0, 71 })
        {
            engine = Create();
            engine.Step();
            engine.InfGateways = [gateway with { Destination = packet with { FieldId = fieldId } }];
            var before = engine.Capture();
            FieldTransition? priorPacket = session.State.FieldArrival;
            try
            {
                CommitComponentPlayer(engine, 1, 0, 0, 0);
                throw new InvalidOperationException("INF world gateway used the field arrival service.");
            }
            catch (NotSupportedException)
            {
            }
            Require(before.GetRawText() == engine.Capture().GetRawText() && session.State.FieldArrival == priorPacket, "Unsupported world INF gateway changed movement, direction, contact or packet before rejecting.");
        }
        Console.WriteLine("INF routing: finite quantized3D gateway crossing/currentZ/typed packet and safe world rejection; all six native modes, original local2/priority6, exact pending/contact restore.");
    }

    private static void DoorLineGateAndGuard()
    {
        byte[] area = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.RET, 0));
        area[0] = 1;
        area[3] = 0;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, area, []);
        Require(engine.Actors[0].DoorLineEnabled, "Native area gate failed to initialize enabled before its script.");
        engine.Step();
        var disabled = engine.Capture();
        Require(!engine.Actors[0].DoorLineEnabled && engine.Actors[0].LineEnabled && !engine.Actors[0].LineContact && engine.IsUserControlEnabled, "DOORLINEOFF changed the independent SETLINE contact/gate or player control.");
        engine.Restore(disabled);
        Require(disabled.GetRawText() == engine.Capture().GetRawText(), "Disabled area gate did not survive exact snapshot restoration.");
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            engine.Step();
            throw new InvalidOperationException("DOORLINEOFF accepted an unproved non-area entity layout.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.DOORLINEOFF))
        {
        }
        var frozen = engine.Capture();
        Require(frozen.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 1 && engine.Actors[0].DoorLineEnabled, "Unproved DOORLINEOFF advanced its PC or changed its gate.");
        engine.Restore(frozen);
        try
        {
            engine.Step();
            throw new InvalidOperationException("Unproved DOORLINEOFF resumed after restore.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.DOORLINEOFF))
        {
        }
        Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Unproved non-area DOORLINEOFF failed to remain frozen.");
    }

    private static void TutorialUnlockPersistence()
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        List<(Jsm.Opcode, int)> commands = [(Jsm.Opcode.LBL, 0)];
        foreach (int value in new[] { 0, 127, 128, -1, 0 })
        {
            commands.Add((Jsm.Opcode.PSHN_L, value));
            commands.Add((Jsm.Opcode.MENUTIPS, 0));
        }
        commands.Add((Jsm.Opcode.PSHN_L, 11));
        commands.Add((Jsm.Opcode.POPM_W, 512));
        commands.Add((Jsm.Opcode.RET, 0));
        byte[] program = SyntheticProgram(commands.ToArray());
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.Scene.UnlockedTutorials.SetEquals([0, 127]) && session.State.Variables[512] == 11 && engine.IsUserControlEnabled && !engine.IsDialogueOpen && engine.Scene.MenuEnabled, "MENUTIPS failed its low7-bit mask/dedup or blocked ordinary caller continuation.");
        var unlocked = engine.Capture();
        engine.Restore(unlocked);
        Require(unlocked.GetRawText() == engine.Capture().GetRawText(), "Tutorial unlock IDs did not round-trip through the typed Scene checkpoint.");
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        engine.Step();
        Require(engine.Scene.UnlockedTutorials.SetEquals([0, 127]), "A field change erased the persistent native tutorial-info bits.");
        engine.LoadField(100, program, []);
        engine.Restore(unlocked);
        Require(unlocked.GetRawText() == engine.Capture().GetRawText(), "Restoring a tutorial checkpoint after a field change altered its IDs or VM state.");
        var invalid = System.Text.Json.Nodes.JsonNode.Parse(unlocked.GetRawText())!;
        invalid["Scene"]!["UnlockedTutorials"] = new System.Text.Json.Nodes.JsonArray(128);
        using var invalidDocument = System.Text.Json.JsonDocument.Parse(invalid.ToJsonString());
        try
        {
            engine.Restore(invalidDocument.RootElement);
            throw new InvalidOperationException("An out-of-bitmap tutorial unlock ID was restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(unlocked.GetRawText() == engine.Capture().GetRawText(), "Invalid tutorial checkpoint rejection changed existing VM state.");
    }

    private static void ClassroomTutorialUnlockBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        EventEngine Create(string leaf)
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures", leaf));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(232, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            Require(engine.Scene.UnlockedTutorials.Count == 0, "A legacy checkpoint without tutorial state acquired unlocks during restore.");
            return engine;
        }
        const string beforeLeaf = "player-performance-menutips-preboundary-checkpoint.json";
        EventEngine original = Create(beforeLeaf);
        EventEngine replayed = Create(beforeLeaf);
        Require(original.Frame == 2333 && !original.IsUserControlEnabled, "MENUTIPS exact-before fixture lost its frame or original control state.");
        original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(original.Capture().GetRawText() == replayed.Capture().GetRawText(), "Exact-before MENUTIPS action replay changed the full VM state.");
        Require(original.Frame == 2334 && original.Scene.UnlockedTutorials.SetEquals([0]) && original.IsUserControlEnabled && !original.Actors[11].Visible && !original.IsDialogueOpen && original.LastFault is null, "MENUTIPS0 failed to unlock its persisted bit before original UCON/HIDE/RET continuation.");
        var completed = original.Capture();
        original.Restore(completed);
        Require(completed.GetRawText() == original.Capture().GetRawText(), "Actual tutorial0 checkpoint failed an exact typed round-trip.");
        EventEngine retry = Create("player-performance-menutips-fault-checkpoint.json");
        var preserved = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 91);
        Require(retry.LastFault is null && preserved.GetProperty("Position").GetInt32() == 1720 && preserved.GetProperty("Stack").GetArrayLength() == 1 && preserved.GetProperty("Stack")[0].GetInt32() == 0, "MENUTIPS source upgrade changed the exact saved fault PC or operand.");
        retry.Step();
        Require(retry.Scene.UnlockedTutorials.SetEquals([0]) && retry.IsUserControlEnabled && !retry.Actors[11].Visible && retry.LastFault is null, "Saved MENUTIPS fault retry lost tutorial0 or the original caller's control/visibility continuation.");
        Console.WriteLine("MENUTIPS: exact2333 -> 2334 replay, tutorial0 bit unlock without UI/wait -> original UCON/HIDE/RET; old missing state and unchanged fault operands verified.");
    }

    private static void ClassroomPartyRequestBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        EventEngine Create(string leaf)
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures", leaf));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(232, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        // Historical snapshots retain the pre-SETPC defect. They diagnose this request boundary only, not corrected normal traversal.
        EventEngine original = Create("player-performance-preqew-preboundary-checkpoint.json");
        EventEngine replayed = Create("player-performance-preqew-preboundary-checkpoint.json");
        Require(original.Frame == 1427 && original.Scene.Party.SequenceEqual([0]), "PREQEW fixture did not start at the actual preceding frame and party.");
        original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(original.Capture().GetRawText() == replayed.Capture().GetRawText(), "PREQEW exact-before action replay diverged.");
        var waiting = original.Capture();
        var caller = waiting.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 18 && fiber.GetProperty("ScriptId").GetInt32() == 171);
        var requested = waiting.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 11);
        Require(original.Frame == 1428 && caller.GetProperty("Position").GetInt32() == 3528 && caller.GetProperty("WaitReason").GetString() == nameof(WaitReason.RequestEnd) && caller.GetProperty("Stack").GetArrayLength() == 0, "PREQEW did not consume both operands and wait at the original next PC.");
        Require(requested.GetProperty("Priority").GetInt32() == 1 && caller.GetProperty("RequestSequence").GetInt64() == requested.GetProperty("Sequence").GetInt64(), "PREQEW did not resolve party0 to Squall's local script7/label11 at priority1.");
        original.Restore(waiting);
        Require(waiting.GetRawText() == original.Capture().GetRawText(), "PREQEW pending request relationship failed to round-trip.");
        original.Step();
        replayed.Step();
        Require(original.Actors[1].X == -189 && original.Actors[1].Y == -2121 && original.Actors[1].WalkmeshTriangle == 58 && original.Capture().GetRawText() == replayed.Capture().GetRawText(), "The requested original Squall script did not execute its SET3/INITTRACE or changed across request restore.");
        EventEngine retry = Create("player-performance-preqew-exact-fault-checkpoint.json");
        var preserved = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 18 && fiber.GetProperty("ScriptId").GetInt32() == 171);
        Require(retry.LastFault is null && preserved.GetProperty("Position").GetInt32() == 3527 && preserved.GetProperty("Stack")[0].GetInt32() == 7 && preserved.GetProperty("Stack")[1].GetInt32() == 1, "PREQEW upgrade changed the original fault PC or operands.");
        retry.Step();
        Require(retry.LastFault is null && retry.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 11), "The saved PREQEW fault failed to queue its original party request.");
        Console.WriteLine("PREQEW: historical exact frame1427 -> 1428 replay; party0 -> Squall1 script7/label11 priority1, typed end wait and fault retry passed.");
    }

    private static void PartyTurnAnglesAndResume()
    {
        EventEngine Create(int start, int x, int y, int duration)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, start), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.PCTURN, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, SyntheticObjects([0, 16 << 7], [caller, player]), []);
            engine.Step();
            engine.Step();
            return engine;
        }
        EventEngine wrapped = Create(250, 10, -100, 260);
        Require(wrapped.Actors[0].RotationTarget == 260 && wrapped.Actors[0].RotationTotalFrames == 4 && wrapped.Actors[0].RotationFrames == 5, "PCTURN did not capture the nearest wrapped angle or duration's low byte.");
        wrapped.Step();
        Require(wrapped.Actors[0].Direction == 250 && wrapped.Actors[0].RotationFrames == 4, "PCTURN skipped the native elapsed0 sample.");
        wrapped.Step();
        Require(wrapped.Actors[0].Direction == 251, "PCTURN first eased step did not use native integer cosine interpolation.");
        var midturn = wrapped.Capture();
        EventEngine resumed = Create(250, 10, -100, 260);
        resumed.Restore(midturn);
        Require(midturn.GetRawText() == resumed.Capture().GetRawText(), "PCTURN midturn state failed to round-trip.");
        for (int index = 0; index < 3; index++)
        {
            wrapped.Step();
            resumed.Step();
            Require(wrapped.Capture().GetRawText() == resumed.Capture().GetRawText(), "Restoring PCTURN changed an angular sample or completion tick.");
        }
        Require(wrapped.Actors[0].Direction == 4 && wrapped.Actors[0].RotationTarget is null && !wrapped.Actors[0].RotationUsesNativeCosine && wrapped.Actors[0].RotationFrames == 0 && wrapped.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 7, "PCTURN did not include its endpoint and release its source wait after duration+1 ticks.");
        EventEngine tie = Create(0, 0, 100, 4);
        tie.Step();
        tie.Step();
        tie.Step();
        Require(tie.Actors[0].Direction == 64 && tie.Actors[0].RotationTarget == 128, "PCTURN changed the native positive half-turn tie into a negative turn.");
        EventEngine immediate = Create(0, 100, 0, 256);
        Require(immediate.Actors[0].Direction == 0 && immediate.Actors[0].RotationTarget == 64, "Zero-duration PCTURN incorrectly skipped its next native driver tick.");
        immediate.Step();
        Require(immediate.Actors[0].Direction == 64 && immediate.Actors[0].RotationTarget is null, "Zero-duration PCTURN did not complete on its first driver sample.");
        EventEngine aligned = Create(64, 100, 0, 15);
        Require(aligned.Actors[0].RotationTarget is null && !aligned.Actors[0].RotationUsesNativeCosine && aligned.Actors[0].RotationFrames == 0 && aligned.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 7, "Already aligned PCTURN introduced a rotation wait.");
    }

    private static void ClassroomPartyTurnBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(232);
        EventEngine Create(string leaf)
        {
            GameState state = GameSession.Load(Path.Combine("../fixtures", leaf));
            GameSession session = new(state);
            EventEngine engine = new(session, state.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted };
            engine.LoadField(232, field.Jsm, field.Messages);
            engine.Restore(state.EventState!.Value);
            return engine;
        }
        const string beforeLeaf = "player-performance-party-corrected-pcturn-preboundary-checkpoint.json";
        EventEngine original = Create(beforeLeaf);
        EventEngine replayed = Create(beforeLeaf);
        Require(original.Frame == 1724 && original.Actors[11].Direction == 120 && original.Scene.Party.SequenceEqual([0]), "PCTURN fixture lost its original party, starting angle or immediately preceding frame.");
        original.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
        replayed.Replay([new PlayerAction(PlayerActionKind.Wait, Frames: 1)]);
        Require(original.Capture().GetRawText() == replayed.Capture().GetRawText() && original.Actors[11].RotationTarget == 143 && original.Actors[11].RotationFrames == 16, "Actual PCTURN replay changed its fixed native target143 or duration15/16 samples.");
        for (int index = 0; index < 6; index++)
        {
            original.Step();
        }
        var midturn = original.Capture();
        EventEngine resumed = Create(beforeLeaf);
        resumed.Restore(midturn);
        Require(midturn.GetRawText() == resumed.Capture().GetRawText(), "Actual PCTURN midwait did not retain its sampled target and cosine progress.");
        while (!original.IsDialogueOpen && original.Frame < 1800)
        {
            original.Step();
            resumed.Step();
            Require(original.Capture().GetRawText() == resumed.Capture().GetRawText(), "Actual PCTURN resume changed angular samples or the next original message.");
        }
        Require(original.Frame == 1741 && original.Actors[11].Direction == 143 && original.CurrentDialogue?.MessageId == 11 && original.CurrentDialogue?.Operation == 1624, "Actual PCTURN did not complete its16 driver samples before original Quistis message11.");
        EventEngine retry = Create("player-performance-party-corrected-pcturn-exact-fault-checkpoint.json");
        var preserved = retry.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 91);
        Require(retry.LastFault is null && preserved.GetProperty("Position").GetInt32() == 1618 && preserved.GetProperty("Stack")[0].GetInt32() == 15 && preserved.GetProperty("Stack")[1].GetInt32() == 0, "PCTURN source upgrade changed the saved PC or original party/duration operands.");
        retry.Step();
        Require(retry.Actors[11].RotationTarget == 143 && retry.Actors[11].RotationFrames == 16, "PCTURN fault retry failed to consume the original operands and start the native turn.");
        Console.WriteLine("PCTURN: exact1724 -> 1725 replay; Quistis120 -> fixed143, duration15/16 cosine ticks -> message11/frame1741; midwait and fault retry passed.");
    }

    private static void BackgroundModuleInitialSelection(string gameDirectory)
    {
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] selection = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BGDRAW, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.BGOFF, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([16 << 7, 0], [idle, selection]);
        program[2] = 2;
        program[3] = 0;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1, "Background modules did not start disabled in native raw-group order.");
        engine.Step();
        engine.Step();
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1, "An unselected module appeared during its preceding wait.");
        engine.Step();
        Require(engine.BackgroundStates[0] == 0 && engine.BackgroundStates[1] == -1, "BGDRAW0 did not select only its actual source module.");
        var selected = engine.Capture();
        EventEngine resumed = new(session, session.State.Variables);
        resumed.LoadField(100, program, []);
        resumed.Restore(selected);
        engine.Step();
        resumed.Step();
        Require(engine.BackgroundStates[0] == 0 && engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "Selected background frame changed across a saved wait.");
        engine.Step();
        resumed.Step();
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1 && engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "BGOFF did not disable the previously selected module after restore.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets classroom = assets.LoadField(232);
        EventObject deskModule = JsmProgram.Read(classroom.Jsm).Objects.Single(obj => obj.BackgroundModuleIndex == 6);
        Require(deskModule.Index == 35 && deskModule.Label == 328 && deskModule.Scripts[0].Start == 7480 && deskModule.Scripts[0].End == 7482, "Actual desk-sprite module no longer matches its original no-selection init script.");
        BackgroundTile[] deskTiles = classroom.Background.Where(tile => tile.AnimationId == 6).ToArray();
        Require(deskTiles.Length == 11 && deskTiles.All(tile => tile.AnimationState == 0) && deskTiles.Select(tile => tile.Z).Distinct().Order().SequenceEqual([391, 559]), "Actual two desk sprites lost their module6/frame0 MAP selection or depth groups.");
        GameState historical = GameSession.Load(Path.Combine("../fixtures/external-fixture.json"));
        GameSession historicalSession = new(historical);
        EventEngine restoredHistorical = new(historicalSession, historical.Variables);
        restoredHistorical.LoadField(232, classroom.Jsm, classroom.Messages);
        Require(restoredHistorical.BackgroundStates[6] == -1, "Actual classroom LoadField failed to initialize never-selected module6 as disabled.");
        restoredHistorical.Restore(historical.EventState!.Value);
        Require(!restoredHistorical.BackgroundStates.ContainsKey(6) && restoredHistorical.Frame == 1724, "Restore migrated the historical missing-module state or changed its exact preceding frame.");
        Console.WriteLine("Background selection: initialized off -> BGDRAW0 -> saved wait -> BGOFF; actual classroom module6 has11 frame0 tiles in two desk-depth groups; historical missing state retained.");
    }

    private static void BackgroundLoopResume()
    {
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] shade = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 25), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.RBGSHADELOOP, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([16 << 7, 0], [idle, shade]);
        program[2] = 1;
        program[3] = 1;
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.LoadField(100, program, []);
        engine.Step();
        Require(engine.GetBackgroundModuleShades()[0] == new System.Numerics.Vector3(126), "Shade module did not begin at its actual RGB endpoint.");
        for (int index = 0; index < 12; index++)
        {
            engine.Step();
        }
        var snapshot = engine.Capture();
        EventEngine restored = new(session, session.State.Variables);
        restored.LoadField(100, program, []);
        restored.Restore(snapshot);
        for (int index = 12; index < 49; index++)
        {
            engine.Step();
            restored.Step();
            Require(engine.GetBackgroundModuleShades()[0] == restored.GetBackgroundModuleShades()[0], "Restoring a running module shade loop changed an observed frame color.");
            if (index == 24)
            {
                Require(engine.GetBackgroundModuleShades()[0] == System.Numerics.Vector3.Zero, "Shade loop did not finish its first25-frame fade at black.");
            }
        }
        Require(engine.GetBackgroundModuleShades()[0] == new System.Numerics.Vector3(126) && engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Loop second endpoint/holds or exact phase snapshot diverged.");
    }

    private static void ArrivalInitializationOrder()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -99), (Jsm.Opcode.PSHN_L, -99), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([1], [init, main]);
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables) { ResolveFieldArrival = arrival => new WalkmeshPosition(new System.Numerics.Vector3(arrival.X, arrival.Y, -7), arrival.Triangle) };
        engine.LoadField(100, program, ["Arrival"], new FieldTransition(100, 10, 20, 50, 32));
        var pending = engine.Capture();
        engine.Restore(pending);
        Require(pending.GetRawText() == engine.Capture().GetRawText(), "Pending coordinate arrival was lost on restore.");
        engine.Step();
        Require(engine.Actors[0].X == 10 && engine.Actors[0].Y == 20 && engine.Actors[0].Z == -7 && engine.Actors[0].WalkmeshTriangle == 50 && engine.Actors[0].Direction == 32, "Arrival failed to override initialization SET3 before default scripts.");
        engine.Step();
        Require(engine.IsDialogueOpen, "Default script failed to start after coordinate arrival.");
        engine.ConfirmDialogue();
        engine.Step();
        Require(engine.Actors[0].X == 4 && engine.Actors[0].Y == 5 && engine.Actors[0].Z == 6, "Arrival incorrectly overrode later original SET3.");
    }

    private static void MovieAxisAndFaultResume()
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field });
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.AXIS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.PSHM_L, 80), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        EventEngine engine = new(session, session.State.Variables) { MoviePlayback = () => session.State.MoviePlayback };
        engine.LoadField(100, content, []);
        engine.Step();
        session.SetMoviePlayback(new MoviePlaybackState(145, 325, true, false, false));
        engine.Step();
        Require(session.State.Variables[80] == 145 && !session.State.Variables.ContainsKey(512) && engine.InputAxisDirection == 16, "Movie frame sampling, per-fiber movie wait or AXIS tick ordering failed.");
        var snapshot = engine.Capture();
        EventEngine resumed = new(session, session.State.Variables) { MoviePlayback = engine.MoviePlayback };
        resumed.LoadField(100, content, []);
        resumed.Restore(snapshot);
        Require(snapshot.GetRawText() == resumed.Capture().GetRawText(), "Mid-movie AXIS snapshot lost timing or decoder frame state.");
        session.CompleteMovie(skipped: true);
        resumed.Step();
        resumed.Step();
        resumed.Step();
        Require(session.State.Variables[512] == 325 && resumed.InputAxisDirection == 64, "Skipped real terminal frame was truncated or AXIS did not finish at its byte target.");

        byte[] unsupported = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 456), (Jsm.Opcode.BATTLE, 0), (Jsm.Opcode.RET, 0));
        resumed.LoadField(101, unsupported, []);
        try
        {
            resumed.Step();
            throw new InvalidOperationException("Unsupported command executed.");
        }
        catch (EventScriptException)
        {
        }
        var fault = resumed.Capture();
        var fiber = fault.GetProperty("Fibers")[0];
        Require(fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack")[0].GetInt32() == 456 && fiber.GetProperty("Stack")[1].GetInt32() == 123 && resumed.NextTrigger.Kind == PlayerTriggerKind.Faulted, "Unsupported command advanced the PC, consumed operands or hid its fault.");
        resumed.Restore(fault);
        try
        {
            resumed.Step();
            throw new InvalidOperationException("A faulted saved VM continued past its unsupported opcode.");
        }
        catch (EventScriptException)
        {
        }
        Require(fault.GetRawText() == resumed.Capture().GetRawText(), "A repeated unsupported command changed its saved retryable state.");

        session.Restore(new GameState { Scene = SceneKind.Field, MoviePlayback = new MoviePlaybackState(325, 325, false, true, true) });
        byte[] freshMovie = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.PSHM_L, 80), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.RET, 0));
        resumed = new EventEngine(session, session.State.Variables) { MoviePlayback = () => session.State.MoviePlayback };
        resumed.LoadField(102, freshMovie, []);
        resumed.Step();
        Require(session.State.Variables[512] == 0, "A previous completed movie frame leaked into the new clip before its first decoder sample.");
    }

    private static void LineContactAndResume()
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)[]> scripts = [player, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 0)]];
        for (int label = 17; label < 23; label++)
        {
            scripts.Add([(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 0)]);
        }
        scripts.Add([(Jsm.Opcode.LBL, 23), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.RET, 0)]);
        byte[] program = SyntheticObjects([0, (16 << 7) | 7], scripts);
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables);
        engine.ResolveWalkmesh = (x, y) => new WalkmeshPosition(new System.Numerics.Vector3(x, y, 0), 0);
        engine.LoadField(100, program, ["Crossed"]);
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 10));
        engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 2));
        Require(engine.Actors[0].Direction == 64, "Native positive-X movement facing must be64.");
        Require(engine.LineCrossings.Count == 1 && engine.CurrentDialogue?.Text == "Crossed" && engine.Actors[1].LineContact, "Forward entry did not dispatch the original crossing script.");
        System.Text.Json.JsonElement snapshot = engine.Capture();
        EventEngine restored = new(session, session.State.Variables);
        restored.ResolveWalkmesh = engine.ResolveWalkmesh;
        restored.LoadField(100, program, ["Crossed"]);
        restored.Restore(snapshot);
        Require(snapshot.GetRawText() == restored.Capture().GetRawText(), "Saving at a line contact lost the pending original script/contact state.");
        restored.Apply(new PlayerAction(PlayerActionKind.Confirm));
        restored.Apply(new PlayerAction(PlayerActionKind.Move));
        restored.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Require(restored.LineCrossings.Count == 0, "Stopping on or leaving a contacted line retriggered its script.");
        restored.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        Require(restored.LineCrossings.Count == 1 && restored.IsDialogueOpen, "Reverse entry after leaving did not rearm the enabled line.");
    }

    private static byte[] SyntheticObjects(ushort[] groups, List<(Jsm.Opcode Opcode, int Parameter)[]> scripts)
    {
        int scriptsOffset = 8 + groups.Length * 2;
        int operationsOffset = scriptsOffset + (scripts.Count + 1) * 2;
        byte[] bytes = new byte[operationsOffset + scripts.Sum(script => script.Length) * 4];
        bytes[3] = checked((byte)groups.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), checked((ushort)scriptsOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)operationsOffset));
        for (int index = 0; index < groups.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8 + index * 2), groups[index]);
        }
        int operationIndex = 0;
        for (int index = 0; index < scripts.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + index * 2), checked((ushort)operationIndex));
            foreach ((Jsm.Opcode opcode, int parameter) in scripts[index])
            {
                int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + operationIndex++ * 4), encoded);
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(scriptsOffset + scripts.Count * 2), checked((ushort)operationIndex));
        return bytes;
    }

    private sealed record ActionCorpus(string Checkpoint, PlayerActionObservation[] Actions, BranchDecision[] Decisions, DialogueOutcome[] Dialogues, BranchCoverage[] Coverage, LineCrossing[] Lines);

    private static byte[] SyntheticProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHAC or Jsm.Opcode.PSHI_L or Jsm.Opcode.POPI_L or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
