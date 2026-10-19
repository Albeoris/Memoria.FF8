using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90IoState([property: JsonRequired] ImmutableArray<byte> Script, [property: JsonRequired] NativeBattleChoco97IoBanks? Banks);
public sealed record NativeBattleTonberry90IoAdvance(NativeBattleTonberry90Frame Frame, NativeBattleModulePoolTask Task, NativeBattleChoco97IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleTonberry90IoQueued(NativeBattleTonberry90Frame Frame, NativeBattleFileIoState FileIo);

public static class NativeBattleTonberry90Io
{
    public const uint ScriptAddress = 0x15474D0;
    private static readonly NativeBattleIoProfile Profile = new(ScriptAddress, 0x40, 0x762850, 0x762A50, 0x762AB0, 0x762AC0, 0x19DB018, true, 0x762AD0, 0x762AE0, false, 0x762A80);

    public static NativeBattleTonberry90Frame Initialize762760(NativeBattleTonberry90Frame frame, ImmutableArray<byte> retainedScript, NativeBattleChoco97IoBanks? banks = null)
    {
        NativeBattleTonberry90State shared = Shared(frame);
        ImmutableArray<byte> script = shared.Io?.Script ?? retainedScript;
        uint next = shared.ReadDword(0x25A4C1C);
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, script, next);
        shared = shared.WriteDword(0x25A2CF8, ScriptAddress).WriteDword(0x25A3DEC, 0x25A4C28).WriteDword(0x25A4C1C, unchecked(next + NativeBattleProfiledIoCore.BufferLength));
        shared = shared.Write(0x25A4C28, state.Control.AsSpan()) with { Io = new(state.Script, shared.Io?.Banks ?? banks) };
        return frame with { Memory = frame.Memory with { Tonberry90 = shared } };
    }

    public static (NativeBattleTonberry90Frame Frame, bool Released) Release763C10(NativeBattleTonberry90Frame frame)
    {
        var result = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(frame));
        return (Store(frame, result.State), result.Released);
    }

    public static NativeBattleTonberry90IoAdvance Advance762850(NativeBattleTonberry90Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleProfiledIoAdvance advanced = NativeBattleProfiledIoCore.Advance(Profile, task, Core(frame));
        NativeBattleChoco97IoRequest? request = advanced.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(Store(frame, advanced.State), advanced.Task, request, advanced.ReadyToComplete);
    }

    public static NativeBattleTonberry90IoQueued CopyAndQueueFile(NativeBattleTonberry90Frame frame, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        NativeBattleTonberry90State shared = Shared(frame);
        NativeBattleChoco97IoBanks? banks = shared.Io?.Banks;
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(frame), frame.Memory, fileIo, file, (request, bytes) => banks = (banks ?? throw new NotSupportedException("Native Tonberry data file requires its explicitly supplied external destination owner.")).Write(request.DestinationAddress, bytes));
        shared = Shared(frame) with { Io = (shared.Io ?? throw new NotSupportedException("Native Tonberry IO script is missing.")) with { Banks = banks } };
        return new(frame with { Memory = queued.Memory with { Tonberry90 = shared } }, queued.FileIo);
    }

    public static NativeBattleTonberry90Frame Complete(NativeBattleTonberry90Frame frame, uint callback)
    {
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(frame), frame.Memory, callback, (state, memory, source) =>
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Tonberry stream completion requires its live file buffer.");
            if (source < NativeBattleEffectNodeMemory.ArenaAddress || source - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length - 4)
            {
                throw new NotSupportedException("Native Tonberry stream descriptor leaves its current arena.");
            }
            int number = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked((int)(source - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
            NativeBattleSummonStreamState stream = (memory.SummonStream ?? NativeBattleSummonStreamState.Unknown()).Publish501860(source, number);
            byte[] control = state.Control.ToArray();
            control[0x16A] = 1;
            return (state with { Control = control.ToImmutableArray() }, memory with { SummonStream = stream });
        });
        return Store(frame with { Memory = completed.Memory }, completed.State);
    }

    private static NativeBattleProfiledIoState Core(NativeBattleTonberry90Frame frame)
    {
        NativeBattleTonberry90State shared = Shared(frame);
        NativeBattleTonberry90IoState io = shared.Io ?? throw new NotSupportedException("Native Tonberry IO requires its current retained script.");
        return new(shared.Read(shared.ReadDword(0x25A3DEC), 0x16C).ToImmutableArray(), io.Script, shared.ReadDword(0x25A2CF8));
    }

    private static NativeBattleTonberry90Frame Store(NativeBattleTonberry90Frame frame, NativeBattleProfiledIoState state)
    {
        NativeBattleTonberry90State shared = Shared(frame);
        NativeBattleTonberry90IoState io = shared.Io ?? throw new NotSupportedException("Native Tonberry IO requires its current script owner.");
        shared = shared.Write(shared.ReadDword(0x25A3DEC), state.Control.AsSpan()).WriteDword(0x25A2CF8, state.CursorAddress) with { Io = io with { Script = state.Script } };
        return frame with { Memory = frame.Memory with { Tonberry90 = shared } };
    }

    private static NativeBattleTonberry90State Shared(NativeBattleTonberry90Frame frame)
    {
        NativeBattleTonberry90State state = frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry IO process owner is missing.");
        state.Validate();
        return state;
    }
}
