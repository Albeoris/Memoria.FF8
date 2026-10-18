using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
public sealed record NativeBattleChoco100IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress, [property: JsonRequired] NativeBattleChoco97IoBanks Banks)
{
    public const uint ScriptAddress = 0x1529B70;
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x164));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
    [JsonIgnore] public short RangeCount => BinaryPrimitives.ReadInt16LittleEndian(Control.AsSpan().Slice(0x168));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(0x100));
    public void Validate() => NativeBattleChoco100Io.ValidateState(this);
}
public sealed record NativeBattleChoco100IoInitialization(NativeBattleChoco100IoState State, uint NextAddress);
public sealed record NativeBattleChoco100IoRelease(NativeBattleChoco100IoState State, bool Released);
public sealed record NativeBattleChoco100IoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleChoco100IoAdvance(NativeBattleModulePoolTask Task, NativeBattleChoco100IoState State, NativeBattleChoco100IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleChoco100IoCompletion(NativeBattleChoco100IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleChoco100IoQueued(NativeBattleChoco100IoState State, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

public static class NativeBattleChoco100Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x1529B70, 0x160, 0x70EF30, 0x70F130, 0x70F190, 0x70F1A0, 0x19D5018, true, 0, 0x70F1C0);

    internal static void ValidateState(NativeBattleChoco100IoState state)
    {
        NativeBattleProfiledIoCore.Validate(Profile, Core(state));
        state.Banks.Validate();
    }

    public static NativeBattleChoco100IoInitialization Initialize70EE30(ImmutableArray<byte> retainedScript, uint nextAddress, NativeBattleChoco97IoBanks banks)
    {
        banks.Validate();
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            Set(offset, 0x19D5018);
        }
        foreach ((int offset, uint address) in new (int, uint)[] { (0x108, 0x19E3018), (0x118, 0x19E8678), (0x128, 0x19EF018), (0x12C, 0x19E4970), (0x130, 0x19DC678) })
        {
            Set(offset, address);
        }
        return new(Public(state with { Control = control.ToImmutableArray() }, banks), checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));

        void Set(int offset, uint address) => BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset), address);
    }
    public static NativeBattleChoco100IoRelease ReleaseWait(NativeBattleChoco100IoState state)
    {
        state.Validate();
        var release = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(release.State, state.Banks), release.Released);
    }
    public static NativeBattleChoco100IoAdvance Advance70EF30(NativeBattleModulePoolTask task, NativeBattleChoco100IoState state)
    {
        state.Validate();
        NativeBattleProfiledIoAdvance advance = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleChoco100IoRequest? request = advance.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advance.Task, Public(advance.State, state.Banks), request, advance.ReadyToComplete);
    }
    public static NativeBattleChoco100IoQueued CopyAndQueueFile(NativeBattleChoco100IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleChoco97IoBanks banks = state.Banks;
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file, (request, data) => banks = banks.Write(request.DestinationAddress, data));
        return new(state with { Banks = banks }, queued.Memory, queued.FileIo);
    }
    public static NativeBattleChoco100IoCompletion Complete(NativeBattleChoco100IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        state.Validate();
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(completed.State, state.Banks), completed.Memory);
    }
    private static NativeBattleProfiledIoState Core(NativeBattleChoco100IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleChoco100IoState Public(NativeBattleProfiledIoState state, NativeBattleChoco97IoBanks banks) => new(state.Control, state.Script, state.CursorAddress, banks);
}
