using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMoogle96IoState([property: JsonRequired] ImmutableArray<byte> Control, [property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] uint CursorAddress)
{
    public const uint ScriptAddress = 0x152BBA0;
    [JsonIgnore] public ushort Busy => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x164));
    [JsonIgnore] public ushort WaitReleased => BinaryPrimitives.ReadUInt16LittleEndian(Control.AsSpan().Slice(0x166));
    [JsonIgnore] public uint BufferAddress => BinaryPrimitives.ReadUInt32LittleEndian(Control.AsSpan().Slice(0x100));
    public void Validate()
    {
        if (Control.IsDefault || Control.Length != 0x16C || Script.IsDefault || Script.Length != 0x60 || CursorAddress < ScriptAddress || CursorAddress >= ScriptAddress + 0x60 || ((CursorAddress - ScriptAddress) & 15) != 0)
        {
            throw new InvalidDataException("Native Moogle IO requires its complete control, current script and aligned cursor owners.");
        }
        NativeBattleMoogle96Io.RequireBuffer(BufferAddress);
    }
}

public sealed record NativeBattleMoogle96IoInitialization(NativeBattleMoogle96IoState State, uint NextAddress);
public sealed record NativeBattleMoogle96IoRelease(NativeBattleMoogle96IoState State, bool Released);
public sealed record NativeBattleMoogle96IoRequest(short FileId, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleMoogle96IoAdvance(NativeBattleModulePoolTask Task, NativeBattleMoogle96IoState State, NativeBattleMoogle96IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleMoogle96IoCompletion(NativeBattleMoogle96IoState State, NativeBattleEffectMemory Memory);
public sealed record NativeBattleMoogle96IoQueued(NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

/// <summary>Schema-preserving module facade over the shared current-script file scheduler contract.</summary>
public static class NativeBattleMoogle96Io
{
    public static NativeBattleMoogle96IoInitialization Initialize7322A0(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(NativeBattleIoProfile.Moogle96, retainedScript, nextAddress);
        return new(Public(state), checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));
    }

    public static NativeBattleMoogle96IoRelease ReleaseWait733A80(NativeBattleMoogle96IoState state)
    {
        var released = NativeBattleProfiledIoCore.ReleaseWait(NativeBattleIoProfile.Moogle96, Core(state));
        return new(Public(released.State), released.Released);
    }

    public static NativeBattleMoogle96IoAdvance Advance732390(NativeBattleModulePoolTask task, NativeBattleMoogle96IoState state)
    {
        NativeBattleProfiledIoAdvance advance = NativeBattleProfiledIoCore.Advance(NativeBattleIoProfile.Moogle96, task, Core(state));
        NativeBattleMoogle96IoRequest? request = advance.Request is { } file ? new(checked((short)file.FileId), file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(advance.Task, Public(advance.State), request, advance.ReadyToComplete);
    }

    public static NativeBattleMoogle96IoQueued CopyAndQueueFile(NativeBattleMoogle96IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(NativeBattleIoProfile.Moogle96, Core(state), memory, fileIo, file);
        return new(queued.Memory, queued.FileIo);
    }

    public static NativeBattleMoogle96IoCompletion Complete732590(NativeBattleMoogle96IoState state, NativeBattleEffectMemory memory)
    {
        var completed = NativeBattleProfiledIoCore.Complete(NativeBattleIoProfile.Moogle96, Core(state), memory, 0x732590);
        return new(Public(completed.State), completed.Memory);
    }

    internal static void RequireBuffer(uint address) => NativeBattleProfiledIoCore.RequireBuffer(address);
    private static NativeBattleProfiledIoState Core(NativeBattleMoogle96IoState state) => new(state.Control, state.Script, state.CursorAddress);
    private static NativeBattleMoogle96IoState Public(NativeBattleProfiledIoState state) => new(state.Control, state.Script, state.CursorAddress);
}
