namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private NativeBattleRetainedMemory? liveFieldMemory;
    private NativeBattleRetainedMemory? preparedFieldMemory;

    /// <summary>The active field's physical owner, or the detached retained handoff when no field is attached.</summary>
    public NativeBattleRetainedMemory? CurrentNativeBattleMemory => liveFieldMemory ?? State.NativeBattleMemory;

    /// <summary>A field keeps its shared graphics memory through its movie and the callback that requests its successor.</summary>
    public bool OwnsNativeFieldMemory(int fieldId)
    {
        if (HasActiveBattle || !HasActiveFieldScene)
        {
            return false;
        }
        // INF publishes the accepted request during player movement, before the outgoing model draw.
        // The destination loader consumes that request before it starts its own field callbacks.
        if (liveFieldMemory is not null && State.Scene == SceneKind.Field && State.PreviousFieldId is int departing && State.FieldLoadContext?.Origin == NativeFieldLoadOrigin.FieldRequest && State.FieldRequestPacket is { Kind: 1, FieldId: ushort destination } && State.FieldArrival?.FieldId == destination && State.FieldId == destination)
        {
            return departing == fieldId;
        }
        return State.FieldId == fieldId;
    }

    public NativeBattleRetainedMemory ActivateNativeFieldMemory()
    {
        if (liveBattle is not null)
        {
            throw new InvalidOperationException("Field activation cannot acquire an active battle's retained memory.");
        }
        return liveFieldMemory ??= NativeFieldEyeProcess.EnsureOwnedMemory(State.NativeBattleMemory ?? throw new NotSupportedException("Native field activation requires its retained process memory."));
    }

    /// <summary>Captures the current field owner for persistence or battle entry without changing the live owner.</summary>
    public NativeBattleRetainedMemory? CaptureNativeFieldMemory()
    {
        NativeBattleRetainedMemory? memory = liveFieldMemory ?? State.NativeBattleMemory;
        if (memory is null)
        {
            return null;
        }
        if (preparingSnapshot && preparedFieldMemory is { } prepared)
        {
            return prepared;
        }
        NativeBattleRetainedMemory captured = NativeBattleStorageSnapshots.Capture(memory);
        if (preparingSnapshot)
        {
            preparedFieldMemory = captured;
        }
        return captured;
    }

    private GameState CaptureLiveFieldMemory(GameState source)
    {
        if (liveFieldMemory is not null || ReferenceEquals(source.NativeBattleMemory, State.NativeBattleMemory))
        {
            return source with { NativeBattleMemory = CaptureNativeFieldMemory() };
        }
        return source.NativeBattleMemory is { } memory ? source with { NativeBattleMemory = NativeBattleStorageSnapshots.Capture(memory) } : source;
    }

    public void SetNativeFieldEyeOwners(NativeBattleRetainedMemory memory, NativeCrtRandomState? crtRandom)
    {
        memory.Validate();
        NativeBattleRetainedMemory owned = NativeFieldEyeProcess.EnsureOwnedMemory(memory);
        if (ReferenceEquals(liveFieldMemory, owned) && State.CrtRandom == crtRandom)
        {
            return;
        }
        liveFieldMemory = owned;
        Change(State with { CrtRandom = crtRandom });
    }
}
