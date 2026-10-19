using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
public sealed record NativeBattleRinoa86IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress)
{
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(356));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(358));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(256));
    public void Validate() => NativeBattleRinoa86Io.Validate(this);
}
public sealed record NativeBattleRinoa86IoAdvance(NativeBattleModulePoolTask Task, NativeBattleRinoa86IoState State, NativeBattleRinoa86IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleRinoa86IoRequest(ushort FileId, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleRinoa86IoQueued(NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
public sealed record NativeBattleRinoa86IoCompletion(NativeBattleRinoa86IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleRinoa86IoRelease(NativeBattleRinoa86IoState State, bool Released);

/// <summary>77F680/77F780's reached wait and two TIM records, using the original shared asynchronous file queue.</summary>
public static class NativeBattleRinoa86Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x1565EB0, 64, 0x77F780, 0x77F980, 0, 0, 0x19CF018, true, ExternalDirectoryPresets: false);

    public static NativeBattleRinoa86IoState Initialize77F680(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(276), 0x19EB018);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(300), 0x19D2560);
        return new(control.ToImmutableArray(), state.Script, state.CursorAddress);
    }

    internal static void Validate(NativeBattleRinoa86IoState state) => NativeBattleProfiledIoCore.Validate(Profile, Core(state));

    public static NativeBattleRinoa86IoRelease ReleaseWait77FDB0(NativeBattleRinoa86IoState state)
    {
        var released = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(released.State), released.Released);
    }

    public static NativeBattleRinoa86IoAdvance Advance77F780(NativeBattleModulePoolTask task, NativeBattleRinoa86IoState state)
    {
        state.Validate();
        int cursor = checked((int)(state.CursorAddress - Profile.ScriptAddress));
        ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor));
        if (opcode is not (0 or 1 or 255))
        {
            throw new NotSupportedException("Native Rinoa86 requires its reached wait, TIM and end records; external data and stream consumers are unsupported.");
        }
        NativeBattleProfiledIoAdvance advanced = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleRinoa86IoRequest? request = advanced.Request is { } file ? new(file.FileId, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advanced.Task, Public(advanced.State), request, advanced.ReadyToComplete);
    }

    public static NativeBattleRinoa86IoQueued CopyAndQueueFile(NativeBattleRinoa86IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode != 1)
        {
            throw new NotSupportedException("Native Rinoa86's supplied IO source requires its original TIM operation.");
        }
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file);
        return new(queued.Memory, queued.FileIo);
    }

    public static NativeBattleRinoa86IoCompletion Complete77F980(NativeBattleRinoa86IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode != 1)
        {
            throw new NotSupportedException("Native Rinoa86 completion requires its reached TIM operation.");
        }
        var complete = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(complete.State), complete.Memory);
    }

    private static NativeBattleProfiledIoState Core(NativeBattleRinoa86IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleRinoa86IoState Public(NativeBattleProfiledIoState state) => new(state.Control, state.Script, state.CursorAddress);
}
