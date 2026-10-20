using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85IoAdvance(NativeBattleTheEnd85Frame Frame, NativeBattleModulePoolTask Task, NativeBattleChoco97IoRequest? Request, bool Complete);
public sealed record NativeBattleTheEnd85IoQueued(NativeBattleTheEnd85Frame Frame, NativeBattleFileIoState FileIo);

public static class NativeBattleTheEnd85Io
{
    public const uint ScriptAddress = 0x1571E70;
    private static readonly NativeBattleIoProfile Profile = new(ScriptAddress, 80, 0x784A60, 0x784C60, 0x784CC0, 0x784CD0, 0x19CF018, true, 0x784CE0, 0x784CF0, false, 0x784C90);

    public static NativeBattleTheEnd85Frame Initialize784960(NativeBattleTheEnd85Frame frame, ImmutableArray<byte> sourceScript)
    {
        NativeBattleTheEnd85State state = State(frame);
        uint buffer = state.Dword(0x25C21D8);
        NativeBattleProfiledIoState core = NativeBattleProfiledIoCore.Initialize(Profile, state.Script ?? sourceScript, buffer);
        byte[] control = core.Control.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x114), 0x19EB018);
        BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(0x12C), 0x19D2560);
        state = state.WriteDword(0x25C1B60, ScriptAddress).WriteDword(0x25C1C58, 0x25C21E0).WriteDword(0x25C21D8, checked(buffer + NativeBattleProfiledIoCore.BufferLength)).Write(0x25C21E0, control) with { Script = core.Script };
        return frame with { Memory = frame.Memory with { TheEnd85 = state } };
    }

    public static NativeBattleTheEnd85IoAdvance Advance784A60(NativeBattleTheEnd85Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleProfiledIoAdvance step = NativeBattleProfiledIoCore.Advance(Profile, task, Core(frame));
        NativeBattleChoco97IoRequest? request = step.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(Store(frame, step.State), step.Task, request, step.ReadyToComplete);
    }

    public static NativeBattleTheEnd85IoQueued CopyAndQueue(NativeBattleTheEnd85Frame frame, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(frame.Memory);
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(frame), frame.Memory, fileIo, file, (request, bytes) => banks = banks.Write(request.DestinationAddress, bytes));
        return new(frame with { Memory = NativeBattleProcessFileBanks.Write(queued.Memory, banks) }, queued.FileIo);
    }

    public static NativeBattleTheEnd85Frame Complete(NativeBattleTheEnd85Frame frame, uint callback)
    {
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(frame), frame.Memory, callback, (state, memory, source) =>
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native The End stream requires its actual file arena.");
            if (source < NativeBattleEffectNodeMemory.ArenaAddress || source - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length - 4)
            {
                throw new NotSupportedException("Native The End stream source leaves its current arena.");
            }
            int number = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked((int)(source - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
            NativeBattleSummonStreamState stream = (memory.SummonStream ?? NativeBattleSummonStreamState.Unknown()).Publish501860(source, number);
            byte[] control = state.Control.ToArray();
            control[0x16A] = 1;
            return (state with { Control = control.ToImmutableArray() }, memory with { SummonStream = stream });
        });
        return Store(frame with { Memory = completed.Memory }, completed.State);
    }

    private static NativeBattleTheEnd85State State(NativeBattleTheEnd85Frame frame)
    {
        NativeBattleTheEnd85State state = frame.Memory.TheEnd85 ?? throw new NotSupportedException("Native The End requires its retained physical process owner.");
        state.Validate();
        return state;
    }
    private static NativeBattleProfiledIoState Core(NativeBattleTheEnd85Frame frame)
    {
        NativeBattleTheEnd85State state = State(frame);
        return new(state.Read(state.Dword(0x25C1C58), 364).ToImmutableArray(), state.Script ?? throw new NotSupportedException("Native The End requires its initialized script owner."), state.Dword(0x25C1B60));
    }
    private static NativeBattleTheEnd85Frame Store(NativeBattleTheEnd85Frame frame, NativeBattleProfiledIoState core)
    {
        NativeBattleTheEnd85State state = State(frame);
        state = state.Write(state.Dword(0x25C1C58), core.Control.AsSpan()).WriteDword(0x25C1B60, core.CursorAddress) with { Script = core.Script };
        return frame with { Memory = frame.Memory with { TheEnd85 = state } };
    }
}
