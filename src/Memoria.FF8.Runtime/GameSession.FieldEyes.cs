namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private NativeBattleRetainedMemory? liveFieldMemory;
    private NativeBattleRetainedMemory? preparedFieldMemory;

    /// <summary>The active field's physical owner, or the detached retained handoff when no field is attached.</summary>
    public NativeBattleRetainedMemory? CurrentNativeBattleMemory => liveFieldMemory ?? State.NativeBattleMemory;

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
        if (liveFieldMemory is null)
        {
            return State.NativeBattleMemory;
        }
        if (preparingSnapshot && preparedFieldMemory is { } prepared)
        {
            return prepared;
        }
        NativeBattleRetainedMemory captured = NativeBattleStorageSnapshots.Capture(liveFieldMemory);
        if (preparingSnapshot)
        {
            preparedFieldMemory = captured;
        }
        return captured;
    }

    private GameState CaptureLiveFieldMemory(GameState source) => liveFieldMemory is null ? source : source with { NativeBattleMemory = CaptureNativeFieldMemory() };

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
