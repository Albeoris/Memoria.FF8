using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
public sealed record NativeBattleRinoa88IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress)
{
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(356));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(358));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(256));
    public void Validate() => NativeBattleRinoa88Io.Validate(this);
}
public sealed record NativeBattleRinoa88IoAdvance(NativeBattleModulePoolTask Task, NativeBattleRinoa88IoState State, NativeBattleRinoa88IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleRinoa88IoRequest(ushort FileId, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleRinoa88IoQueued(NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
public sealed record NativeBattleRinoa88IoCompletion(NativeBattleRinoa88IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleRinoa88IoRelease(NativeBattleRinoa88IoState State, bool Released);

/// <summary>7689C0/768AC0's reached wait, four data and two TIM records, using the original shared asynchronous file queue.</summary>
public static class NativeBattleRinoa88Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x154F898, 128, 0x768AC0, 0x768CC0, 0x768D20, 0x768D30, 0x19ED018, true, DataCallback4: 0x768D40, ExternalDirectoryPresets: false);

    public static NativeBattleRinoa88IoState Initialize7689C0(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(296), 0x19F3A8C);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(324), 0x19EF0BC);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(328), 0x19F1B5C);
        return new(control.ToImmutableArray(), state.Script, state.CursorAddress);
    }

    internal static void Validate(NativeBattleRinoa88IoState state) => NativeBattleProfiledIoCore.Validate(Profile, Core(state));

    public static NativeBattleRinoa88IoRelease ReleaseWait769D10(NativeBattleRinoa88IoState state)
    {
        var released = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(released.State), released.Released);
    }

    public static NativeBattleRinoa88IoAdvance Advance768AC0(NativeBattleModulePoolTask task, NativeBattleRinoa88IoState state)
    {
        state.Validate();
        int cursor = checked((int)(state.CursorAddress - Profile.ScriptAddress));
        ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor));
        if (opcode is not (0 or 1 or 2 or 3 or 4 or 255))
        {
            throw new NotSupportedException("Native Rinoa88 requires its reached wait, data, TIM and end records; other script consumers are unsupported.");
        }
        NativeBattleProfiledIoAdvance advanced = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleRinoa88IoRequest? request = advanced.Request is { } file ? new(file.FileId, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advanced.Task, Public(advanced.State), request, advanced.ReadyToComplete);
    }

    public static NativeBattleRinoa88IoQueued CopyAndQueueFile(NativeBattleRinoa88IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file, Action<uint, ImmutableArray<byte>>? writeExternal = null)
    {
        state.Validate();
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode is not (1 or 2 or 3 or 4))
        {
            throw new NotSupportedException("Native Rinoa88's supplied IO source requires its actual TIM or external-data operation.");
        }
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file, writeExternal is null ? null : (request, data) => writeExternal(request.DestinationAddress, data));
        return new(queued.Memory, queued.FileIo);
    }

    public static NativeBattleRinoa88IoCompletion Complete768CC0(NativeBattleRinoa88IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode is not (1 or 2 or 3 or 4))
        {
            throw new NotSupportedException("Native Rinoa88 completion requires its reached TIM or data operation.");
        }
        var complete = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(complete.State), complete.Memory);
    }

    private static NativeBattleProfiledIoState Core(NativeBattleRinoa88IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleRinoa88IoState Public(NativeBattleProfiledIoState state) => new(state.Control, state.Script, state.CursorAddress);
}
