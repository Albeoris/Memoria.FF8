using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Captures logical execution without native resources, receipt histories or replaying completed initialization.</summary>
    public GameplaySaveFieldScripts CaptureGameplaySaveScripts()
    {
        if (program is null || !defaultsStarted || transitionRequested || LastFault is not null || dialogues.Count != 0 || services.IsMoviePlaying || CurrentMoviePlayback?.IsPlaying == true || movieProgressExpected || partyJoin is not null || partySplit is not null || pendingArrival is not null)
        {
            throw new NotSupportedException("Gameplay script capture requires completed stable field initialization with no transition or modal operation.");
        }
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Gameplay script capture requires its actual callback timebase.");
        clock.Validate(Frame);
        foreach (Fiber fiber in fibers)
        {
            ValidateGameplayAmbientWait(fiber);
        }
        _ = CaptureGameplayScriptMenu();
        ValidateGameplayMovement(fibers);
        ValidateNativeJumps(actors, Scene, fibers);
        GameplaySaveScriptFiber[] saved = fibers.Select(CaptureGameplaySaveFiber).ToArray();
        GameplaySaveScriptOperands[] operands = actors.Where(actor => actor.NativeOperandMemory is not null).Select(actor => new GameplaySaveScriptOperands(actor.ObjectId, actor.NativeOperandMemory!.Words.Select(word => word is null ? (int?)null : GameplayScriptInteger(word)).ToArray(), actor.NativeOperandMemory.SavedDepths.ToArray(), actor.NativeOperandMemory.LiveDepth, actor.NativeActorWord398)).ToArray();
        GameplaySaveDoorSchedule[] doors = actors.Where(actor => actor.NativeDoorScheduler is not null).Select(actor => new GameplaySaveDoorSchedule(actor.ObjectId, actor.NativeDoorScheduler!.CurrentSlot, actor.NativeDoorScheduler.CurrentSequence, actor.NativeDoorScheduler.DefaultSequence, actor.NativeDoorScheduler.InitializationSequence, actor.NativeDoorScheduler.RetiredOperation)).ToArray();
        GameplaySaveEventCell[] locals = EventMemory.Cells.Where(pair => pair.Key >= 1024 && (long)pair.Key + pair.Value.Width <= 2304).OrderBy(pair => pair.Key).Select(pair => new GameplaySaveEventCell(pair.Key, pair.Value.Width, GameplayScriptInteger(pair.Value.Value))).ToArray();
        if (EventMemory.Cells.Any(cell => cell.Key < 1024 && (long)cell.Key + cell.Value.Width > 1024 || cell.Key < 2304 && (long)cell.Key + cell.Value.Width > 2304))
        {
            throw new NotSupportedException("Gameplay script capture cannot split a cross-owner local event cell.");
        }
        GameplaySaveFieldScripts result = new(1, FieldId, programHash, Frame, accumulatedSeconds, sequence, userControlEnabled, userControlSequenceDisabled, backgroundFps, nameEntity, new(clock.OrdinaryCallbacks, clock.MovieCallbacks, clock.MenuCallbacks, clock.FieldLoaderCallbacks, clock.BattleCallbacks, clock.InitializationTransactions ?? throw new NotSupportedException("Gameplay scripts require known completed initialization."), clock.RandomBattleCallbacks, clock.MenuBattleCallbacks), saved, operands, doors, locals) { PreparedMovie = preparedMovie >= 0 ? preparedMovie : null, InputAxis = new(axisStart, axisTarget, axisDuration, axisElapsed, InputAxisDirection) };
        result.Validate();
        return result;
    }

    private static int GameplayScriptInteger(NativeScalarValue value)
    {
        if (!value.IsExact || value.ActorInfoOrigin is not null || value.Origin is not null)
        {
            throw new NotSupportedException("Gameplay script continuation requires exact logical scalar operands without resource or physical-read provenance.");
        }
        return value.RequireExact();
    }

    private GameplaySaveScriptFiber CaptureGameplaySaveFiber(Fiber fiber)
    {
        ValidateGameplayAnimationWait(fiber);
        ValidateGameplayFaceWait(fiber);
        ValidateGameplayBackgroundAnimationWait(fiber);
        ValidateGameplayTurnWait(fiber);
        GameplaySaveScriptWait wait = fiber.WaitReason switch
        {
            WaitReason.None => GameplaySaveScriptWait.None,
            WaitReason.Frames => GameplaySaveScriptWait.Frames,
            WaitReason.RequestStart => GameplaySaveScriptWait.RequestStart,
            WaitReason.RequestEnd => GameplaySaveScriptWait.RequestEnd,
            WaitReason.NativeHalt => GameplaySaveScriptWait.Halt,
            WaitReason.Animation => GameplaySaveScriptWait.Animation,
            WaitReason.NativeAnimationBlocking => GameplaySaveScriptWait.NativeAnimationBlocking,
            WaitReason.NativeAnimationSync => GameplaySaveScriptWait.NativeAnimationSync,
            WaitReason.NativeBackgroundShade => GameplaySaveScriptWait.BackgroundShade,
            WaitReason.BackgroundScroll => GameplaySaveScriptWait.BackgroundScroll,
            WaitReason.NativePartyMenu => GameplaySaveScriptWait.ScriptMenu,
            WaitReason.FaceDirection => GameplaySaveScriptWait.FaceDirection,
            WaitReason.NativeFaceSync => GameplaySaveScriptWait.NativeFaceSync,
            WaitReason.BackgroundAnimation => GameplaySaveScriptWait.BackgroundAnimation,
            WaitReason.Rotation => GameplaySaveScriptWait.Rotation,
            WaitReason.NativeTurnCompletion => GameplaySaveScriptWait.NativeTurnCompletion,
            WaitReason.NativeScriptMovement => GameplaySaveScriptWait.NativeScriptMovement,
            WaitReason.NativeJump => GameplaySaveScriptWait.NativeJump,
            WaitReason.NativeMovementFlush => GameplaySaveScriptWait.NativeMovementFlush,
            _ => throw new NotSupportedException($"Gameplay script continuation does not yet represent {fiber.WaitReason} waits.")
        };
        if (fiber.NativeGlobalJumps.Count != 0 || fiber.NativeDoorRequestWait is not null || fiber.NativePartyRequestPointer is not null || fiber.NativeDialogueConsumer is not null || fiber.DialogueChannel != -1 || fiber.SoundResourceId != 0)
        {
            throw new NotSupportedException("Gameplay script continuation requires a represented logical scheduler without live external request owners.");
        }
        return new(fiber.ObjectId, fiber.Script.Id, fiber.Priority, fiber.Sequence, fiber.Initialization, fiber.Position, fiber.Complete, fiber.Started, fiber.NativeResultFrameEntered, fiber.Stack.Capture().Select(GameplayScriptInteger).ToArray(), fiber.Results.ToDictionary(pair => pair.Key, pair => GameplayScriptInteger(pair.Value)), wait, fiber.WaitUntil, fiber.Request?.Sequence, fiber.NativeDoorSlot, fiber.NativeActorSlot, fiber.NativeDoorDefaultFrame, fiber.NativeDoorSavedResults?.Select(GameplayScriptInteger).ToArray()) { ShadeOperation = fiber.NativeShadeOperation, MovementIssue = CaptureGameplayMovementIssue(fiber) };
    }

    /// <summary>Installs only script execution owners. The caller must separately reconstruct current scene/actor/resource owners before input.</summary>
    public void RestoreGameplaySaveScripts(GameplaySaveFieldScripts saved)
    {
        RestoreGameplaySaveScripts(saved, false);
    }

    private void RestoreGameplaySaveScripts(GameplaySaveFieldScripts saved, bool hasScriptMenu)
    {
        ArgumentNullException.ThrowIfNull(saved);
        saved.Validate();
        if (!hasScriptMenu && saved.Fibers.Any(fiber => fiber.Wait == GameplaySaveScriptWait.ScriptMenu))
        {
            throw new InvalidDataException("Script-menu waits require their complete logical menu continuation.");
        }
        if (program is null || saved.FieldId != FieldId || saved.ProgramSha256 != programHash || transitionRequested || dialogues.Count != 0 || partyMenu is not null)
        {
            throw new InvalidOperationException("Gameplay script continuation requires the same installed program and a nonmodal destination.");
        }
        List<Fiber> restored = [];
        foreach (GameplaySaveScriptFiber source in saved.Fibers)
        {
            if ((uint)source.ObjectId >= program.Objects.Count)
            {
                throw new InvalidDataException("Gameplay script object is absent from the installed program.");
            }
            EventScript script = program.Objects[source.ObjectId].Scripts.SingleOrDefault(script => script.Id == source.ScriptId) ?? throw new InvalidDataException("Gameplay script is absent from the installed object.");
            if (!script.ContainsPosition(source.Position, source.Complete))
            {
                throw new InvalidDataException("Gameplay script continuation has an invalid instruction pointer.");
            }
            Fiber fiber = new(source.ObjectId, script, source.Priority, source.Sequence, source.Initialization)
            {
                Position = source.Position,
                BackgroundId = program.Objects[source.ObjectId].BackgroundModuleIndex ?? 0,
                NativeShadeOperation = source.ShadeOperation,
                Complete = source.Complete,
                Started = source.Started,
                NativeResultFrameEntered = source.ResultFrameEntered,
                WaitReason = source.Wait switch { GameplaySaveScriptWait.None => WaitReason.None, GameplaySaveScriptWait.Frames => WaitReason.Frames, GameplaySaveScriptWait.RequestStart => WaitReason.RequestStart, GameplaySaveScriptWait.RequestEnd => WaitReason.RequestEnd, GameplaySaveScriptWait.Halt => WaitReason.NativeHalt, GameplaySaveScriptWait.Animation => WaitReason.Animation, GameplaySaveScriptWait.NativeAnimationBlocking => WaitReason.NativeAnimationBlocking, GameplaySaveScriptWait.NativeAnimationSync => WaitReason.NativeAnimationSync, GameplaySaveScriptWait.BackgroundShade => WaitReason.NativeBackgroundShade, GameplaySaveScriptWait.BackgroundScroll => WaitReason.BackgroundScroll, GameplaySaveScriptWait.ScriptMenu => WaitReason.NativePartyMenu, GameplaySaveScriptWait.FaceDirection => WaitReason.FaceDirection, GameplaySaveScriptWait.NativeFaceSync => WaitReason.NativeFaceSync, GameplaySaveScriptWait.BackgroundAnimation => WaitReason.BackgroundAnimation, GameplaySaveScriptWait.Rotation => WaitReason.Rotation, GameplaySaveScriptWait.NativeTurnCompletion => WaitReason.NativeTurnCompletion, GameplaySaveScriptWait.NativeScriptMovement => WaitReason.NativeScriptMovement, GameplaySaveScriptWait.NativeMovementFlush => WaitReason.NativeMovementFlush, GameplaySaveScriptWait.NativeJump => WaitReason.NativeJump, _ => throw new InvalidDataException("Unknown gameplay script wait.") },
                WaitUntil = source.WaitUntil,
                NativeDoorSlot = source.DoorSlot,
                NativeActorSlot = source.ActorSlot,
                NativeDoorDefaultFrame = source.DefaultResultFrame,
                NativeDoorSavedResults = source.SavedResults?.Select(NativeScalarValue.Exact).ToArray()
            };
            RestoreGameplayMovementIssue(fiber, source.MovementIssue);
            foreach (int value in source.Stack.Reverse())
            {
                fiber.Stack.Push(value);
                fiber.StackExpressions.Push("saved operand");
            }
            foreach ((int key, int value) in source.Results)
            {
                fiber.Results.Add(key, NativeScalarValue.Exact(value));
            }
            ValidateGameplayAnimationWait(fiber);
            ValidateGameplayFaceWait(fiber);
            ValidateGameplayBackgroundAnimationWait(fiber);
            ValidateGameplayTurnWait(fiber);
            restored.Add(fiber);
        }
        Dictionary<long, Fiber> bySequence = restored.ToDictionary(fiber => fiber.Sequence);
        for (int index = 0; index < restored.Count; index++)
        {
            restored[index].Request = saved.Fibers[index].RequestSequence is long request ? bySequence[request] : null;
        }
        foreach (Fiber fiber in restored)
        {
            ValidateGameplayAmbientWait(fiber);
        }
        Dictionary<int, NativeActorOperandMemory> operands = saved.Operands.ToDictionary(item => item.ObjectId, item => new NativeActorOperandMemory(1, item.Words.Select(word => word is int value ? NativeScalarValue.Exact(value) : null).ToArray(), item.SavedDepths.ToArray(), item.LiveDepth));
        Dictionary<int, NativeDoorSchedulerState> doors = saved.Doors.ToDictionary(item => item.ObjectId, item => new NativeDoorSchedulerState(1, item.CurrentSlot, item.CurrentSequence, item.DefaultSequence, item.InitializationSequence, item.RetiredOperation));
        if (operands.Keys.Concat(doors.Keys).Any(id => (uint)id >= actors.Count))
        {
            throw new InvalidDataException("Gameplay script physical frame references an absent actor.");
        }
        Dictionary<int, NativeMemoryValue> cells = EventMemory.Cells.Where(pair => pair.Key >= 2304 || (long)pair.Key + pair.Value.Width <= 1024).ToDictionary();
        foreach (GameplaySaveEventCell local in saved.Locals)
        {
            cells.Add(local.Address, new(local.Width, NativeScalarValue.Exact(local.Value)));
        }
        NativeEventMemory memory = new(NativeEventMemory.CurrentVersion, cells);
        memory.Validate();
        NativeFieldCallbackClock clock = new(1, saved.Clock.Ordinary, saved.Clock.Movie, saved.Clock.Menu, saved.Clock.Loader, saved.Clock.Battle, saved.Clock.Initialization, saved.Clock.RandomBattle, saved.Clock.MenuBattle);
        clock.Validate(saved.Frame);
        PublishEventMemory(memory);
        foreach ((int id, NativeActorOperandMemory values) in operands)
        {
            actors[id].NativeOperandMemory = values;
            actors[id].NativeActorWord398 = saved.Operands.Single(item => item.ObjectId == id).ActorWord398;
        }
        foreach ((int id, NativeDoorSchedulerState values) in doors)
        {
            actors[id].NativeDoorScheduler = values;
        }
        foreach (Fiber fiber in restored)
        {
            BindNativeOperandMemory(fiber);
        }
        ValidateGameplayMovement(restored);
        ValidateNativeJumps(actors, Scene, restored);
        fibers.Clear();
        fibers.AddRange(restored);
        inputPollingFibers.Clear();
        inputPollingObserved = false;
        Frame = saved.Frame;
        accumulatedSeconds = saved.FractionalSeconds;
        sequence = saved.Sequence;
        userControlEnabled = saved.UserControl;
        userControlSequenceDisabled = saved.SequenceControlDisabled;
        backgroundFps = saved.BackgroundFps;
        nameEntity = saved.NameEntity;
        axisStart = saved.InputAxis?.Start ?? 0;
        axisTarget = saved.InputAxis?.Target ?? 0;
        axisDuration = saved.InputAxis?.Duration ?? 0;
        axisElapsed = saved.InputAxis?.Elapsed ?? 0;
        InputAxisDirection = saved.InputAxis?.Direction ?? 0;
        preparedMovie = saved.PreparedMovie ?? -1;
        CurrentMoviePlayback = null;
        movieProgressExpected = false;
        Scene.FieldCallbackClock = clock;
        defaultsStarted = true;
    }
}
