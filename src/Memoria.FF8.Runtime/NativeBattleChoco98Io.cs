using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco98IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress, [property: JsonRequired] NativeBattleChoco97IoBanks Banks)
{
    public const uint ScriptAddress = 0x152B2A8;
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x164));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
    [JsonIgnore] public short RangeCount => BinaryPrimitives.ReadInt16LittleEndian(Control.AsSpan().Slice(0x168));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(0x100));
    public void Validate()
    {
        NativeBattleChoco98Io.ValidateState(this);
    }
}
public sealed record NativeBattleChoco98IoInitialization(NativeBattleChoco98IoState State, uint NextAddress);
public sealed record NativeBattleChoco98IoRelease(NativeBattleChoco98IoState State, bool Released);
public sealed record NativeBattleChoco98IoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleChoco98IoAdvance(NativeBattleModulePoolTask Task, NativeBattleChoco98IoState State, NativeBattleChoco98IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleChoco98IoCompletion(NativeBattleChoco98IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleChoco98IoQueued(NativeBattleChoco98IoState State, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

public static class NativeBattleChoco98Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x152B2A8, 0x120, 0x721DF0, 0x721FF0, 0x722050, 0x722060, 0x19DB018, true);

    internal static void ValidateState(NativeBattleChoco98IoState state)
    {
        NativeBattleProfiledIoCore.Validate(Profile, Core(state));
        state.Banks.Validate();
    }

    public static NativeBattleChoco98IoInitialization Initialize721CF0(ImmutableArray<byte> retainedScript, uint nextAddress, NativeBattleChoco97IoBanks banks)
    {
        banks.Validate();
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x12C), 0x19E187C);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x130), 0x19E5584);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x134), 0x19E8E70);
        state = state with { Control = control.ToImmutableArray() };
        return new(Public(state, banks), checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));
    }
    public static NativeBattleChoco98IoRelease ReleaseWait(NativeBattleChoco98IoState state)
    {
        state.Validate();
        var release = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(release.State, state.Banks), release.Released);
    }
    public static NativeBattleChoco98IoAdvance Advance721DF0(NativeBattleModulePoolTask task, NativeBattleChoco98IoState state)
    {
        state.Validate();
        NativeBattleProfiledIoAdvance advance = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleChoco98IoRequest? request = advance.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advance.Task, Public(advance.State, state.Banks), request, advance.ReadyToComplete);
    }
    public static NativeBattleChoco98IoQueued CopyAndQueueFile(NativeBattleChoco98IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleChoco97IoBanks banks = state.Banks;
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file, (request, data) => banks = banks.Write(request.DestinationAddress, data));
        return new(state with { Banks = banks }, queued.Memory, queued.FileIo);
    }
    public static NativeBattleChoco98IoCompletion Complete(NativeBattleChoco98IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        state.Validate();
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(completed.State, state.Banks), completed.Memory);
    }
    private static NativeBattleProfiledIoState Core(NativeBattleChoco98IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleChoco98IoState Public(NativeBattleProfiledIoState state, NativeBattleChoco97IoBanks banks) => new(state.Control, state.Script, state.CursorAddress, banks);
}
