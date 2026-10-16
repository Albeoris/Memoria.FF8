using Albeoris.Games.FF8.Jsm;
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
    NativeLadder,
    NativeLadderMode3,
    NativeHalt,
    NativeFaceSync,
    NativePartyMenu,
    NativeAnimationBlocking,
    NativeAnimationSync,
    NativeDoorRequest,
    NativeBackgroundShade,
    NativeTurnCompletion,
    NativeMovementFlush
}

public sealed partial class EventEngine
{
    private static readonly JsonSerializerOptions CheckpointOptions = new() { IncludeFields = true, Converters = { new JsonStringEnumConverter() } };
    private string programHash = "";

    /// <summary>Captures the complete cooperative VM, including typed waits and its deterministic random stream.</summary>
    public JsonElement Capture()
    {
        if (program is null)
        {
            throw new InvalidOperationException("No field has been loaded.");
        }
        Checkpoint snapshot = new(
            1, FieldId, programHash, Frame, accumulatedSeconds, preparedMovie, sequence, defaultsStarted, transitionRequested, userControlEnabled,
            backgroundFps, nameEntity,
            actors.ToArray(), fibers.Select(fiber => new FiberCheckpoint(fiber.ObjectId, fiber.Script.Id, fiber.Priority, fiber.Sequence, fiber.Initialization, fiber.Position, fiber.Complete, fiber.Started, fiber.NativeResultFrameEntered, fiber.Stack.Capture(), fiber.StackExpressions.ToArray(), new Dictionary<int, NativeScalarValue>(fiber.Results), fiber.WaitReason, fiber.WaitUntil, fiber.Request?.Sequence, fiber.DialogueChannel, fiber.BackgroundId, fiber.SoundResourceId, fiber.NativeDoorSlot, fiber.NativeActorSlot, fiber.NativeMoveOperation, fiber.NativeMoveTarget, fiber.NativeDoorRequestWait, fiber.NativeDoorDefaultFrame, fiber.NativeDoorSavedResults, fiber.NativeShadeOperation)).ToArray(),
            dialogues.Values.ToArray(), CurrentDialogue?.Channel, new Dictionary<int, int>(backgroundStates), backgroundAnimations.Select(pair => new BackgroundCheckpoint(pair.Key, pair.Value.First, pair.Value.Last, pair.Value.Speed, pair.Value.Complete, pair.Value.NativeVersion, pair.Value.Counter, pair.Value.Position, pair.Value.Active, pair.Value.Loop)).ToArray(), EventMemory, Scene, axisStart, axisTarget, axisDuration, axisElapsed, InputAxisDirection, movieProgressExpected, CurrentMoviePlayback, pendingArrival, LastFault, partyJoin, partySplit, services.WorldControls, services.ExamTimer, services.CharacterVitals, services.CharacterAppearance, services.CardInventory, services.SeeD, services.MessageVariables, services.Economy, services.GlobalSoundProcess, services.SalaryPresentation, services.FieldShake, services.InputConfiguration, services.FieldButtonInput, services.ShotAvailability, partyMenu, services.EventRandom, services.NativeFieldLocation, services.VibrationProcess, services.FieldFade, services.ShopHistory, services.MenuRandom, services.WeaponRecipes, services.WorldEntryMemory, services.DiscProcess, services.MovieSource, userControlSequenceDisabled, services.FieldBars);
        return JsonSerializer.SerializeToElement(snapshot, CheckpointOptions);
    }

