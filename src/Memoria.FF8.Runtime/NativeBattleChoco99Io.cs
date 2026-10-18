using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
public sealed record NativeBattleChoco99IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress, [property: JsonRequired] NativeBattleChoco97IoBanks Banks)
{
    public const uint ScriptAddress = 0x152AAA0;
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x164));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
    [JsonIgnore] public short RangeCount => BinaryPrimitives.ReadInt16LittleEndian(Control.AsSpan().Slice(0x168));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(0x100));
    public void Validate() => NativeBattleChoco99Io.ValidateState(this);
}
public sealed record NativeBattleChoco99IoInitialization(NativeBattleChoco99IoState State, uint NextAddress);
public sealed record NativeBattleChoco99IoRelease(NativeBattleChoco99IoState State, bool Released);
public sealed record NativeBattleChoco99IoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleChoco99IoAdvance(NativeBattleModulePoolTask Task, NativeBattleChoco99IoState State, NativeBattleChoco99IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleChoco99IoCompletion(NativeBattleChoco99IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleChoco99IoQueued(NativeBattleChoco99IoState State, NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

public static class NativeBattleChoco99Io
{
    private static NativeBattleIoProfile Profile { get; } = new(0x152AAA0, 0x1A0, 0x718310, 0x718510, 0x718570, 0x718580, 0x19D3018, true, 0x718590);

    internal static void ValidateState(NativeBattleChoco99IoState state)
    {
        NativeBattleProfiledIoCore.Validate(Profile, Core(state));
        state.Banks.Validate();
    }

    public static NativeBattleChoco99IoInitialization Initialize718200(ImmutableArray<byte> retainedScript, uint nextAddress, NativeBattleChoco97IoBanks banks)
    {
        banks.Validate();
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            Set(offset, 0x19D3018);
        }
        foreach ((int offset, uint address) in new (int, uint)[] { (0x128, 0x19E3018), (0x12C, 0x19EB018), (0x130, 0x19EED20), (0x134, 0x19F0F40), (0x138, 0x19DF380), (0x13C, 0x19E2054), (0x140, 0x19E4778), (0x144, 0x19DD2D0), (0x148, 0x19DE510) })
        {
            Set(offset, address);
        }
        return new(Public(state with { Control = control.ToImmutableArray() }, banks), checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));

        void Set(int offset, uint address) => BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset), address);
    }
    public static NativeBattleChoco99IoRelease ReleaseWait(NativeBattleChoco99IoState state)
    {
        state.Validate();
        var release = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(state));
        return new(Public(release.State, state.Banks), release.Released);
    }
    public static NativeBattleChoco99IoAdvance Advance718310(NativeBattleModulePoolTask task, NativeBattleChoco99IoState state)
    {
        state.Validate();
        NativeBattleProfiledIoAdvance advance = NativeBattleProfiledIoCore.Advance(Profile, task, Core(state));
        NativeBattleChoco99IoRequest? request = advance.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advance.Task, Public(advance.State, state.Banks), request, advance.ReadyToComplete);
    }
    public static NativeBattleChoco99IoQueued CopyAndQueueFile(NativeBattleChoco99IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleChoco97IoBanks banks = state.Banks;
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(state), memory, fileIo, file, (request, data) => banks = banks.Write(request.DestinationAddress, data));
        return new(state with { Banks = banks }, queued.Memory, queued.FileIo);
    }
    public static NativeBattleChoco99IoCompletion Complete(NativeBattleChoco99IoState state, NativeBattleEffectMemory memory, uint callback)
    {
        state.Validate();
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(state), memory, callback);
        return new(Public(completed.State, state.Banks), completed.Memory);
    }
    private static NativeBattleProfiledIoState Core(NativeBattleChoco99IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleChoco99IoState Public(NativeBattleProfiledIoState state, NativeBattleChoco97IoBanks banks) => new(state.Control, state.Script, state.CursorAddress, banks);
}
