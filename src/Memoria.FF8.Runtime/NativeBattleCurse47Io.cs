using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47IoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
public sealed record NativeBattleCurse47IoAdvance(NativeBattleModulePoolTask Task, NativeBattleCurse47State State, NativeBattleCurse47IoRequest? Request, bool ReadyToComplete);

public static class NativeBattleCurse47Io
{
    public const uint ScriptAddress = 0x15B8230;
    public const int ScriptLength = 80;
    public const uint Callback = 0x82AD90;
    public const uint ControlAddress = 0x264EAF0;
    public const uint ControlPointerGlobal = 0x264E6A8;
    public const uint CursorGlobal = 0x264E6A0;
    public const uint NextAddressGlobal = 0x264EAE8;
    internal static NativeBattleIoProfile Profile { get; } = new(ScriptAddress, ScriptLength, Callback, 0x82AF90, 0x82AFF0, 0x82B000, 0x19EB018, true, 0x82B010, 0x82B020, false, 0x82AFC0);

    public static NativeBattleCurse47State Initialize(NativeBattleCurse47State state)
    {
        NativeBattleProfiledIoState initialized = NativeBattleProfiledIoCore.Initialize(Profile, state.IoScript ?? throw new NotSupportedException("Curse IO initialization requires its actual retained script."), state.ReadDword(NextAddressGlobal));
        byte[] control = initialized.Control.ToArray();
        for (int offset = 0x104; offset <= 0x110; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset), 0x19F0324);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x128), 0x19ECBB8);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x12C), 0x19ED83C);
        for (int offset = 0x130; offset <= 0x160; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset), 0x19F0324);
        }
        return state.WriteDword(ControlPointerGlobal, ControlAddress).WriteDword(NextAddressGlobal, checked(state.ReadDword(NextAddressGlobal) + NativeBattleProfiledIoCore.BufferLength)).WithIo(initialized with { Control = control.ToImmutableArray() });
    }

    public static (NativeBattleCurse47State State, bool Released) ReleaseWait(NativeBattleCurse47State state)
    {
        var released = NativeBattleProfiledIoCore.ReleaseWait(Profile, state.ReadIo());
        return (state.WithIo(released.State), released.Released);
    }

    public static NativeBattleCurse47IoAdvance AdvanceTask(NativeBattleModulePoolTask task, NativeBattleCurse47State state)
    {
        NativeBattleProfiledIoAdvance step = NativeBattleProfiledIoCore.Advance(Profile, task, state.ReadIo());
        NativeBattleCurse47IoRequest? request = step.Request is { } value ? Convert(value) : null;
        return new(step.Task, state.WithIo(step.State), request, step.ReadyToComplete);
    }

    public static (NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo) CopyAndQueueFile(NativeBattleCurse47State state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file, Action<NativeBattleCurse47IoRequest, ImmutableArray<byte>>? writeExternal = null)
    {
        return NativeBattleProfiledIoCore.CopyAndQueue(Profile, state.ReadIo(), memory, fileIo, file, writeExternal is null ? null : (request, bytes) => writeExternal(Convert(request), bytes));
    }

    public static (NativeBattleCurse47State State, NativeBattleEffectMemory Memory) Complete(NativeBattleCurse47State state, NativeBattleEffectMemory memory, uint callback, Func<NativeBattleCurse47State, NativeBattleEffectMemory, uint, (NativeBattleCurse47State State, NativeBattleEffectMemory Memory)>? publishStream = null)
    {
        NativeBattleCurse47State retained = state;
        var completed = NativeBattleProfiledIoCore.Complete(Profile, state.ReadIo(), memory, callback, publishStream is null ? null : (io, currentMemory, address) =>
        {
            var published = publishStream(retained.WithIo(io), currentMemory, address);
            retained = published.State;
            return (retained.ReadIo(), published.Memory);
        });
        return (retained.WithIo(completed.State), completed.Memory);
    }

    private static NativeBattleCurse47IoRequest Convert(NativeBattleProfiledIoRequest value) => new(value.Opcode, value.FileId, value.Index, value.DestinationAddress, value.DeclaredLength, value.CompletionCallback);
}
