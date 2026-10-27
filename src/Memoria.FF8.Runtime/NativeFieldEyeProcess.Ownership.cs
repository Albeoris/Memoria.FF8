namespace Memoria.FF8.Runtime;

public sealed partial record NativeFieldEyeProcess
{
    /// <summary>Detaches the field process at an explicit checkpoint boundary.</summary>
    public NativeFieldEyeProcess CaptureSnapshot() => this with { Memory = NativeBattleStorageSnapshots.Capture(Memory) };

    /// <summary>Creates field execution ownership from a detached process, or retains its existing working owner.</summary>
    public NativeFieldEyeProcess RestoreOwned() => this with { Memory = EnsureOwnedMemory(Memory) };

    internal static NativeBattleRetainedMemory EnsureOwnedMemory(NativeBattleRetainedMemory memory)
    {
        bool frozen = memory.FileStaging?.IsSnapshot == true || memory.EffectMemory?.Textures is INativeBattleStorageNode { IsSnapshot: true };
        return frozen ? NativeBattleStorageSnapshots.Restore(memory) : memory;
    }
}
