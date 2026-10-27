using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95IoState([property: JsonRequired] ImmutableArray<byte> Script);
public sealed record NativeBattleSiren95IoAdvance(NativeBattleSiren95Frame Frame, NativeBattleModulePoolTask Task, NativeBattleChoco97IoRequest? Request, bool ReadyToComplete);
public sealed record NativeBattleSiren95IoQueued(NativeBattleSiren95Frame Frame, NativeBattleFileIoState FileIo);

public static class NativeBattleSiren95Io
{
    public const uint ScriptAddress = 0x1533850;
    private static readonly NativeBattleIoProfile Profile = new(ScriptAddress, 0x180, 0x73A380, 0x73A580, 0x73A5E0, 0x73A5F0, 0x19D1018, true, 0x73A600, 0x73A610, false, 0x73A5B0);

    public static NativeBattleSiren95Frame Initialize73A270(NativeBattleSiren95Frame frame, ImmutableArray<byte> retainedScript)
    {
        NativeBattleSiren95State shared = Shared(frame);
        ImmutableArray<byte> script = shared.Io?.Script ?? retainedScript;
        uint next = shared.ReadDword(0x258FB74);
        NativeBattleProfiledIoState state = NativeBattleProfiledIoCore.Initialize(Profile, script, next);
        shared = shared.WriteDword(0x258BFA8, ScriptAddress).WriteDword(0x258EAA0, 0x258FB88).WriteDword(0x258FB74, unchecked(next + NativeBattleProfiledIoCore.BufferLength));
        byte[] control = state.Control.ToArray();
        foreach ((int offset, uint address) in new (int, uint)[] { (0x108, 0x19DFCC4), (0x10C, 0x19F1018), (0x128, 0x19F1018), (0x12C, 0x19E10F8), (0x130, 0x19EBF30), (0x134, 0x19ED200), (0x138, 0x19EE4D0), (0x13C, 0x19F19BC), (0x144, 0x19E0F58), (0x148, 0x19E1028) })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(control.AsSpan(offset), address);
        }
        state = state with { Control = control.ToImmutableArray() };
        shared = shared.Write(0x258FB88, state.Control.AsSpan()) with { Io = new(state.Script) };
        return frame with { Memory = frame.Memory with { Siren95 = shared } };
    }

    public static (NativeBattleSiren95Frame Frame, bool Released) Release73A950(NativeBattleSiren95Frame frame)
    {
        var result = NativeBattleProfiledIoCore.ReleaseWait(Profile, Core(frame));
        return (Store(frame, result.State), result.Released);
    }

    public static NativeBattleSiren95IoAdvance Advance73A380(NativeBattleSiren95Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleProfiledIoAdvance advanced = NativeBattleProfiledIoCore.Advance(Profile, task, Core(frame));
        NativeBattleChoco97IoRequest? request = advanced.Request is { } file ? new(file.Opcode, file.FileId, file.Index, file.DestinationAddress, file.DeclaredLength, file.CompletionCallback) : null;
        return new(Store(frame, advanced.State), advanced.Task, request, advanced.ReadyToComplete);
    }

    public static NativeBattleSiren95IoQueued CopyAndQueueFile(NativeBattleSiren95Frame frame, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(frame.Memory);
        var queued = NativeBattleProfiledIoCore.CopyAndQueue(Profile, Core(frame), frame.Memory, fileIo, file, (request, bytes) => banks = banks.Write(request.DestinationAddress, bytes));
        NativeBattleEffectMemory memory = NativeBattleProcessFileBanks.Write(queued.Memory, banks);
        return new(frame with { Memory = memory }, queued.FileIo);
    }

    public static NativeBattleGfAnimationSource ReadProgrammeSource(NativeBattleEffectMemory memory, uint address)
    {
        NativeBattleChoco97IoBanks banks = NativeBattleProcessFileBanks.Read(memory);
        long offset = (long)address - banks.BaseAddress;
        if (offset < 0 || offset >= banks.Length)
        {
            throw new NotSupportedException("Native Siren programme leaves its current physical file bank.");
        }
        int end = banks.Length;
        foreach (NativeBattleEffectStorageRange unknown in banks.Bytes.EnumerateUnknownRanges())
        {
            if ((long)unknown.Offset + unknown.Length <= offset)
            {
                continue;
            }
            end = Math.Min(end, unknown.Offset);
            break;
        }
        if (end <= offset)
        {
            throw new NotSupportedException("Native Siren programme starts in unknown current file bytes.");
        }
        return NativeBattleGfAnimationSource.Borrow(address, banks.Bytes, checked((int)offset), checked(end - (int)offset));
    }

    public static NativeBattleSiren95Frame Complete(NativeBattleSiren95Frame frame, uint callback)
    {
        var completed = NativeBattleProfiledIoCore.Complete(Profile, Core(frame), frame.Memory, callback, (state, memory, source) =>
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Siren stream completion requires its live file buffer.");
            if (source < NativeBattleEffectNodeMemory.ArenaAddress || source - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length - 4)
            {
                throw new NotSupportedException("Native Siren stream descriptor leaves its current arena.");
            }
            int number = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(checked((int)(source - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
            NativeBattleSummonStreamState stream = (memory.SummonStream ?? NativeBattleSummonStreamState.Unknown()).Publish501860(source, number);
            byte[] control = state.Control.ToArray();
            control[0x16A] = 1;
            return (state with { Control = control.ToImmutableArray() }, memory with { SummonStream = stream });
        });
        return Store(frame with { Memory = completed.Memory }, completed.State);
    }

    private static NativeBattleProfiledIoState Core(NativeBattleSiren95Frame frame)
    {
        NativeBattleSiren95State shared = Shared(frame);
        NativeBattleSiren95IoState io = shared.Io ?? throw new NotSupportedException("Native Siren IO requires its current retained script.");
        return new(shared.Read(shared.ReadDword(0x258EAA0), 0x16C).ToImmutableArray(), io.Script, shared.ReadDword(0x258BFA8));
    }

    private static NativeBattleSiren95Frame Store(NativeBattleSiren95Frame frame, NativeBattleProfiledIoState state)
    {
        NativeBattleSiren95State shared = Shared(frame);
        NativeBattleSiren95IoState io = shared.Io ?? throw new NotSupportedException("Native Siren IO requires its current script owner.");
        shared = shared.Write(shared.ReadDword(0x258EAA0), state.Control.AsSpan()).WriteDword(0x258BFA8, state.CursorAddress) with { Io = io with { Script = state.Script } };
        return frame with { Memory = frame.Memory with { Siren95 = shared } };
    }

    private static NativeBattleSiren95State Shared(NativeBattleSiren95Frame frame)
    {
        NativeBattleSiren95State state = frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren IO process owner is missing.");
        state.Validate();
        return state;
    }
}
