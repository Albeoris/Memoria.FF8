using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
public sealed record NativeBattleRinoa87IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress)
{
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(356));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(358));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(256));
    public void Validate() => NativeBattleRinoa87Io.Validate(this);
}
public sealed record NativeBattleRinoa87IoAdvance(NativeBattleModulePoolTask Task, NativeBattleRinoa87IoState State, NativeBattleRinoa87IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleRinoa87IoRequest(ushort FileId, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleRinoa87IoQueued(NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);
public sealed record NativeBattleRinoa87IoCompletion(NativeBattleRinoa87IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleRinoa87IoRelease(NativeBattleRinoa87IoState State, bool Released);

/// <summary>773D40/773E30's reached wait and two TIM records, using the original shared asynchronous file queue.</summary>
public static class NativeBattleRinoa87Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x155B190, 80, 0x773E30, 0x774030, 0x774090, 0x7740A0, 0x19CF018, true, ExternalDirectoryPresets: false);

    public static NativeBattleRinoa87IoState Initialize773D40(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(276), 0x19EB018);
        return new(control.ToImmutableArray(), state.Script, state.CursorAddress);
    }

    internal static void Validate(NativeBattleRinoa87IoState state) => NativeBattleProfiledIoCore.Validate(Profile, Core(state));

    public static NativeBattleRinoa87IoRelease ReleaseWait774FD0(NativeBattleRinoa87IoState state)
    {
        var released = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(released.State), released.Released);
    }

    public static NativeBattleRinoa87IoAdvance Advance773E30(NativeBattleModulePoolTask task, NativeBattleRinoa87IoState state)
    {
        state.Validate();
        int cursor = checked((int)(state.CursorAddress - Profile.ScriptAddress));
        ushort opcode = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor));
        if (opcode is not (0 or 1 or 3 or 255))
        {
            throw new NotSupportedException("Native Rinoa87 requires its reached wait, data, TIM and end records; other script consumers are unsupported.");
        }
        NativeBattleProfiledIoAdvance advanced = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleRinoa87IoRequest? request = advanced.Request is { } file ? new(file.FileId, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advanced.Task, Public(advanced.State), request, advanced.ReadyToComplete);
    }

    public static NativeBattleRinoa87IoQueued CopyAndQueueFile(NativeBattleRinoa87IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file, Action<uint, ImmutableArray<byte>>? writeExternal = null)
    {
        state.Validate();
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode is not (1 or 3))
        {
            throw new NotSupportedException("Native Rinoa87's supplied IO source requires its actual TIM or external-data operation.");
        }
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file, writeExternal is null ? null : (request, data) => writeExternal(request.DestinationAddress, data));
        return new(queued.Memory, queued.FileIo);
    }

    public static NativeBattleRinoa87IoCompletion Complete774030(NativeBattleRinoa87IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        NativeBattleProfiledIoRequest request = NativeBattleProfiledIoCore.CurrentFile(Profile, Core(state));
        if (request.Opcode is not (1 or 3))
        {
            throw new NotSupportedException("Native Rinoa87 completion requires its reached TIM or data operation.");
        }
        var complete = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(complete.State), complete.Memory);
    }

    private static NativeBattleProfiledIoState Core(NativeBattleRinoa87IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleRinoa87IoState Public(NativeBattleProfiledIoState state) => new(state.Control, state.Script, state.CursorAddress);
}