    /// <summary>Restores a captured VM after LoadField has supplied exactly the same field's bytes and messages.</summary>
    private void RestoreCore(JsonElement snapshot)
    {
        Checkpoint saved = snapshot.Deserialize<Checkpoint>(CheckpointOptions) ?? throw new InvalidDataException("Empty event checkpoint.");
        if (saved.EventMemory is null)
        {
            throw new InvalidDataException("Saved VM requires explicit typed event memory; the snapshot is missing required native state");
        }
        if (saved.UserControlSequenceDisabled is null)
        {
            throw new InvalidDataException("Saved VM requires its independent native UCOFF sequence owner; migrate this save explicitly.");
        }
        saved.FieldBars?.Validate();
        if (saved.FieldBars is null && services.FieldBars is not null || saved.FieldBars is not null && !services.CanStoreFieldBars)
        {
            throw new InvalidDataException("Saved VM cannot replace known native global bars with unknown state or an unsupported service.");
        }
        saved.EventMemory.Validate();
        ValidateSceneMemoryOwners(saved.EventMemory, saved.Scene);
        if (program is null || saved.Version != 1 || saved.FieldId != FieldId || saved.ProgramHash != programHash || saved.Actors.Length != actors.Count || saved.Frame < 0 || !double.IsFinite(saved.AccumulatedSeconds) || saved.AccumulatedSeconds < 0 || saved.AccumulatedSeconds > 10)
        {
            throw new InvalidDataException("Event checkpoint does not match the loaded field/program or has invalid timing.");
        }
        if (!snapshot.TryGetProperty("WorldControls", out _) || saved.WorldControls is null && services.WorldControls is not null)
        {
            throw new InvalidDataException("Saved VM must preserve its canonical world-control state; missing or unknown snapshot state cannot retain a different known service state.");
        }
        saved.WorldControls?.Validate();
        foreach (ActorState actor in saved.Actors)
        {
            actor.NativeOperandMemory?.Validate();
        }
        ValidateNativeMovieMask(saved.Scene, saved.PreparedMovie, saved.Frame, saved.CurrentMoviePlayback, saved.MovieSource);
        saved.NativeFieldLocation?.Validate();
        if (saved.NativeFieldLocation is null && services.NativeFieldLocation is not null || saved.NativeFieldLocation is not null && !services.CanStoreNativePartyMenuOwners)
        {
            throw new InvalidDataException("Saved VM cannot replace the actual470250 location with unknown history or an unsupported service.");
        }
        saved.ShotAvailability?.Validate();
        if (saved.ShotAvailability is null && services.ShotAvailability is not null || saved.ShotAvailability is not null && !services.CanStoreNativePartyMenuOwners)
        {
            throw new InvalidDataException("Saved VM cannot replace native shot availability with unknown history or an unsupported service.");
        }
        saved.InputConfiguration?.Validate();
        if (saved.InputConfiguration is null && services.InputConfiguration is not null || saved.InputConfiguration is not null && !services.CanStoreInputConfiguration)
        {
            throw new InvalidDataException("Saved VM cannot replace known native input options with unknown history or an unsupported service.");
        }
        if (saved.MessageVariables is null && services.MessageVariables is not null || saved.MessageVariables is not null && !services.CanStoreMessageVariables)
        {
            throw new InvalidDataException("Saved VM cannot replace known process message variables with unknown history or an unsupported service.");
        }
        if (saved.Scene.NativeSalaryMovementUnits is not null and not (0 or 3 or 5) || NativeRuntimeProfile is { IsVerified: true } && saved.Scene.NativeSalaryMovementUnits is null)
        {
            throw new InvalidDataException("Saved native field must preserve its actual previous salary movement scalar0/3/5; migrate this save explicitly.");
        }
        if (!HasCanonicalPartySlots(saved.Scene))
        {
            throw new InvalidDataException("Saved VM requires exactly three native party character bytes; compressed or malformed party slots require authentic reconstruction.");
        }
        if (NativeRuntimeProfile is { IsVerified: true } && saved.Scene.NativeActorObjectIds.Any(id => saved.Actors[id].NativeTurnMode580 is null or > 3))
        {
            throw new InvalidDataException("Saved physical actor requires its explicit native turn mode580; migrate this save explicitly.");
        }
        ValidateNativeModelParts(saved.Scene, saved.Actors, saved.Frame, saved.DefaultsStarted, saved.TransitionRequested, saved.MovieSource);
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
            throw new InvalidDataException("Active or pending movie snapshot has no executed native actor lifecycle; the snapshot is missing required native state");
        }
        foreach (NativeMemoryValue entry in saved.EventMemory.Cells.Values)
        {
            ValidateNativeScalarSource(entry.Value, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer, saved.Scene);
        }
        if (saved.Scene.UnlockedTutorials.Any(id => (uint)id >= 128))
        {
            throw new InvalidDataException("Saved tutorial unlock ID is outside the native bitmap.");
        }
        JsonElement savedActorJson = snapshot.GetProperty("Actors");
        for (int index = 0; index < saved.Actors.Length; index++)
        {
            Vector3? color = saved.Actors[index].PolygonColor;
            if (color is Vector3 rgb && !ValidNativePolygonColor(rgb) || NativeRuntimeProfile is { IsVerified: true } && program.NativeObjects.Take(program.NativeActorCount).Contains(index) && color is null)
            {
                throw new InvalidDataException("Saved native actor must preserve its explicit BYTE394/395/396 RGB; generic defaults and nonbyte values are unsupported.");
            }
            if (!savedActorJson[index].TryGetProperty("FaceStateVersion", out _) || !savedActorJson[index].TryGetProperty("NativeBaseAnimationVersion", out _) || saved.Actors[index].FaceStateVersion != 1 || saved.Actors[index].NativeBaseAnimationVersion != 1)
            {
                throw new InvalidDataException("Saved actor lacks current native face or base animation provenance; the snapshot is missing required native state");
            }
            ValidateNativeFaceCheckpoint(savedActorJson[index], saved.Actors[index]);
            if (saved.Frame == 0 && !saved.DefaultsStarted && saved.PendingArrival is not null && saved.Fibers.All(fiber => fiber.Initialization && !fiber.Started) && saved.Actors[index].WalkmeshTriangle == -1)
            {
                throw new InvalidDataException("Saved allocation contains the obsolete invented triangle sentinel; the snapshot is missing required native state");
            }
        }
        ValidateNativeAnimationCheckpoint(saved);
        ValidateNativeEyes(saved.Actors);
        if (snapshot.GetProperty("Dialogues").EnumerateArray().Any(dialogue => !dialogue.TryGetProperty("CanConfirm", out _)))
        {
            throw new InvalidDataException("Saved dialogue lacks native confirmability; the snapshot is missing required native state");
        }
        ValidatePreviousFieldCheckpoint(saved);
        ValidatePreviousWorldEntityCheckpoint(saved, IsDormantWorldReturnCheckpoint(snapshot));
        saved.ExamTimer?.Validate();
        if (services.ExamTimer is not null && saved.ExamTimer is null)
        {
            throw new InvalidDataException("Saved VM lacks the current process clock; the snapshot is missing required native state");
        }
        if (saved.Scene.AudioLoadEpoch <= 0)
        {
            throw new InvalidDataException("Saved process audio history requires its executed field-load identity; the snapshot is missing required native state");
        }
        saved.Scene.FieldCallbackClock?.Validate(saved.Frame);
        if (saved.Scene.FieldCallbackClock is { } callbackClock && (saved.DefaultsStarted && callbackClock.InitializationTransactions != 1 || saved.Frame == 0 && callbackClock.InitializationTransactions != 0))
        {
            throw new InvalidDataException("Saved field callback clock lacks its executed synchronous initialization transaction.");
        }
        saved.FieldShake?.Validate();
        if (saved.FieldShake is null && services.FieldShake is not null || saved.FieldShake is not null && !services.CanStoreFieldShake)
        {
            throw new InvalidDataException("Saved VM cannot replace its current shake process owner with unknown history or an unsupported service.");
        }
        if (saved.FieldShake is NativeFieldShake restoredShake && (restoredShake.X.Enabled || restoredShake.X.Oscillating || restoredShake.X.Duration > restoredShake.X.Counter || restoredShake.Y.Enabled || restoredShake.Y.Oscillating || restoredShake.Y.Duration > restoredShake.Y.Counter) && ShakeTables is null)
        {
            throw new InvalidDataException("Saved native shake process history requires authenticated original lookup tables.");
        }
        saved.CharacterVitals?.Validate();
        if (saved.CharacterVitals is not null && saved.EventMemory.Cells.Any(pair => pair.Key < 194 && pair.Key + pair.Value.Width > 188))
        {
            throw new InvalidDataException("Canonical JUNCTION backup bytes overlap a separate saved VM alias; migrate this save explicitly.");
        }
        saved.CharacterAppearance?.Validate();
        saved.CardInventory?.Validate();
        saved.WeaponRecipes?.Validate();
        if (saved.WeaponRecipes is null && services.WeaponRecipes is not null || saved.WeaponRecipes is not null && !services.CanStoreWeaponRecipes)
        {
            throw new InvalidDataException("Saved VM cannot erase native weapon recipes or restore them through an unsupported service.");
        }
        saved.MovieSource?.Validate();
        if (saved.MovieSource is null && services.MovieSource is not null || saved.MovieSource is not null && !services.CanStoreMovieSource)
        {
            throw new InvalidDataException("Saved VM lacks matching prepared movie resource ownership.");
        }
        saved.DiscProcess?.Validate();
        if (saved.DiscProcess is null && services.DiscProcess is not null || saved.DiscProcess is not null && (!services.CanStoreDiscProcess || saved.EventMemory.Cells.Any(pair => pair.Key <= 204 && pair.Key + pair.Value.Width > 204)))
        {
            throw new InvalidDataException("Saved VM lacks matching native disc process ownership.");
        }
        saved.MenuRandom?.Validate();
        if (saved.MenuRandom is null && services.MenuRandom is not null || saved.MenuRandom is not null && !services.CanStoreMenuRandom)
        {
            throw new InvalidDataException("Saved VM cannot erase native menu RNG or restore it through an unsupported service.");
        }
        saved.ShopHistory?.Validate();
        if (saved.ShopHistory is null && services.ShopHistory is not null || saved.ShopHistory is not null && !services.CanStoreShopHistory)
        {
            throw new InvalidDataException("Saved VM cannot erase native shop history or restore it through an unsupported service.");
        }
        saved.FieldFade?.Validate();
        if (saved.FieldFade is null && services.FieldFade is not null || saved.FieldFade is not null && !services.CanStoreFieldFade)
        {
            throw new InvalidDataException("Saved VM cannot erase its native field fade owner or restore it through an unsupported service.");
        }
        saved.VibrationProcess?.Validate();
        if (saved.VibrationProcess is null && services.VibrationProcess is not null || saved.VibrationProcess is not null && !services.CanStoreVibrationProcess)
        {
            throw new InvalidDataException("Saved native vibration queue requires matching known process ownership.");
        }
        saved.EventRandom?.Validate();
        if (saved.EventRandom is null && services.EventRandom is not null || saved.EventRandom is { DrawPoints: null } && services.EventRandom?.DrawPoints is not null || saved.EventRandom is not null && !services.CanStoreEventRandom || saved.EventRandom is not null && saved.EventMemory.Cells.Any(pair => pair.Key < 182 && pair.Key + pair.Value.Width > 116))
        {
            throw new InvalidDataException("Saved native random process requires matching known ownership and canonical packed depletion memory.");
        }
        saved.Economy?.Validate();
        saved.GlobalSoundProcess?.Validate();
        saved.SalaryPresentation?.Validate();
        if (saved.GlobalSoundProcess is null && services.GlobalSoundProcess is not null || saved.GlobalSoundProcess is not null && !services.CanStoreGlobalSoundProcess || saved.SalaryPresentation is null && services.SalaryPresentation is not null || saved.SalaryPresentation is not null && !services.CanStoreSalaryPresentation)
        {
            throw new InvalidDataException("Saved presentation process owners require complete matching current shapes and persistent services.");
        }
        if (saved.Economy is not null && !services.CanStoreEconomy || saved.Economy is null && services.Economy is not null)
        {
            throw new InvalidDataException("Saved economy requires matching known history and a persistent service; migrate this save explicitly.");
        }
        if (saved.Economy is not null && (saved.EventMemory.Cells.Any(pair => pair.Key < 80 && pair.Key + pair.Value.Width > 72) || saved.CharacterVitals is not null && saved.Economy.InitOutSha256 != saved.CharacterVitals.InitOutSha256))
        {
            throw new InvalidDataException("Saved economy overlaps another memory owner or has a different New Game template identity.");
        }
        saved.SeeD?.Validate();
        if (saved.SeeD is not null && !services.CanStoreSeeD || saved.SeeD is null && services.SeeD is not null || saved.SeeD is not null && saved.EventMemory.Cells.Any(pair => pair.Key < 12 && pair.Key + pair.Value.Width > 8 || pair.Key < 20 && pair.Key + pair.Value.Width > 16 || pair.Key < 188 && pair.Key + pair.Value.Width > 184))
        {
            throw new InvalidDataException("Saved SeeD rank and salary require matching native state and canonical WORD16 ownership; the snapshot is missing required native state");
        }
        if (saved.CardInventory is not null && !services.CanStoreCardInventory || saved.CardInventory is null && services.CardInventory is not null)
        {
            throw new InvalidDataException("Saved card inventory requires matching native history and a persistent service; the snapshot is missing required native state");
        }
        if (saved.CharacterAppearance is not null && saved.CharacterVitals is not null && saved.CharacterAppearance.InitOutSha256 != saved.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("Saved character appearance and vitals have different native New Game identities.");
        }
        saved.FieldButtonInput?.Validate();
        if (saved.Scene.FieldCallbackClock is { } sampledClock && (sampledClock.OrdinaryCallbacks > 0 || sampledClock.MovieCallbacks > 0) && saved.FieldButtonInput is not null)
        {
            saved.FieldButtonInput.ValidateSample(saved.FieldId, saved.Frame, saved.InputConfiguration);
        }
        if (saved.FieldButtonInput is null && services.FieldButtonInput is not null || saved.FieldButtonInput is not null && !services.CanStoreFieldButtonInput)
        {
            throw new InvalidDataException("Saved VM cannot replace known input sampler history with an absent process or unsupported service.");
        }
        if (saved.Dialogues.Select(dialogue => dialogue.Channel).Distinct().Count() != saved.Dialogues.Length || saved.Dialogues.Any(dialogue => (uint)dialogue.Channel >= 8) || saved.CurrentDialogueChannel is int focusedChannel && !saved.Dialogues.Any(dialogue => dialogue.Channel == focusedChannel))
        {
            throw new InvalidDataException("Saved dialogue channels must be distinct original window IDs and contain the focused channel.");
        }
        ValidateDrawPointCheckpoint(saved);
        ValidateDrawPointInteractionCheckpoint(saved);
        ValidateSoundInitializationCheckpoint(saved);
        ValidateSoundStopCheckpoint(saved);
        ValidateSoundVolumeTransitionCheckpoint(saved);
        ValidateSoundVolumeCheckpoint(saved);
        ValidateSoundPositionCheckpoint(saved);
        ValidateVibrationCheckpoint(saved);
        ValidateAllSoundVolumeTransitionCheckpoint(saved);
        ValidateAllSoundVolumeCheckpoint(saved);
        ValidateFollowOffCheckpoint(saved);
        ValidateMusicStopCheckpoint(saved);
        ValidateMusicLoadCheckpoint(saved);
        ValidateMusicVolumeCheckpoint(saved);
        ValidateNativeFadeCheckpoint(saved);
        ValidateNativeLineCheckpoint(saved);
        ValidateFieldSoundCheckpoint(saved);
        ValidateParticleCheckpoint(saved);
        ValidateWorldEntryMemoryCheckpoint(saved);
        if (NativeRuntimeProfile is { IsVerified: true } && (saved.Fibers.Any(fiber => fiber.Results.Keys.Any(index => (uint)index >= 8) || !fiber.NativeResultFrameEntered && !fiber.NativeDoorDefaultFrame && (fiber.Stack.Length != 0 || fiber.Results.Count != 0 && !IsSavedNativeResultCurrent(saved, fiber) || fiber.Position != program!.Objects[fiber.ObjectId].Scripts.Single(script => script.Id == fiber.ScriptId).Start)) || saved.Fibers.Where(fiber => !fiber.Complete && (fiber.NativeResultFrameEntered || fiber.NativeDoorDefaultFrame)).GroupBy(fiber => fiber.ObjectId).Any(group => group.Sum(fiber => fiber.Stack.Length + (fiber.NativeResultFrameEntered ? 8 : 0)) > 80)))
        {
            throw new InvalidDataException("Saved native operand/result banks require represented in-bank writes; unchecked physical memory aliases are unsupported.");
        }
        foreach (ActorState actor in saved.Actors)
        {
            bool allocated = saved.Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, saved.FieldId, saved.ProgramHash) == true && program!.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId);
            if (allocated && actor.NativePartyFlags is null)
            {
                throw new InvalidDataException("Saved allocated actor lost its required executed DWORD352 masks4/64.");
            }
            if (allocated != (actor.NativeSavedCollisionDisabled16 is not null))
            {
                throw new InvalidDataException("Saved actor DWORD352 mask16 requires its explicit native allocation and executed HIDE/SHOW history.");
            }
            if (allocated != (actor.NativeActorWord398 is not null) || allocated && actor.NativeActorWord398 != 0)
            {
                throw new InvalidDataException("Saved physical actor WORD398 requires its explicit native allocation and original header write history.");
            }
            if (allocated != (actor.NativeLadderAnimation is not null))
            {
                throw new InvalidDataException("Saved ladder animation bytes594..596 require the current explicit native actor allocation.");
            }
            if (allocated && (actor.NativeMovementCompletion542 is null or > 6 || actor.NativeMovementCompletion542 > 2 && actor.NativeLadder is not { Active: true } || actor.NativeLandingTriangle508 is null || actor.NativeTurnAccumulator474 is null || actor.NativeMovementGroundContact600 is null) || !allocated && (actor.NativeMovementCompletion542 is not null || actor.NativeLandingTriangle508 is not null || actor.NativeTurnAccumulator474 is not null || actor.NativeMovementGroundContact600 is not null))
            {
                throw new InvalidDataException("Saved physical WORD474/508/542 requires current explicit native allocation state.");
            }
        }
        ValidateDisplayOffsetCheckpoint(saved);
        ValidateActorBackgroundScrollCheckpoint(saved);
        ValidateNativeLinearTurns(saved);
        ValidateNativeNormalBackgroundScrollCheckpoint(saved);
        ValidateNativeBackgroundScrollCurrents(saved);
        ValidateActorShadowCheckpoint(saved);
        ValidateFootstepPairCheckpoint(saved);
        ValidateFootstepOffAllCheckpoint(saved);
        if (saved.Scene.NativeFieldInitialization is not null && saved.Scene.BackgroundScrollLayers.Count != 8)
        {
            throw new InvalidDataException("Native field background settings require all eight executed current loader records; the snapshot is missing required native state");
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
            throw new InvalidDataException("Saved managed scripted movement is unsupported; the snapshot is missing required native state");
        }
        if (saved.Scene.FieldPartyNativeActorIds.Count != 0 && saved.Scene.NativeActorLayoutVersion != 1)
        {
            throw new InvalidDataException("Legacy refreshed party bindings predate native actor compaction. Load a snapshot with native actor compaction state.");
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
        foreach (GlobalSoundRequest request in saved.Scene.GlobalSoundRequests)
        {
            if (request.Allocation is null || request.ChannelMask != (request.RawChannelMask & 0xFFFFFF) || request.Voice != request.Allocation.Voice || request.PitchPercent != 100 || request.EffectivePan != request.RawPan >> 1 || !ValidGlobalSoundResource(request.Resource, MapGlobalSoundSample(request.OriginalEffectId)))
            {
                throw new InvalidDataException("Saved global sound attempt lost its native allocator, mapping, pan or resource policy.");
            }
            NativeGlobalSoundProcess.ValidateAllocation(request.RawChannelMask, request.Allocation);
        }

        if (saved.BackgroundAnimations.Any(animation => animation.NativeVersion != 1))
        {
            throw new InvalidDataException("Background animation requires the current native state contract; the snapshot is missing required native state");
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
            if (!script.ContainsPosition(savedFiber.Position, savedFiber.Complete))
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
                if (savedFiber.Complete || layer is not int selected || !saved.Scene.BackgroundScrollPositions.ContainsKey(selected) && !(saved.Scene.ActorBackgroundScroll is { } actorScroll && actorScroll.Layer == selected))
                {
                    throw new InvalidDataException("Saved scroll synchronization wait does not preserve its original instruction, unconsumed layer operand and known scroll state.");
                }
            }
            if (savedFiber.WaitReason == WaitReason.NativeFaceSync && (savedFiber.Complete || !script.ContainsOperation(savedFiber.Position) || program.Operations[savedFiber.Position].Opcode != Jsm.Opcode.FACEDIRSYNC || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(savedFiber.ObjectId) || saved.Actors[savedFiber.ObjectId].FaceStateVersion != 1))
            {
                throw new InvalidDataException("Saved FACEDIRSYNC wait requires its original retained PC and current native face-word owner.");
            }
            if (savedFiber.NativeShadeOperation is int shadeOperation && (savedFiber.Complete || shadeOperation != savedFiber.Position || (uint)shadeOperation >= program.Operations.Count || program.Operations[shadeOperation].Opcode != Jsm.Opcode.BGSHADE || savedFiber.WaitReason != WaitReason.NativeBackgroundShade || program.Objects[savedFiber.ObjectId].BackgroundModuleIndex != savedFiber.BackgroundId || !saved.Scene.BackgroundShades.ContainsKey(savedFiber.BackgroundId) && !saved.Scene.BackgroundShadeLoops.ContainsKey(savedFiber.BackgroundId)) || savedFiber.WaitReason == WaitReason.NativeBackgroundShade && savedFiber.NativeShadeOperation is null)
            {
                throw new InvalidDataException("Saved BGSHADE wait lost its issued instruction or actual module shade owner.");
            }
            Fiber fiber = new(savedFiber.ObjectId, script, savedFiber.Priority, savedFiber.Sequence, savedFiber.Initialization)
            {
                Position = savedFiber.Position,
                Complete = savedFiber.Complete,
                Started = savedFiber.Started,
                NativeResultFrameEntered = savedFiber.NativeResultFrameEntered,
                WaitReason = savedFiber.WaitReason,
                WaitUntil = savedFiber.WaitUntil,
                DialogueChannel = savedFiber.DialogueChannel,
                BackgroundId = savedFiber.BackgroundId,
                SoundResourceId = savedFiber.SoundResourceId,
                NativeDoorSlot = savedFiber.NativeDoorSlot,
                NativeActorSlot = savedFiber.NativeActorSlot,
                NativeMoveOperation = savedFiber.NativeMoveOperation,
                NativeMoveTarget = savedFiber.NativeMoveTarget,
                NativeShadeOperation = savedFiber.NativeShadeOperation,
                NativeDoorRequestWait = savedFiber.NativeDoorRequestWait,
                NativeDoorDefaultFrame = savedFiber.NativeDoorDefaultFrame,
                NativeDoorSavedResults = savedFiber.NativeDoorSavedResults
            };
            foreach (NativeScalarValue operand in savedFiber.Stack.Reverse())
            {
                ValidateNativeScalarSource(operand, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer, saved.Scene);
                fiber.Stack.Push(operand);
            }
            foreach (string expression in savedFiber.StackExpressions.Reverse())
            {
                fiber.StackExpressions.Push(expression);
            }
            foreach ((int key, NativeScalarValue value) in savedFiber.Results)
            {
                ValidateNativeScalarSource(value, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer, saved.Scene);
                fiber.Results.Add(key, value);
            }
            foreach (NativeScalarValue value in savedFiber.NativeDoorSavedResults ?? [])
            {
                ValidateNativeScalarSource(value, saved.FieldId, saved.ProgramHash, saved.Frame, saved.Scene.NativeFieldInitialization, saved.ExamTimer, saved.Scene);
            }
            restored.Add(fiber);
        }
        Dictionary<long, Fiber> bySequence = restored.ToDictionary(fiber => fiber.Sequence);
        ValidateNativeDoorSuspensionCheckpoint(saved, restored);
        ValidateNativeJumps(saved, restored);
        ValidateNativeLadders(saved, restored);
        ValidateNativeLaddersMode3(saved, restored);
        ValidateNativeActorContactCheckpoint(saved);
        ValidateNativeMovementRequests(saved, restored);
        ValidateBackgroundShades(saved);
        ValidateNativeJoinedPositions(saved);
        ValidateNativeCopiedPositions(saved);
        ValidateWorldTransitionCheckpoint(saved, bySequence, IsDormantWorldReturnCheckpoint(snapshot));
        ValidateMinimalMenuCheckpoint(saved, bySequence);
        ValidatePartyMenuCheckpoint(saved, bySequence);
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
            bool directionPoint = (uint)motion.Operation < program.Operations.Count && program.Operations[motion.Operation].Opcode == Jsm.Opcode.DIRP;
            bool partyTarget = IsNativePartyMove(motion.Operation);
            bool asyncPartyTarget = IsNativeAsyncPartyMove(motion.Operation);
            string movementOpcode = asyncPartyTarget ? "RPMOVEA" : partyTarget ? "PMOVEA" : directionPoint ? "DIRP" : motion.Accelerated ? "MACCEL" : motion.HoldsFacing ? motion.Asynchronous ? "RCMOVE" : "CMOVE" : motion.TargetNativeObjectId is not null ? motion.Asynchronous ? "RMOVEA" : motion.AutomaticAnimation ? "MOVEA" : "FMOVEA" : motion.Asynchronous ? motion.AutomaticAnimation ? "RMOVE" : "RFMOVE" : motion.AutomaticAnimation ? "MOVE" : "FMOVE";
            if (asyncPartyTarget && (!motion.Asynchronous || !motion.AutomaticAnimation || motion.HoldsFacing || motion.Accelerated || motion.TargetNativeObjectId is null))
            {
                throw new InvalidDataException("Saved RPMOVEA lost its one-time raw party target or asynchronous native mode1 policy.");
            }
            if (partyTarget && (motion.Asynchronous || !motion.AutomaticAnimation || motion.HoldsFacing || motion.Accelerated || motion.Active && (!validOwner || owner!.Stack.Count == 0 || !owner.Stack.PeekValue().IsExact || (uint)owner.Stack.Peek() >= 3 || owner.NativeMoveTarget != (int)owner.Stack.Peek())))
            {
                throw new InvalidDataException("Saved PMOVEA lost its full DWORD party-slot consumer or native mode1 policy.");
            }
            if (motion.HoldsFacing && !motion.Accelerated && (motion.AutomaticAnimation || motion.TargetNativeObjectId is not null))
            {
                throw new InvalidDataException("Saved held-facing CMOVE/RCMOVE lost its animation policy.");
            }
            if (motion.ResetAnimationOnCompletion is not null && (motion.ResetAnimationOnCompletion != true || motion.AutomaticAnimation || motion.TargetNativeObjectId is not null && !IsNativeForcedActorMove(motion.Operation)))
            {
                throw new InvalidDataException("Saved forced-animation movement has invalid inherited native animation flags.");
            }
            if (motion.Cancelled && (motion.Active || motion.Suspended || motion.AnimationFlag40000 is null || actor.MovementTarget is not null))
            {
                throw new InvalidDataException("Saved cancelled native movement has invalid mode0 or original asynchronous ownership.");
            }
            if (motion.TargetNativeObjectId is int targetId && ((uint)targetId >= program.NativeObjects.Count || !saved.Scene.NativeActorObjectIds.Contains(program.NativeObjects[targetId]) || !motion.AutomaticAnimation && !IsNativeForcedActorMove(motion.Operation) || motion.Active && !motion.Asynchronous && !partyTarget && (!validOwner || owner!.Stack.Count == 0 || owner.Stack.Peek() != targetId)))
            {
                throw new InvalidDataException("Saved native MOVEA has invalid original target identity or remaining operand.");
            }
            if (motion.Accelerated && (!motion.HoldsFacing || !motion.AutomaticAnimation || motion.Asynchronous || motion.TargetNativeObjectId is not null || motion.Active && actor.NativeMovementAccelerationTarget is null))
            {
                throw new InvalidDataException("Saved native MACCEL lost its blocking held-facing mode or executed target WORD516.");
            }
            if (motion.FacingHold576 > 1 || motion.Version != 1 || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || (uint)motion.Operation >= program.Operations.Count || program.Operations[motion.Operation].Opcode.ToString() != movementOpcode || motion.Asynchronous && (issuingScript is null || !issuingScript.ContainsOperation(motion.Operation) || ownerExists && !validOwner || validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || (actor.NativeMotionAuthority is not { Version: 1 } || actor.NativeMotionAuthority.FacingHold != (motion.Active || motion.Cancelled ? NativeMoveFacingHold(motion) : 0))) || actor.NativeMovementCompletion542 > 2 || !motion.Active && !motion.Cancelled && actor.NativeMovementCompletion542 == 1 || (directionPoint ? motion.Speed < 0 || motion.Active && motion.Speed != 0 || motion.StopDistance != 0 || motion.AutomaticAnimation || motion.HoldsFacing || motion.Asynchronous || motion.Accelerated || motion.TargetNativeObjectId is not null : motion.Speed < 0) || motion.Active && motion.Speed != actor.MovementSpeed || motion.Triangle != actor.WalkmeshTriangle || motion.Triangle is < 0 or > short.MaxValue || actor.X != motion.FixedX / 4096f || actor.Y != motion.FixedY / 4096f || actor.Z != motion.FixedZ / 4096f || motion.Active && actor.Direction != motion.FacingDirection || (motion.FixedZ & 4095) != 0 || new[] { motion.FixedX, motion.FixedY, motion.FixedZ, motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || motion.TargetNativeObjectId is null && !partyTarget && new[] { motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value & 4095) != 0) || motion.Active && !motion.Asynchronous && (!validOwner || owner!.Complete || owner.Position != motion.Operation || owner.WaitReason != WaitReason.NativeScriptMovement) || !motion.Active && !motion.Cancelled && validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || motion.Active && actor.NativeMovementCompletion542 != 2 && actor.MovementTarget != new Vector3(motion.TargetX / 4096f, motion.TargetY / 4096f, motion.TargetZ / 4096f) || actor.NativeMovementCompletion542 == 2 && actor.MovementTarget is not null)
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
        if (saved.PartySplit is PartySplitState split && (saved.PartyJoin is not null || !bySequence.TryGetValue(split.FiberSequence, out Fiber? splitCaller) || splitCaller.ObjectId != split.CallerObjectId || splitCaller.Complete || splitCaller.WaitReason != WaitReason.PartySplit || (uint)splitCaller.Position >= program.Operations.Count || program.Operations[splitCaller.Position].Opcode.ToString() != "SPLIT" || saved.Scene.NativeActorLayoutVersion != 1 || split.Targets.Length is < 1 or > 3 || split.Targets.Select(target => target.ObjectId).Distinct().Count() != split.Targets.Length || split.Targets.Any(target => !saved.Scene.NativeActorObjectIds.Contains(target.ObjectId) || saved.Actors[target.ObjectId].NativeMovementCompletion542 is null or > 2 || target.Speed is not (2696 or 7929) || saved.Actors[target.ObjectId].X != target.FixedX / 4096f || saved.Actors[target.ObjectId].Y != target.FixedY / 4096f || saved.Actors[target.ObjectId].Z != target.FixedZ / 4096f)))
        {
            throw new InvalidDataException("Saved native SPLIT does not match its preserved caller, raw party targets or reached movement context.");
        }
        if (saved.PartySplit is { } nativeSplit && (saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || nativeSplit.Targets.Any(target => new[] { target.FixedX, target.FixedY, target.FixedZ, target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || (target.FixedZ & 4095) != 0 || new[] { target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value & 4095) != 0))))
        {
            throw new InvalidDataException("Saved native SPLIT requires its exact saved party bindings and raw placement.");
        }
        if (saved.PartySplit is { } boundSplit)
        {
            NativePartyBindingMemory memory = saved.Scene.PartyBindingMemory ?? throw new InvalidDataException("Saved native SPLIT lacks its original saved party body pointers.");
            int[] bindings = Enumerable.Range(0, 3).Select(slot => (int)memory.At(slot)).ToArray();
            if (memory.Version != 1 || bindings.Any(binding => binding != byte.MaxValue && (uint)binding >= saved.Scene.NativeActorObjectIds.Count) || !bindings.Where(binding => binding != byte.MaxValue).Select(binding => saved.Scene.NativeActorObjectIds[binding]).SequenceEqual(boundSplit.Targets.Select(target => target.ObjectId)))
            {
                throw new InvalidDataException("Saved native SPLIT targets differ from saved party bodies; runtime follow slots are not substitutes.");
            }
        }
        if (saved.PartySplit is { } playerSplit && NativeFieldPartyBinding.TryGetObjectId(saved.Scene, 0, out int splitPlayerObjectId) && playerSplit.Targets.FirstOrDefault(target => target.ObjectId == splitPlayerObjectId) is PartyJoinTarget playerTarget && saved.Scene.NativePlayerPosition != new FieldFixedPosition(playerTarget.FixedX, playerTarget.FixedY, playerTarget.FixedZ))
        {
            throw new InvalidDataException("Saved native SPLIT current runtime leader differs from its raw player position.");
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
        ValidateNativeDoorSchedulers(saved, restored);
        ValidateNativeDoorRequestWaitCheckpoint(saved, restored);
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
        if (saved.FieldButtonInput is NativeFieldButtonInput fieldButtonInput)
        {
            services.SetFieldButtonInput(fieldButtonInput);
        }
        if (saved.InputConfiguration is NativeInputConfiguration inputConfiguration)
        {
            services.SetInputConfiguration(inputConfiguration);
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
        if (saved.GlobalSoundProcess is NativeGlobalSoundProcess soundProcess)
        {
            services.SetGlobalSoundProcess(soundProcess);
        }
        if (saved.EventRandom is NativeEventRandomState eventRandom)
        {
            services.SetEventRandom(eventRandom);
        }
        if (saved.WorldEntryMemory is NativeWorldEntryMemory worldEntryMemory)
        {
            services.SetWorldEntryMemory(worldEntryMemory);
        }
        if (saved.WeaponRecipes is NativeWeaponRecipes weaponRecipes)
        {
            services.SetWeaponRecipes(weaponRecipes);
        }
        if (saved.MovieSource is NativeMovieSource movieSource)
        {
            services.SetMovieSource(movieSource);
        }
        if (saved.DiscProcess is NativeDiscProcessState discProcess)
        {
            services.SetDiscProcess(discProcess);
        }
        if (saved.MenuRandom is NativeMenuRandomState menuRandom)
        {
            services.SetMenuRandom(menuRandom);
        }
        if (saved.ShopHistory is NativeShopHistory shopHistory)
        {
            services.SetShopHistory(shopHistory);
        }
        if (saved.FieldFade is NativeFieldFadeState fieldFade)
        {
            services.SetFieldFade(fieldFade);
        }
        if (saved.VibrationProcess is NativeVibrationProcessState vibration)
        {
            services.SetVibrationProcess(vibration);
        }
        if (saved.FieldBars is NativeFieldBarsState fieldBars)
        {
            services.SetFieldBars(fieldBars);
        }
        if (saved.FieldShake is NativeFieldShake fieldShake)
        {
            services.SetFieldShake(fieldShake);
        }
        if (saved.SalaryPresentation is NativeSalaryPresentation salaryPresentation)
        {
            services.SetSalaryPresentation(salaryPresentation);
        }
        if (saved.Economy is NativeEconomy savedEconomy)
        {
            services.SetEconomy(savedEconomy);
        }
        if (saved.SeeD is NativeSeeDState savedSeeD)
        {
            services.SetSeeD(savedSeeD);
        }
        if (saved.ShotAvailability is NativeShotAvailabilityState shots)
        {
            services.SetNativePartyMenuOwners(saved.CharacterVitals ?? throw new InvalidDataException("Shot owner lacks native saved characters."), saved.Economy ?? throw new InvalidDataException("Shot owner lacks native economy."), shots, saved.SalaryPresentation ?? throw new InvalidDataException("Shot owner lacks native salary process."), fieldLocation: saved.NativeFieldLocation);
        }
        partyMenu = saved.PartyMenu;
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
        preparedMovie = saved.PreparedMovie;
        sequence = saved.Sequence;
        defaultsStarted = saved.DefaultsStarted;
        transitionRequested = saved.TransitionRequested;
        IsUserControlEnabled = saved.UserControlEnabled;
        userControlSequenceDisabled = saved.UserControlSequenceDisabled.Value;
        backgroundFps = saved.BackgroundFps;
        nameEntity = saved.NameEntity;
        actors.Clear();
        preparedGlobalSounds.Clear();
        preparedFieldSounds.Clear();
        preparedWorldTransitions.Clear();
        actors.AddRange(saved.Actors);
        fibers.Clear();
        fibers.AddRange(restored);
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
        PublishEventMemory(saved.EventMemory);
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

    private sealed record FiberCheckpoint(int ObjectId, int ScriptId, int Priority, long Sequence, bool Initialization, int Position, bool Complete, bool Started, [property: JsonRequired] bool NativeResultFrameEntered, NativeScalarValue[] Stack, string[] StackExpressions, Dictionary<int, NativeScalarValue> Results, WaitReason WaitReason, long WaitUntil, long? RequestSequence, int DialogueChannel, int BackgroundId, int SoundResourceId = 0, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? NativeDoorSlot = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? NativeActorSlot = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NativeMoveOperation = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NativeMoveTarget = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeDoorRequestWaitState? NativeDoorRequestWait = null, [property: JsonRequired] bool NativeDoorDefaultFrame = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeScalarValue[]? NativeDoorSavedResults = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NativeShadeOperation = null);
    private sealed record BackgroundCheckpoint(int Id, int First, int Last, int Speed, bool Complete, int NativeVersion = 0, int Counter = 0, int Position = -1, bool Active = false, bool Loop = false);
    private sealed record Checkpoint(int Version, int FieldId, string ProgramHash, long Frame, double AccumulatedSeconds, int PreparedMovie, long Sequence, bool DefaultsStarted, bool TransitionRequested, bool UserControlEnabled, int BackgroundFps, int NameEntity, ActorState[] Actors, FiberCheckpoint[] Fibers, DialogueState[] Dialogues, int? CurrentDialogueChannel, Dictionary<int, int> BackgroundStates, BackgroundCheckpoint[] BackgroundAnimations, [property: JsonRequired] NativeEventMemory EventMemory, SceneEventState Scene, int AxisStart = 0, int AxisTarget = 0, int AxisDuration = 0, int AxisElapsed = 0, int InputAxisDirection = 0, bool MovieProgressExpected = false, MoviePlaybackState? CurrentMoviePlayback = null, FieldTransition? PendingArrival = null, EventFault? LastFault = null, PartyJoinState? PartyJoin = null, PartySplitState? PartySplit = null, NativeWorldControlState? WorldControls = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeExamTimerState? ExamTimer = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCharacterVitals? CharacterVitals = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCharacterAppearance? CharacterAppearance = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeCardInventory? CardInventory = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeSeeDState? SeeD = null, [property: JsonRequired] NativeMessageVariables? MessageVariables = null, [property: JsonRequired] NativeEconomy? Economy = null, [property: JsonRequired] NativeGlobalSoundProcess? GlobalSoundProcess = null, [property: JsonRequired] NativeSalaryPresentation? SalaryPresentation = null, [property: JsonRequired] NativeFieldShake? FieldShake = null, [property: JsonRequired] NativeInputConfiguration? InputConfiguration = null, [property: JsonRequired] NativeFieldButtonInput? FieldButtonInput = null, [property: JsonRequired] NativeShotAvailabilityState? ShotAvailability = null, [property: JsonRequired] NativePartyMenuState? PartyMenu = null, [property: JsonRequired] NativeEventRandomState? EventRandom = null, [property: JsonRequired] NativeFieldLocation? NativeFieldLocation = null, [property: JsonRequired] NativeVibrationProcessState? VibrationProcess = null, [property: JsonRequired] NativeFieldFadeState? FieldFade = null, [property: JsonRequired] NativeShopHistory? ShopHistory = null, [property: JsonRequired] NativeMenuRandomState? MenuRandom = null, [property: JsonRequired] NativeWeaponRecipes? WeaponRecipes = null, [property: JsonRequired] NativeWorldEntryMemory? WorldEntryMemory = null, [property: JsonRequired] NativeDiscProcessState? DiscProcess = null, [property: JsonRequired] NativeMovieSource? MovieSource = null, [property: JsonRequired] bool? UserControlSequenceDisabled = null, [property: JsonRequired] NativeFieldBarsState? FieldBars = null)
    {
        [JsonIgnore]
        public IReadOnlyDictionary<int, int> Variables => EventMemory.KnownValues;

        [JsonIgnore]
        public IReadOnlyDictionary<int, NativeMemoryValue> NativeVariableValues => EventMemory.UncertainValues;
    }
}
