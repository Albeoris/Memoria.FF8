using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One caller-supplied physical address span preserves aliases and overlapping native file destinations.</summary>
public sealed record NativeBattleChoco97IoBanks([property: JsonRequired] uint BaseAddress, [property: JsonRequired] int Length, [property: JsonRequired] NativeBattleEffectArena Bytes)
{
    public NativeBattleChoco97IoBanks Write(uint address, ImmutableArray<byte> data)
    {
        int offset = Offset(address, data.Length);
        return this with { Bytes = Bytes.Write(offset, data.AsSpan()) };
    }
    public byte[] Read(uint address, int length) => Bytes.Read(Offset(address, length), length);
    public void Validate()
    {
        Bytes.Validate();
        if (Length <= 0 || Length > NativeBattleEffectArena.Length || (ulong)BaseAddress + (uint)Length > uint.MaxValue || Bytes.ExceedsExtent(Length))
        {
            throw new InvalidDataException("Native Choco IO external bytes require their supplied bounded absolute address span.");
        }
    }
    private int Offset(uint address, int length)
    {
        Validate();
        long offset = (long)address - BaseAddress;
        if (length < 0 || offset < 0 || offset > Length - length)
        {
            throw new NotSupportedException("Native Choco file destination leaves its explicit external physical owner.");
        }
        return (int)offset;
    }
}

public sealed record NativeBattleChoco97IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress, [property: JsonRequired] NativeBattleChoco97IoBanks Banks)
{
    public const uint ScriptAddress = 0x152B930;
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x164));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
    [JsonIgnore] public short RangeCount => BinaryPrimitives.ReadInt16LittleEndian(Control.AsSpan().Slice(0x168));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(0x100));
    public void Validate()
    {
        NativeBattleProfiledIoCore.Validate(NativeBattleIoProfile.Choco97, new(Control, Script, CursorAddress));
        Banks.Validate();
    }
}
public sealed record NativeBattleChoco97IoInitialization(NativeBattleChoco97IoState State, uint NextAddress);
public sealed record NativeBattleChoco97IoRelease(NativeBattleChoco97IoState State, bool Released);
public sealed record NativeBattleChoco97IoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleChoco97IoAdvance(NativeBattleModulePoolTask Task, NativeBattleChoco97IoState State, NativeBattleChoco97IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleChoco97IoCompletion(NativeBattleChoco97IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleChoco97IoQueued(NativeBattleChoco97IoState State, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

public static class NativeBattleChoco97Io
{
    public static NativeBattleChoco97IoInitialization Initialize729EF0(ImmutableArray<byte> retainedScript, uint nextAddress, NativeBattleChoco97IoBanks banks)
    {
        banks.Validate();
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(NativeBattleIoProfile.Choco97, retainedScript, nextAddress);
        return new(Public(state, banks), checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));
    }
    public static NativeBattleChoco97IoRelease ReleaseWait730590(NativeBattleChoco97IoState state)
    {
        state.Validate();
        var release = NativeBattleProfiledIoCore.ReleaseWait(NativeBattleIoProfile.Choco97, Core(state));
        return new(Public(release.State, state.Banks), release.Released);
    }
    public static NativeBattleChoco97IoAdvance Advance729FF0(NativeBattleModulePoolTask task, NativeBattleChoco97IoState state)
    {
        state.Validate();
        NativeBattleProfiledIoAdvance advance = NativeBattleProfiledIoCore.Advance(NativeBattleIoProfile.Choco97, task, Core(state));
        NativeBattleChoco97IoRequest? request = advance.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advance.Task, Public(advance.State, state.Banks), request, advance.ReadyToComplete);
    }
    public static NativeBattleChoco97IoQueued CopyAndQueueFile(NativeBattleChoco97IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleChoco97IoBanks banks = state.Banks;
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(NativeBattleIoProfile.Choco97, Core(state), memory, fileIo, file, (request, data) => banks = banks.Write(request.DestinationAddress, data));
        return new(state with { Banks = banks }, queued.Memory, queued.FileIo);
    }
    public static NativeBattleChoco97IoCompletion Complete(NativeBattleChoco97IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        state.Validate();
        var completed = NativeBattleProfiledIoCore.Complete(NativeBattleIoProfile.Choco97, Core(state), memory, callback);
        return new(Public(completed.State, state.Banks), completed.Memory);
    }
    private static NativeBattleProfiledIoState Core(NativeBattleChoco97IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleChoco97IoState Public(NativeBattleProfiledIoState state, NativeBattleChoco97IoBanks banks) => new(state.Control, state.Script, state.CursorAddress, banks);
}
