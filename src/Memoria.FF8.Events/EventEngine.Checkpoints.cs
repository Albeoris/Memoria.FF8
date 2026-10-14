using System.Numerics;
using Memoria.FF8.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public enum WaitReason
{
    None,
    Frames,
    Animation,
    Movement,
    MoveActor,
    Rotation,
    Dialogue,
    Movie,
    Color,
    Fade,
    BackgroundAnimation,
    RequestStart,
    RequestEnd,
    SoundResource,
    FaceDirection,
    PartyJoin,
    PartySplit,
    BackgroundScroll,
    NativeScriptMovement,
    WorldJump,
    MinimalMenu,
    MinimalBattle,
    NativeJump,
    NativeHalt
}

public sealed partial class EventEngine
{
    private static readonly JsonSerializerOptions CheckpointOptions = new() { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
    private string programHash = "";

    /// <summary>Captures the complete cooperative VM, including typed waits and its deterministic random stream.</summary>
    public JsonElement Capture()
    {
        ValidateReplayMemory();
        if (program is null)
        {
            throw new InvalidOperationException("No field has been loaded.");
        }
        Checkpoint snapshot = new(
            1, FieldId, programHash, Frame, accumulatedSeconds, randomState, preparedMovie, sequence, defaultsStarted, transitionRequested, userControlEnabled,
            colorStartFrame, colorEndFrame, colorStart, colorEnd, subtractColor, fadeStartFrame, fadeEndFrame, fadeStart, fadeEnd, backgroundFps, nameEntity,
            actors.ToArray(), fibers.Select(fiber => new FiberCheckpoint(fiber.ObjectId, fiber.Script.Id, fiber.Priority, fiber.Sequence, fiber.Initialization, fiber.Position, fiber.Complete, fiber.Started, fiber.Stack.Capture(), fiber.StackExpressions.ToArray(), new Dictionary<int, NativeScalarValue>(fiber.Results), fiber.WaitReason, fiber.WaitUntil, fiber.Request?.Sequence, fiber.DialogueChannel, fiber.BackgroundId, fiber.SoundResourceId, fiber.NativeDoorSlot, fiber.NativeActorSlot, fiber.NativeMoveOperation, fiber.NativeMoveTarget)).ToArray(),
            dialogues.Values.ToArray(), CurrentDialogue?.Channel, new Dictionary<int, int>(backgroundStates), backgroundAnimations.Select(pair => new BackgroundCheckpoint(pair.Key, pair.Value.First, pair.Value.Last, pair.Value.Speed, pair.Value.Complete, pair.Value.NativeVersion, pair.Value.Counter, pair.Value.Position, pair.Value.Active, pair.Value.Loop)).ToArray(), new Dictionary<int, int>(variables), Scene, axisStart, axisTarget, axisDuration, axisElapsed, InputAxisDirection, movieProgressExpected, CurrentMoviePlayback, pendingArrival, LastFault, partyJoin, partySplit, services.WorldControls, services.ExamTimer, services.NativeVariableValues is { Count: > 0 } nativeMemory ? new Dictionary<int, NativeMemoryValue>(nativeMemory) : null, services.CharacterVitals, services.CharacterAppearance, services.CardInventory, services.SeeD, services.MessageVariables, services.Economy);
        return JsonSerializer.SerializeToElement(snapshot, CheckpointOptions);
    }

    /// <summary>Restores a captured VM after LoadField has supplied exactly the same field's bytes and messages.</summary>
    private void RestoreCore(JsonElement snapshot)
    {
        Checkpoint saved = snapshot.Deserialize<Checkpoint>(CheckpointOptions) ?? throw new InvalidDataException("Empty event checkpoint.");
        if (program is null || saved.Version != 1 || saved.FieldId != FieldId || saved.ProgramHash != programHash || saved.Actors.Length != actors.Count || saved.Frame < 0 || !double.IsFinite(saved.AccumulatedSeconds) || saved.AccumulatedSeconds < 0 || saved.AccumulatedSeconds > 10)
        {
            throw new InvalidDataException("Event checkpoint does not match the loaded field/program or has invalid timing.");
        }
        if (!snapshot.TryGetProperty("WorldControls", out _) || saved.WorldControls is null && services.WorldControls is not null)
        {
            throw new InvalidDataException("Saved VM must preserve its canonical world-control state; missing or unknown snapshot state cannot retain a different known service state.");
        }
        saved.WorldControls?.Validate();
        if (saved.MessageVariables is null && services.MessageVariables is not null || saved.MessageVariables is not null && !services.CanStoreMessageVariables)
        {
            throw new InvalidDataException("Saved VM cannot replace known process message variables with unknown history or an unsupported service.");
        }
        if (saved.Scene.NativeSalaryMovementUnits is not null and not (0 or 3 or 5) || NativeRuntimeProfile is { IsVerified: true } && saved.Scene.NativeSalaryMovementUnits is null)
        {
            throw new InvalidDataException("Saved native field must preserve its actual previous salary movement scalar0/3/5; recreate its authentic inputs.");
        }
        if (!HasCanonicalPartySlots(saved.Scene))
        {
            throw new InvalidDataException("Saved VM requires exactly three native party character bytes; compressed or malformed party slots require authentic reconstruction.");
        }
        if (saved.Scene.MovieActors is NativeMovieActorLifecycle movieActors)
        {
            movieActors.Validate(saved.Frame);
            if (movieActors.MovieIndex != saved.PreparedMovie || movieActors.Active != services.IsMoviePlaying)
            {
                throw new InvalidDataException("Saved movie actor lifecycle disagrees with its prepared index or current service.");
            }
            IEnumerable<int> movieObjects = saved.Scene.NativeActorLayoutVersion == 1 ? saved.Scene.NativeActorObjectIds : program.NativeObjects.Take(program.NativeActorCount);
            if (movieObjects.Any(id => (uint)id >= saved.Actors.Length || saved.Actors[id].NativeMovementBaseSpeed is null))
            {
                throw new InvalidDataException("Saved native movie actor writes have missing WORD512 allocation authority.");
            }
        }
        else if (services.IsMoviePlaying || saved.MovieProgressExpected)
        {
            throw new InvalidDataException("Active or pending movie snapshot has no executed native actor lifecycle; recreate its journal.");
        }
        if (saved.NativeVariableValues is { Count: > 0 } savedMemory)
        {
            if (!services.CanStoreNativeVariableValues)
            {
                throw new InvalidDataException("Saved uncertain native memory requires its typed event service.");
            }
            NativeMemoryValue.Validate(savedMemory, saved.Variables);
            foreach (NativeMemoryValue entry in savedMemory.Values)
            {
                ValidateNativeScalarSource(entry.Value, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer);
            }
        }
        if (saved.Scene.UnlockedTutorials.Any(id => (uint)id >= 128))
        {
            throw new InvalidDataException("Saved tutorial unlock ID is outside the native bitmap.");
        }
        JsonElement savedActorJson = snapshot.GetProperty("Actors");
        for (int index = 0; index < saved.Actors.Length; index++)
        {
            if (!savedActorJson[index].TryGetProperty("FaceStateVersion", out _) || !savedActorJson[index].TryGetProperty("NativeBaseAnimationVersion", out _) || saved.Actors[index].FaceStateVersion != 1 || saved.Actors[index].NativeBaseAnimationVersion != 1)
            {
                throw new InvalidDataException("Saved actor lacks current native face or base animation provenance; recreate the authentic journal.");
            }
            if (saved.Frame == 0 && !saved.DefaultsStarted && saved.PendingArrival is not null && saved.Fibers.All(fiber => fiber.Initialization && !fiber.Started) && saved.Actors[index].WalkmeshTriangle == -1)
            {
                throw new InvalidDataException("Saved allocation contains the obsolete invented triangle sentinel; recreate the authentic journal.");
            }
        }
        if (snapshot.GetProperty("Dialogues").EnumerateArray().Any(dialogue => !dialogue.TryGetProperty("CanConfirm", out _)))
        {
            throw new InvalidDataException("Saved dialogue lacks native confirmability; recreate the authentic journal.");
        }
        ValidatePreviousFieldCheckpoint(saved);
        ValidatePreviousWorldEntityCheckpoint(saved);
        saved.ExamTimer?.Validate();
        if (services.ExamTimer is not null && saved.ExamTimer is null)
        {
            throw new InvalidDataException("Saved VM lacks the current process clock; recreate the authentic journal.");
        }
        if (saved.Scene.AudioLoadEpoch <= 0)
        {
            throw new InvalidDataException("Saved process audio history requires its executed field-load identity; recreate the authentic journal.");
        }
        saved.Scene.FieldCallbackClock?.Validate(saved.Frame);
        saved.Scene.Shake?.Validate();
        if (saved.Scene.Shake is NativeFieldShake restoredShake && (restoredShake.X.Enabled || restoredShake.X.Oscillating || restoredShake.Y.Enabled || restoredShake.Y.Oscillating) && ShakeTables is null)
        {
            throw new InvalidDataException("Saved native shake process history requires authenticated original lookup tables.");
        }
        saved.CharacterVitals?.Validate();
        saved.CharacterAppearance?.Validate();
        saved.CardInventory?.Validate();
        saved.Economy?.Validate();
        if (saved.Economy is not null && !services.CanStoreEconomy || saved.Economy is null && services.Economy is not null)
        {
            throw new InvalidDataException("Saved economy requires matching known history and a persistent service; recreate authentic inputs.");
        }
        if (saved.Economy is not null && (saved.Variables.Keys.Any(address => address < 80 && (long)address + 4 > 72) || saved.NativeVariableValues?.Any(pair => pair.Key < 80 && (long)pair.Key + pair.Value.Width > 72) == true || saved.CharacterVitals is not null && saved.Economy.InitOutSha256 != saved.CharacterVitals.InitOutSha256))
        {
            throw new InvalidDataException("Saved economy overlaps another memory owner or has a different New Game template identity.");
        }
        saved.SeeD?.Validate();
        if (saved.SeeD is not null && !services.CanStoreSeeD || saved.SeeD is null && services.SeeD is not null || saved.SeeD is not null && saved.Variables.Keys.Any(address => address is >= 13 and <= 17))
        {
            throw new InvalidDataException("Saved SeeD rank and salary require matching native state and canonical WORD16 ownership; recreate the authentic journal.");
        }
        if (saved.CardInventory is not null && !services.CanStoreCardInventory || saved.CardInventory is null && services.CardInventory is not null)
        {
            throw new InvalidDataException("Saved card inventory requires matching native history and a persistent service; recreate the authentic journal.");
        }
        if (saved.CharacterAppearance is not null && saved.CharacterVitals is not null && saved.CharacterAppearance.InitOutSha256 != saved.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("Saved character appearance and vitals have different native New Game identities.");
        }
        if (saved.Frame > 0 && saved.Scene.ButtonInput is null)
        {
            throw new InvalidDataException("Saved field callbacks lack their actual mapped button history; recreate the authentic journal.");
        }
        saved.Scene.ButtonInput?.Validate(saved.Frame);
        if (saved.Dialogues.Select(dialogue => dialogue.Channel).Distinct().Count() != saved.Dialogues.Length || saved.Dialogues.Any(dialogue => (uint)dialogue.Channel >= 8) || saved.CurrentDialogueChannel is int focusedChannel && !saved.Dialogues.Any(dialogue => dialogue.Channel == focusedChannel))
        {
            throw new InvalidDataException("Saved dialogue channels must be distinct original window IDs and contain the focused channel.");
        }
        ValidateDrawPointCheckpoint(saved);
        ValidateSoundInitializationCheckpoint(saved);
        ValidateSoundStopCheckpoint(saved);
        ValidateSoundVolumeTransitionCheckpoint(saved);
        ValidateSoundVolumeCheckpoint(saved);
        ValidateVibrationCheckpoint(saved);
        ValidateAllSoundVolumeTransitionCheckpoint(saved);
        ValidateAllSoundVolumeCheckpoint(saved);
        ValidateFollowOffCheckpoint(saved);
        ValidateMusicStopCheckpoint(saved);
        ValidateMusicLoadCheckpoint(saved);
        ValidateMusicVolumeCheckpoint(saved);
        ValidateFadeNoneCheckpoint(saved);
        ValidateNativeLineCheckpoint(saved);
        ValidateFieldSoundCheckpoint(saved);
        ValidateParticleCheckpoint(saved);
        foreach (ActorState actor in saved.Actors)
        {
            bool allocated = saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) == true && program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId);
            if (allocated && (actor.NativeMovementCompletion542 is null or > 2 || actor.NativeLandingTriangle508 is null || actor.NativeTurnAccumulator474 is null) || !allocated && (actor.NativeMovementCompletion542 is not null || actor.NativeLandingTriangle508 is not null || actor.NativeTurnAccumulator474 is not null))
            {
                throw new InvalidDataException("Saved physical WORD474/508/542 requires current explicit native allocation state.");
            }
        }
        ValidateDisplayOffsetCheckpoint(saved);
        ValidateActorBackgroundScrollCheckpoint(saved);
        ValidateNativeLinearTurns(saved);
        ValidateNativeNormalBackgroundScrollCheckpoint(saved);
        ValidateActorShadowCheckpoint(saved);
        ValidateFootstepPairCheckpoint(saved);
        ValidateFootstepOffAllCheckpoint(saved);
        if (saved.Scene.NativeFieldInitialization is not null && saved.Scene.BackgroundScrollLayers.Count != 8)
        {
            throw new InvalidDataException("Native field background settings require all eight executed current loader records; recreate this source from its authentic journal.");
        }
        if (saved.Scene.BackgroundScrollLayers.Keys.Any(layer => (uint)layer >= 8) || saved.Scene.BackgroundScrollLayers.Values.Any(settings => settings is null))
        {
            throw new InvalidDataException("Saved background scroll layer is outside the eight original records.");
        }
        if (saved.Scene.BackgroundScrollPositions.Any(pair => !ValidBackgroundScrollPosition(pair.Key, pair.Value, saved.Scene)))
        {
            throw new InvalidDataException("Saved background scroll has invalid native layer, mode, phase, actor or completion state.");
        }
        if (saved.Scene.BackgroundScrollPositions.Values.Any(position => position.Mode == 4) && CanRestoreLinearBackgroundScroll is not null && !CanRestoreLinearBackgroundScroll(saved.Scene))
        {
            throw new InvalidDataException("Saved native linear scroll has no compatible loader, camera or layer placement contract.");
        }
        if (saved.Scene.NativeFieldInitialization is NativeFieldInitializationProof initialization && (!initialization.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) || initialization.Stage == NativeFieldInitializationStage.Allocated && (saved.Frame != 0 || saved.DefaultsStarted || saved.Fibers.Any(fiber => fiber.Started || !fiber.Initialization)) || initialization.Stage == NativeFieldInitializationStage.Initializing && (saved.Frame <= 0 || saved.DefaultsStarted) || initialization.Stage == NativeFieldInitializationStage.Completed && (!saved.DefaultsStarted || saved.Scene.NativeActorLayoutVersion != 1)))
        {
            throw new InvalidDataException("Saved native field initialization has an unknown runtime source or inconsistent loader stage. Resume the documented control point or migrate this save explicitly.");
        }
        if (saved.Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation is long allocationClock && (allocationClock < 0 || saved.ExamTimer is null || saved.Scene.FieldCallbackClock is null || allocationClock > saved.ExamTimer.ClockCalls || saved.Scene.FieldCallbackClock.TimerCalls > long.MaxValue - allocationClock || !saved.TransitionRequested && saved.ExamTimer.ClockCalls != allocationClock + saved.Scene.FieldCallbackClock.TimerCalls))
        {
            throw new InvalidDataException("Saved field allocation clock differs from its preserved ordinary/movie field callback count.");
        }
        if (saved.Scene.NativeActorLayoutVersion is < 0 or > 1 || saved.Scene.NativeActorObjectIds.Distinct().Count() != saved.Scene.NativeActorObjectIds.Count || saved.Scene.NativeActorObjectIds.Any(id => !program.NativeObjects.Take(program.NativeActorCount).Contains(id)) || saved.Scene.NativeActorLayoutVersion == 0 && saved.Scene.NativeActorObjectIds.Count != 0)
        {
            throw new InvalidDataException("Saved native actor layout does not match the original actor pointer-table prefix.");
        }
        if (saved.Scene.NativeActorLayoutVersion == 1 && (!saved.DefaultsStarted || !saved.Scene.NativeActorObjectIds.SequenceEqual(program.NativeObjects.Take(program.NativeActorCount).Where(saved.Scene.NativeActorObjectIds.Contains))))
        {
            throw new InvalidDataException("Saved compact native actor layout has invalid initialization timing or native order.");
        }
        if (saved.Actors.Any(actor => actor.NativePushContactPending && (saved.Scene.NativeActorLayoutVersion != 1 || (uint)actor.ObjectId >= program.Objects.Count || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || program.Objects[actor.ObjectId].Scripts.Count < 4)))
        {
            throw new InvalidDataException("Saved native push contact lacks its retained original actor/local script3.");
        }
        if (saved.Actors.Any(actor => actor.MovementActorId is not null || actor.MovementTarget is not null && actor.NativeScriptMovement is not { Active: true }))
        {
            throw new InvalidDataException("Saved managed scripted movement is unsupported; recreate the authentic journal with native movement contracts.");
        }
        if (saved.Scene.FieldPartyNativeActorIds.Count != 0 && saved.Scene.NativeActorLayoutVersion != 1)
        {
            throw new InvalidDataException("Legacy refreshed party bindings predate native actor compaction. Replay player-performance-classroom-exit-preboundary-checkpoint.json before field139 loads; preserve this snapshot as diagnostic evidence.");
        }
        if (saved.Scene.FieldPartyNativeActorIds.Count is not (0 or 3) || saved.Scene.FieldPartyNativeActorIds.Any(id => id != 0xFF && (uint)id >= saved.Scene.NativeActorObjectIds.Count))
        {
            throw new InvalidDataException("Saved field-party binding exceeds the retained native actor array.");
        }
        if (saved.Scene.NativePartyTraceVersion is < 0 or > 1 || saved.Scene.PartyTrace is PartyTraceState trace && (trace.Cursor is < 0 or > 63 || trace.FirstOffset is < 0 or > 63 || trace.SecondOffset is < 0 or > 63 || trace.FirstTrail.Length != 64 || trace.SecondTrail.Length != 64) || saved.Scene.NativePartyTraceVersion == 1 && (saved.Scene.PartyTrace is null || saved.Scene.NativePlayerPosition is null || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.FieldPartyNativeActorIds.Count != 3))
        {
            throw new InvalidDataException("Saved native party trace has invalid rings, cursor or preserved fixed leader state.");
        }
        if (saved.Scene.NativePlayerMovementVersion is < 0 or > 1 || saved.Scene.NativePlayerMovementVersion == 1 && saved.Scene.NativePartyTraceVersion != 1)
        {
            throw new InvalidDataException("Saved native player movement requires its preserved fixed party trace and supported driver version.");
        }
        if (saved.Scene.NativePlayerEarlyRetryState is byte earlyRetry && (earlyRetry > 1 || saved.Scene.NativePartyTraceVersion != 1 || saved.Scene.NativeActorLayoutVersion != 1 || saved.Scene.NativeFieldLoadVersion != 1 && saved.Scene.NativePlayerMovementVersion != 1))
        {
            throw new InvalidDataException("Saved native player early-retry byte requires a proved normal loader or certified movement write and its preserved party state.");
        }
        if (saved.Scene.NativeFieldLoadVersion is < 0 or > 1 || saved.Scene.NativeFieldLoadVersion == 1 && (!saved.DefaultsStarted || saved.PendingArrival is not null || saved.Scene.NativePartyTraceVersion != 1))
        {
            throw new InvalidDataException("Saved native field-load policy has invalid completion or trace state.");
        }
        if (saved.Scene.NativeFieldEntryStack is NativeFieldEntryStackReceipt entryStack && (!ValidNativeFieldEntryStackReceipt(entryStack) || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } stackInitialization || !stackInitialization.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) || saved.Scene.NativeFieldLoadVersion != 1 || !saved.DefaultsStarted || saved.PendingArrival is not null))
        {
            throw new InvalidDataException("Saved native field-entry stack receipt lacks its current proved loader, original bytes or successful winner history.");
        }
        if (saved.Scene.GlobalSoundRequests.Any(request => request.ChannelMask == 0 || request.ChannelMask != (request.RawChannelMask & 0xFFFFFF) || request.Voice != System.Numerics.BitOperations.TrailingZeroCount(request.ChannelMask) + 1 || request.PitchPercent != 100 || request.EffectivePan != request.RawPan >> 1 || !ValidGlobalSoundResource(request.Resource, MapGlobalSoundSample(request.OriginalEffectId))) || saved.Scene.GlobalSoundVoices.Any(pair => pair.Key != pair.Value.Voice || !saved.Scene.GlobalSoundRequests.Contains(pair.Value)))
        {
            throw new InvalidDataException("Saved global sound request does not match its original mapping, voice, pan or resource policy.");
        }
        if (saved.BackgroundAnimations.Any(animation => animation.NativeVersion != 1))
        {
            throw new InvalidDataException("Background animation requires the current native state contract; recreate this checkpoint from its authentic journal.");
        }
        if (saved.BackgroundAnimations.Any(animation => animation.NativeVersion == 1 && (animation.Speed is < short.MinValue or > short.MaxValue || animation.Counter is < short.MinValue or > short.MaxValue || animation.Position is < short.MinValue or > short.MaxValue)))
        {
            throw new InvalidDataException("Native background animation phase exceeds its signed16-bit words.");
        }
        if (saved.BackgroundAnimations.Any(animation => animation.Loop && (animation.NativeVersion != 1 || !animation.Active || animation.Complete)))
        {
            throw new InvalidDataException("Saved native background loop must retain an active noncompleted animation.");
        }
        List<Fiber> restored = [];
        HashSet<long> seenSequences = [];
        foreach (FiberCheckpoint savedFiber in saved.Fibers)
        {
            if ((uint)savedFiber.ObjectId >= program.Objects.Count || !seenSequences.Add(savedFiber.Sequence) || !Enum.IsDefined(savedFiber.WaitReason) || savedFiber.Stack.Length > 4096 || savedFiber.Stack.Length != savedFiber.StackExpressions.Length)
            {
                throw new InvalidDataException("Invalid saved script fiber.");
            }
            EventScript script = program.Objects[savedFiber.ObjectId].Scripts.SingleOrDefault(item => item.Id == savedFiber.ScriptId) ?? throw new InvalidDataException("Saved fiber references an unknown script.");
            if (savedFiber.Position < script.Start || savedFiber.Position > script.End || !savedFiber.Complete && savedFiber.Position == script.End && savedFiber.WaitReason == WaitReason.None)
            {
                throw new InvalidDataException("Saved fiber instruction pointer is outside its script.");
            }
            if (savedFiber.WaitReason == WaitReason.BackgroundScroll)
            {
                int? layer = (uint)savedFiber.Position >= program.Operations.Count ? null : program.Operations[savedFiber.Position].Opcode.ToString() switch
                {
                    "SCROLLSYNC" => 0,
                    "SCROLLSYNC2" when savedFiber.Stack.Length > 0 && savedFiber.Stack[0].IsExact && (uint)savedFiber.Stack[0] < 8 => (int)savedFiber.Stack[0],
                    _ => null
                };
                if (savedFiber.Complete || layer is not int selected || !saved.Scene.BackgroundScrollPositions.ContainsKey(selected) && !(selected == 0 && saved.Scene.ActorBackgroundScroll is not null))
                {
                    throw new InvalidDataException("Saved scroll synchronization wait does not preserve its original instruction, unconsumed layer operand and known scroll state.");
                }
            }
            Fiber fiber = new(savedFiber.ObjectId, script, savedFiber.Priority, savedFiber.Sequence, savedFiber.Initialization)
            {
                Position = savedFiber.Position,
                Complete = savedFiber.Complete,
                Started = savedFiber.Started,
                WaitReason = savedFiber.WaitReason,
                WaitUntil = savedFiber.WaitUntil,
                DialogueChannel = savedFiber.DialogueChannel,
                BackgroundId = savedFiber.BackgroundId,
                SoundResourceId = savedFiber.SoundResourceId,
                NativeDoorSlot = savedFiber.NativeDoorSlot,
                NativeActorSlot = savedFiber.NativeActorSlot,
                NativeMoveOperation = savedFiber.NativeMoveOperation,
                NativeMoveTarget = savedFiber.NativeMoveTarget
            };
            foreach (NativeScalarValue operand in savedFiber.Stack.Reverse())
            {
                ValidateNativeScalarSource(operand, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer);
                fiber.Stack.Push(operand);
            }
            foreach (string expression in savedFiber.StackExpressions.Reverse())
            {
                fiber.StackExpressions.Push(expression);
            }
            foreach ((int key, NativeScalarValue value) in savedFiber.Results)
            {
                ValidateNativeScalarSource(value, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer);
                fiber.Results.Add(key, value);
            }
            restored.Add(fiber);
        }
        Dictionary<long, Fiber> bySequence = restored.ToDictionary(fiber => fiber.Sequence);
        ValidateNativeDoorSuspensionCheckpoint(saved, restored);
        ValidateNativeJumps(saved, restored);
        ValidateNativeActorContactCheckpoint(saved);
        ValidateNativeMovementRequests(saved, restored);
        ValidateNativeJoinedPositions(saved);
        ValidateWorldTransitionCheckpoint(saved, bySequence);
        ValidateMinimalMenuCheckpoint(saved, bySequence);
        ValidateNativeBattleCheckpoint(saved, bySequence);
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeMotionAuthority is NativeMotionAuthority authority && (authority.Version != 1 || actor.NativeScriptMovement is { } activeMotion && (activeMotion.Active || activeMotion.Cancelled) && (authority.FacingHold != (NativeMoveFacingHold(activeMotion)) || authority.MovementDirection != activeMotion.MovementDirection)))
            {
                throw new InvalidDataException("Saved native motion authority has an unsupported schema or differs from its active scripted movement scalars.");
            }
            if (actor.NativeScriptMovement is not NativeScriptMovementState motion)
            {
                continue;
            }
            bool ownerExists = bySequence.TryGetValue(motion.FiberSequence, out Fiber? owner);
            bool validOwner = ownerExists && owner!.ObjectId == actor.ObjectId && owner.Script.Id == motion.ScriptId;
            EventScript? issuingScript = program.Objects[actor.ObjectId].Scripts.FirstOrDefault(script => script.Id == motion.ScriptId);
            string movementOpcode = motion.Accelerated ? "MACCEL" : motion.HoldsFacing ? motion.Asynchronous ? "RCMOVE" : "CMOVE" : motion.TargetNativeObjectId is not null ? motion.Asynchronous ? "RMOVEA" : "MOVEA" : motion.Asynchronous ? motion.AutomaticAnimation ? "RMOVE" : "RFMOVE" : motion.AutomaticAnimation ? "MOVE" : "FMOVE";
            if (motion.HoldsFacing && !motion.Accelerated && (motion.AutomaticAnimation || motion.TargetNativeObjectId is not null))
            {
                throw new InvalidDataException("Saved held-facing CMOVE/RCMOVE lost its animation policy.");
            }
            if (motion.ResetAnimationOnCompletion is not null && (motion.ResetAnimationOnCompletion != true || motion.AutomaticAnimation || motion.TargetNativeObjectId is not null))
            {
                throw new InvalidDataException("Saved forced-animation movement has invalid inherited native animation flags.");
            }
            if (motion.Cancelled && (motion.Active || motion.Suspended || motion.AnimationFlag40000 is null || actor.MovementTarget is not null))
            {
                throw new InvalidDataException("Saved cancelled native movement has invalid mode0 or original asynchronous ownership.");
            }
            if (motion.TargetNativeObjectId is int targetId && ((uint)targetId >= program.NativeObjects.Count || !saved.Scene.NativeActorObjectIds.Contains(program.NativeObjects[targetId]) || !motion.AutomaticAnimation || motion.Active && !motion.Asynchronous && (!validOwner || owner!.Stack.Count == 0 || owner.Stack.Peek() != targetId)))
            {
                throw new InvalidDataException("Saved native MOVEA has invalid original target identity or remaining operand.");
            }
            if (motion.Accelerated && (!motion.HoldsFacing || !motion.AutomaticAnimation || motion.Asynchronous || motion.TargetNativeObjectId is not null || motion.Active && actor.NativeMovementAccelerationTarget is null))
            {
                throw new InvalidDataException("Saved native MACCEL lost its blocking held-facing mode or executed target WORD516.");
            }
            if (motion.FacingHold576 > 1 || motion.Version != 1 || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || (uint)motion.Operation >= program.Operations.Count || program.Operations[motion.Operation].Opcode.ToString() != movementOpcode || motion.Asynchronous && (issuingScript is null || motion.Operation < issuingScript.Start || motion.Operation >= issuingScript.End || ownerExists && !validOwner || validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || (actor.NativeMotionAuthority is not { Version: 1 } || actor.NativeMotionAuthority.FacingHold != (motion.Active || motion.Cancelled ? NativeMoveFacingHold(motion) : 0))) || actor.NativeMovementCompletion542 > 2 || !motion.Active && !motion.Cancelled && actor.NativeMovementCompletion542 == 1 || motion.Speed <= 0 || motion.Active && motion.Speed != actor.MovementSpeed || motion.Triangle != actor.WalkmeshTriangle || motion.Triangle is < 0 or > short.MaxValue || actor.X != motion.FixedX / 4096f || actor.Y != motion.FixedY / 4096f || actor.Z != motion.FixedZ / 4096f || motion.Active && actor.Direction != motion.FacingDirection || (motion.FixedZ & 4095) != 0 || new[] { motion.FixedX, motion.FixedY, motion.FixedZ, motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || motion.TargetNativeObjectId is null && new[] { motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value & 4095) != 0) || motion.Active && !motion.Asynchronous && (!validOwner || owner!.Complete || owner.Position != motion.Operation || owner.WaitReason != WaitReason.NativeScriptMovement) || !motion.Active && !motion.Cancelled && validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || motion.Active && actor.NativeMovementCompletion542 != 2 && actor.MovementTarget != new Vector3(motion.TargetX / 4096f, motion.TargetY / 4096f, motion.TargetZ / 4096f) || actor.NativeMovementCompletion542 == 2 && actor.MovementTarget is not null)
            {
                throw new InvalidDataException("Saved native scripted MOVE has invalid raw placement, owner, instruction wait or completion state.");
            }
        }
        if (restored.Any(fiber => fiber.WaitReason == WaitReason.NativeScriptMovement && fiber.NativeMoveOperation != fiber.Position))
        {
            throw new InvalidDataException("Saved native scripted MOVE wait has no original raw movement state.");
        }
        if (saved.PartyJoin is PartyJoinState join && (!bySequence.TryGetValue(join.FiberSequence, out Fiber? caller) || caller.ObjectId != join.CallerObjectId || caller.Complete || caller.WaitReason != WaitReason.PartyJoin || (uint)caller.Position >= program.Operations.Count || program.Operations[caller.Position].Opcode.ToString() != "JOIN" || saved.Scene.NativeActorLayoutVersion != 1 || join.Targets.Length > 2 || join.Targets.Select(target => target.ObjectId).Distinct().Count() != join.Targets.Length || join.Targets.Any(target => !saved.Scene.NativeActorObjectIds.Contains(target.ObjectId) || target.ObjectId == join.CallerObjectId || saved.Actors[target.ObjectId].NativeMovementCompletion542 is null or > 2 || target.Speed is <= 0 or > short.MaxValue)))
        {
            throw new InvalidDataException("Saved native JOIN does not match its preserved caller, retained targets or movement state.");
        }
        if (saved.PartyJoin is { } nativeJoin && (nativeJoin.Targets.Any(target => (target.FixedX >> 12) is < short.MinValue or > short.MaxValue || (target.FixedY >> 12) is < short.MinValue or > short.MaxValue || (target.FixedZ >> 12) is < short.MinValue or > short.MaxValue || (target.FixedZ & 4095) != 0 || saved.Actors[target.ObjectId].X != target.FixedX / 4096f || saved.Actors[target.ObjectId].Y != target.FixedY / 4096f || saved.Actors[target.ObjectId].Z != target.FixedZ / 4096f)))
        {
            throw new InvalidDataException("Saved native JOIN movement does not match its exact preserved raw companion coordinates.");
        }
        if (saved.PartyJoin is { } boundJoin && NativeRuntimeProfile is { IsVerified: true })
        {
            NativePartyBindingMemory memory = saved.Scene.PartyBindingMemory ?? throw new InvalidDataException("Saved native JOIN lacks its original saved party body pointers.");
            int[] bindings = Enumerable.Range(0, 3).Select(slot => (int)memory.At(slot)).ToArray();
            if (memory.Version != 1 || bindings.Any(binding => binding != byte.MaxValue && (uint)binding >= saved.Scene.NativeActorObjectIds.Count) || bindings[0] == byte.MaxValue || saved.Scene.NativeActorObjectIds[bindings[0]] != boundJoin.CallerObjectId || !bindings.Skip(1).Where(binding => binding != byte.MaxValue).Select(binding => saved.Scene.NativeActorObjectIds[binding]).SequenceEqual(boundJoin.Targets.Select(target => target.ObjectId)))
            {
                throw new InvalidDataException("Saved native JOIN targets differ from the original saved party bodies; runtime follow slots are not substitutes.");
            }
        }
        if (saved.PartyJoin is null && restored.Any(fiber => fiber.WaitReason == WaitReason.PartyJoin))
        {
            throw new InvalidDataException("Saved JOIN wait has no native companion movement state.");
        }
        if (saved.PartySplit is PartySplitState split && (saved.PartyJoin is not null || saved.UserControlEnabled || !bySequence.TryGetValue(split.FiberSequence, out Fiber? splitCaller) || splitCaller.ObjectId != split.CallerObjectId || splitCaller.Complete || splitCaller.WaitReason != WaitReason.PartySplit || (uint)splitCaller.Position >= program.Operations.Count || program.Operations[splitCaller.Position].Opcode.ToString() != "SPLIT" || saved.Scene.NativeActorLayoutVersion != 1 || split.Targets.Length is < 1 or > 3 || !split.Targets.Any(target => target.ObjectId == split.CallerObjectId) || split.Targets.Select(target => target.ObjectId).Distinct().Count() != split.Targets.Length || split.Targets.Any(target => !saved.Scene.NativeActorObjectIds.Contains(target.ObjectId) || saved.Actors[target.ObjectId].NativeMovementCompletion542 is null or > 2 || target.Speed is not (2696 or 7929) || saved.Actors[target.ObjectId].X != target.FixedX / 4096f || saved.Actors[target.ObjectId].Y != target.FixedY / 4096f || saved.Actors[target.ObjectId].Z != target.FixedZ / 4096f)))
        {
            throw new InvalidDataException("Saved native SPLIT does not match its preserved caller, raw party targets or reached movement context.");
        }
        if (saved.PartySplit is { } nativeSplit && (nativeSplit.Targets[0].ObjectId != nativeSplit.CallerObjectId || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || nativeSplit.Targets.Length > 1 && nativeSplit.Targets.Any(target => saved.Actors[target.ObjectId].CollisionEnabled) || nativeSplit.Targets.Any(target => new[] { target.FixedX, target.FixedY, target.FixedZ, target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || (target.FixedZ & 4095) != 0 || new[] { target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value & 4095) != 0)) || saved.Scene.NativePlayerPosition != new FieldFixedPosition(nativeSplit.Targets[0].FixedX, nativeSplit.Targets[0].FixedY, nativeSplit.Targets[0].FixedZ)))
        {
            throw new InvalidDataException("Saved native SPLIT requires its exact caller/party bindings, supported moving-peer flags and raw placement.");
        }
        if (saved.PartySplit is { } boundSplit)
        {
            NativePartyBindingMemory memory = saved.Scene.PartyBindingMemory ?? throw new InvalidDataException("Saved native SPLIT lacks its original saved party body pointers.");
            int[] bindings = Enumerable.Range(0, 3).Select(slot => (int)memory.At(slot)).ToArray();
            if (memory.Version != 1 || bindings.Any(binding => binding != byte.MaxValue && (uint)binding >= saved.Scene.NativeActorObjectIds.Count) || bindings[0] == byte.MaxValue || saved.Scene.NativeActorObjectIds[bindings[0]] != boundSplit.CallerObjectId || !bindings.Where(binding => binding != byte.MaxValue).Select(binding => saved.Scene.NativeActorObjectIds[binding]).SequenceEqual(boundSplit.Targets.Select(target => target.ObjectId)))
            {
                throw new InvalidDataException("Saved native SPLIT targets differ from saved party bodies; runtime follow slots are not substitutes.");
            }
        }
        if (saved.PartySplit is null && restored.Any(fiber => fiber.WaitReason == WaitReason.PartySplit))
        {
            throw new InvalidDataException("Saved SPLIT wait has no native party movement state.");
        }
        for (int index = 0; index < restored.Count; index++)
        {
            if (saved.Fibers[index].RequestSequence is long requested)
            {
                restored[index].Request = bySequence.GetValueOrDefault(requested) ?? throw new InvalidDataException("Saved remote-request wait references a missing fiber.");
            }
        }
        for (int index = 0; index < saved.Actors.Length; index++)
        {
            ActorState actor = saved.Actors[index];
            if (actor.ObjectId != index || actor.Label != program.Objects[index].Label || !float.IsFinite(actor.X) || !float.IsFinite(actor.Y) || !float.IsFinite(actor.Z) || !float.IsFinite(actor.AnimationFrame) || actor.NativeBaseAnimationVersion != 1 || actor.MovementActorId is int target && (uint)target >= actors.Count)
            {
                throw new InvalidDataException("Invalid saved actor state.");
            }
        }
        if (saved.AxisDuration is < 0 or > 65535 || saved.AxisElapsed < 0 || saved.AxisElapsed > saved.AxisDuration || saved.InputAxisDirection is < 0 or > 255 || saved.PendingArrival is FieldTransition arrival && arrival.FieldId != FieldId)
        {
            throw new InvalidDataException("Invalid saved movie, axis or arrival state.");
        }
        if (saved.LastFault is EventFault savedFault && (savedFault.FieldId != FieldId || (uint)savedFault.Operation >= program.Operations.Count || program.Operations[savedFault.Operation].Opcode.ToString() != savedFault.Opcode || !restored.Any(fiber => fiber.ObjectId == savedFault.ObjectId && fiber.Script.Id == savedFault.ScriptId && fiber.Position == savedFault.Operation)))
        {
            throw new InvalidDataException("Saved unsupported-opcode fault does not match its preserved instruction pointer.");
        }
        if (saved.WorldControls is NativeWorldControlState savedControls)
        {
            // A complete VM snapshot restores this canonical service state too.
            services.SetWorldControls(savedControls);
        }
        if (saved.NativeVariableValues is { Count: > 0 } || services.NativeVariableValues is { Count: > 0 })
        {
            // Validation completed before mutation; replace the complete captured memory state.
            Dictionary<int, int> originalVariables = new(variables);
            variables.Clear();
            foreach ((int address, int value) in saved.Variables)
            {
                variables.Add(address, value);
            }
            try
            {
                services.SetNativeVariableValues(saved.NativeVariableValues);
            }
            catch
            {
                variables.Clear();
                foreach ((int address, int value) in originalVariables)
                {
                    variables.Add(address, value);
                }
                throw;
            }
        }
        if (saved.MessageVariables is NativeMessageVariables messageVariables)
        {
            services.SetMessageVariables(messageVariables);
        }
        axisStart = saved.AxisStart;
        if (saved.ExamTimer is NativeExamTimerState savedTimer)
        {
            services.SetExamTimer(savedTimer);
        }
        if (saved.CharacterVitals is NativeCharacterVitals savedVitals)
        {
            services.SetCharacterVitals(savedVitals);
        }
        if (saved.CharacterAppearance is NativeCharacterAppearance savedAppearance)
        {
            services.SetCharacterAppearance(savedAppearance);
        }
        if (saved.CardInventory is NativeCardInventory savedCards)
        {
            services.SetCardInventory(savedCards);
        }
        if (saved.Economy is NativeEconomy savedEconomy)
        {
            services.SetEconomy(savedEconomy);
        }
        if (saved.SeeD is NativeSeeDState savedSeeD)
        {
            services.SetSeeD(savedSeeD);
        }
        axisTarget = saved.AxisTarget;
        axisDuration = saved.AxisDuration;
        axisElapsed = saved.AxisElapsed;
        InputAxisDirection = saved.InputAxisDirection;
        movieProgressExpected = saved.MovieProgressExpected || services.IsMoviePlaying;
        CurrentMoviePlayback = saved.CurrentMoviePlayback;
        pendingArrival = saved.PendingArrival;
        if (saved.LastFault is EventFault fault)
        {
            // A source upgrade may implement this exact saved instruction. Retrying then
            // uses its original PC/operands; unsupported snapshots remain frozen.
            LastFault = fault;
        }
        else
        {
            LastFault = null;
        }
        generation++;
        Frame = saved.Frame;
        accumulatedSeconds = saved.AccumulatedSeconds;
        randomState = saved.RandomState;
        preparedMovie = saved.PreparedMovie;
        sequence = saved.Sequence;
        defaultsStarted = saved.DefaultsStarted;
        transitionRequested = saved.TransitionRequested;
        IsUserControlEnabled = saved.UserControlEnabled;
        colorStartFrame = saved.ColorStartFrame;
        colorEndFrame = saved.ColorEndFrame;
        colorStart = saved.ColorStart;
        colorEnd = saved.ColorEnd;
        subtractColor = saved.SubtractColor;
        fadeStartFrame = saved.FadeStartFrame;
        fadeEndFrame = saved.FadeEndFrame;
        fadeStart = saved.FadeStart;
        fadeEnd = saved.FadeEnd;
        backgroundFps = saved.BackgroundFps;
        nameEntity = saved.NameEntity;
        actors.Clear();
        preparedGlobalSounds.Clear();
        preparedFieldSounds.Clear();
        preparedWorldTransitions.Clear();
        actors.AddRange(saved.Actors);
        fibers.Clear();
        fibers.AddRange(restored);
        variables.Clear();
        foreach ((int key, int value) in saved.Variables)
        {
            variables.Add(key, value);
        }
        dialogues.Clear();
        foreach (DialogueState dialogue in saved.Dialogues)
        {
            dialogues.Add(dialogue.Channel, dialogue);
        }
        CurrentDialogue = saved.CurrentDialogueChannel is int channel ? dialogues.GetValueOrDefault(channel) : null;
        backgroundStates.Clear();
        foreach ((int key, int value) in saved.BackgroundStates)
        {
            backgroundStates.Add(key, value);
        }
        backgroundAnimations.Clear();
        foreach (BackgroundCheckpoint animation in saved.BackgroundAnimations)
        {
            backgroundAnimations.Add(animation.Id, new BackgroundAnimation { NativeVersion = animation.NativeVersion, First = animation.First, Last = animation.Last, Speed = animation.Speed, Counter = animation.Counter, Position = animation.Position, Active = animation.Active, Complete = animation.Complete, Loop = animation.Loop });
        }
        Scene = saved.Scene;
        partyJoin = saved.PartyJoin;
        partySplit = saved.PartySplit;
        if (LastFault is EventFault retryableFault && Supports(program.Operations[retryableFault.Operation].Opcode))
        {
            Fiber faultedFiber = fibers.Single(fiber => fiber.ObjectId == retryableFault.ObjectId && fiber.Script.Id == retryableFault.ScriptId && fiber.Position == retryableFault.Operation);
            if (UnsupportedRequestContext(faultedFiber, program.Operations[retryableFault.Operation]) is null)
            {
                LastFault = null;
            }
        }
        RefreshCurrentDialogue();
    }

    private sealed record FiberCheckpoint(int ObjectId, int ScriptId, int Priority, long Sequence, bool Initialization, int Position, bool Complete, bool Started, NativeScalarValue[] Stack, string[] StackExpressions, Dictionary<int, NativeScalarValue> Results, WaitReason WaitReason, long WaitUntil, long? RequestSequence, int DialogueChannel, int BackgroundId, int SoundResourceId = 0, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? NativeDoorSlot = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? NativeActorSlot = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NativeMoveOperation = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NativeMoveTarget = null);
    private sealed record BackgroundCheckpoint(int Id, int First, int Last, int Speed, bool Complete, int NativeVersion = 0, int Counter = 0, int Position = -1, bool Active = false, bool Loop = false);
    private sealed record Checkpoint(int Version, int FieldId, string ProgramHash, long Frame, double AccumulatedSeconds, uint RandomState, int PreparedMovie, long Sequence, bool DefaultsStarted, bool TransitionRequested, bool UserControlEnabled, long ColorStartFrame, long ColorEndFrame, Vector3 ColorStart, Vector3 ColorEnd, bool SubtractColor, long FadeStartFrame, long FadeEndFrame, float FadeStart, float FadeEnd, int BackgroundFps, int NameEntity, ActorState[] Actors, FiberCheckpoint[] Fibers, DialogueState[] Dialogues, int? CurrentDialogueChannel, Dictionary<int, int> BackgroundStates, BackgroundCheckpoint[] BackgroundAnimations, Dictionary<int, int> Variables, SceneEventState Scene, int AxisStart = 0, int AxisTarget = 0, int AxisDuration = 0, int AxisElapsed = 0, int InputAxisDirection = 0, bool MovieProgressExpected = false, MoviePlaybackState? CurrentMoviePlayback = null, FieldTransition? PendingArrival = null, EventFault? LastFault = null, PartyJoinState? PartyJoin = null, PartySplitState? PartySplit = null, NativeWorldControlState? WorldControls = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeExamTimerState? ExamTimer = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<int, NativeMemoryValue>? NativeVariableValues = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCharacterVitals? CharacterVitals = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCharacterAppearance? CharacterAppearance = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCardInventory? CardInventory = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeSeeDState? SeeD = null, [property: JsonRequired] NativeMessageVariables? MessageVariables = null, [property: JsonRequired] NativeEconomy? Economy = null);
}
