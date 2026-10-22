using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleDoom8IoInitialization(NativeBattleProfiledIoState State, uint NextAddress);

internal static class NativeBattleDoom8Io
{
    internal const uint ScriptAddress = 0x1636C78;
    internal const int ScriptLength = 0x70;
    internal const uint ControlAddress = 0x275FA10;
    internal const uint ControlPointerGlobal = 0x275F23C;
    internal const uint CursorGlobal = 0x275E830;
    internal const uint NextAddressGlobal = 0x275F9D8;

    internal static NativeBattleDoom8IoInitialization Initialize8C4E60(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(NativeBattleIoProfile.Doom8, retainedScript, nextAddress);
        byte[] control = state.Control.ToArray();
        for (int offset = 0x104; offset <= 0x110; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset, 4), 0x19F0324);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x128, 4), 0x19ECBB8);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x12C, 4), 0x19ED83C);
        for (int offset = 0x130; offset <= 0x160; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset, 4), 0x19F0324);
        }
        return new(state with { Control = control.ToImmutableArray() }, checked(nextAddress + NativeBattleProfiledIoCore.BufferLength));
    }

    internal static (NativeBattleProfiledIoState State, bool Released) Release8C5550(NativeBattleProfiledIoState state)
    {
        return NativeBattleProfiledIoCore.ReleaseWait(NativeBattleIoProfile.Doom8, state);
    }

    internal static NativeBattleProfiledIoAdvance Advance8C4F60(NativeBattleModulePoolTask task, NativeBattleProfiledIoState state)
    {
        return NativeBattleProfiledIoCore.Advance(NativeBattleIoProfile.Doom8, task, state);
    }

    internal static (NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo) CopyAndQueueFile(NativeBattleProfiledIoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file, Action<NativeBattleProfiledIoRequest, ImmutableArray<byte>>? writeExternal = null)
    {
        return NativeBattleProfiledIoCore.CopyAndQueue(NativeBattleIoProfile.Doom8, state, memory, fileIo, file, writeExternal);
    }

    internal static (NativeBattleProfiledIoState State, NativeBattleEffectMemory Memory) Complete(NativeBattleProfiledIoState state, NativeBattleEffectMemory memory, uint callback, Func<NativeBattleProfiledIoState, NativeBattleEffectMemory, uint, (NativeBattleProfiledIoState State, NativeBattleEffectMemory Memory)>? publishStream = null)
    {
        return NativeBattleProfiledIoCore.Complete(NativeBattleIoProfile.Doom8, state, memory, callback, publishStream);
    }
}
