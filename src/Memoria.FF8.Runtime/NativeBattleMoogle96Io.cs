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

/// <summary>732390's current-script caller and732590's asynchronous TIM completion, over the existing file scheduler.</summary>
public static class NativeBattleMoogle96Io
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const int BufferLength = 0xA000;

    public static NativeBattleMoogle96IoInitialization Initialize7322A0(ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        RequireBuffer(nextAddress);
        if (retainedScript.IsDefault || retainedScript.Length != 0x60)
        {
            throw new InvalidDataException("Native Moogle IO initialization requires its explicitly retained current script bytes.");
        }
        byte[] bytes = new byte[0x16C];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100), nextAddress);
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 0x1D9B018);
        }
        return new(new(bytes.ToImmutableArray(), retainedScript, NativeBattleMoogle96IoState.ScriptAddress), checked(nextAddress + BufferLength));
    }

    public static NativeBattleMoogle96IoRelease ReleaseWait733A80(NativeBattleMoogle96IoState state)
    {
        state.Validate();
        if (state.WaitReleased != 0 || Opcode(state) != 0)
        {
            return new(state, false);
        }
        return new(WriteControlWord(state, 0x166, 1) with { CursorAddress = checked(state.CursorAddress + 16) }, true);
    }

    public static NativeBattleMoogle96IoAdvance Advance732390(NativeBattleModulePoolTask task, NativeBattleMoogle96IoState state)
    {
        state.Validate();
        if (task.Callback != 0x732390 || task.Payload.IsDefault || task.Payload.Length != 0x30 - 12)
        {
            throw new InvalidDataException("Native Moogle IO requires its actual complete30-byte hierarchy task.");
        }
        NativeBattleMoogle96IoRequest? request = null;
        int phase = task.ReadByte(0x29);
        switch (phase)
        {
            case 0:
                switch (Opcode(state))
                {
                    case 0:
                        state = WriteControlWord(state, 0x166, 0);
                        break;
                    case 1:
                        if (state.Busy != 0)
                        {
                            throw new NotSupportedException("Native Moogle file begin requires its actual idle single-script consumer.");
                        }
                        int cursor = Cursor(state);
                        byte[] script = state.Script.ToArray();
                        BinaryPrimitives.WriteUInt32LittleEndian(script.AsSpan(cursor + 12), state.BufferAddress);
                        state = WriteControlWord(state with { Script = script.ToImmutableArray() }, 0x164, 1);
                        request = CurrentFile(state);
                        task = task.WriteByte(0x29, 1);
                        break;
                    case 0xFF:
                        state = WriteControlWord(state, 0x166, 0);
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 1);
                        break;
                    default:
                        throw new NotSupportedException("Native Moogle IO script requires its proved WAIT, file1 or FF opcode.");
                }
                break;
            case 1:
                if ((task.ReadByte(0x26) & 1) != 0)
                {
                    task = task.WriteByte(0x29, 2);
                }
                else if (state.Busy == 0)
                {
                    if (Opcode(state) != 1)
                    {
                        throw new NotSupportedException("Native Moogle file polling requires its actual non-stream file1 record.");
                    }
                    state = state with { CursorAddress = checked(state.CursorAddress + 16) };
                    state.Validate();
                    task = task.WriteByte(0x29, 0);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Moogle IO task has an unsupported phase.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, state, request, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    public static NativeBattleMoogle96IoQueued CopyAndQueueFile(NativeBattleMoogle96IoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file)
    {
        state.Validate();
        NativeBattleMoogle96IoRequest request = CurrentFile(state);
        if (file.FileId != request.FileId || file.Data.IsDefault || file.Data.Length != request.DeclaredLength)
        {
            throw new InvalidDataException("Native Moogle file input must match its actual script ID and declared length.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Moogle file copy requires its actual allocated effect arena.");
        memory = memory with { Arena = arena.Write(checked((int)(request.DestinationAddress - ArenaAddress)), file.Data.AsSpan()) };
        return new(memory, fileIo.Queue482610(file.Data.Length, request.CompletionCallback, null));
    }

    public static NativeBattleMoogle96IoCompletion Complete732590(NativeBattleMoogle96IoState state, NativeBattleEffectMemory memory)
    {
        state.Validate();
        NativeBattleMoogle96IoRequest request = CurrentFile(state);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Moogle IO completion requires its current file buffer.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Moogle IO completion requires the existing TIM upload queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, checked((int)(request.DestinationAddress - ArenaAddress)));
        return new(WriteControlWord(state, 0x164, 0), memory with { Textures = textures });
    }

    internal static void RequireBuffer(uint address)
    {
        if (address < ArenaAddress || address - ArenaAddress > NativeBattleEffectArena.Length - BufferLength)
        {
            throw new InvalidDataException("Native Moogle IO buffer reservation leaves its actual effect arena.");
        }
    }

    private static NativeBattleMoogle96IoRequest CurrentFile(NativeBattleMoogle96IoState state)
    {
        int cursor = Cursor(state);
        uint selector = BinaryPrimitives.ReadUInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 4));
        int length = BinaryPrimitives.ReadInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 8));
        uint destination = BinaryPrimitives.ReadUInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 12));
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor + 2));
        if (Opcode(state) != 1 || selector != 0 || id is < 438 or > 441 || state.Busy == 0 || destination != state.BufferAddress || length <= 0 || length > BufferLength)
        {
            throw new NotSupportedException("Native Moogle file operation requires its active owned file1 record and declared buffer extent.");
        }
        return new((short)id, destination, length, 0x732590);
    }

    private static int Cursor(NativeBattleMoogle96IoState state) => checked((int)(state.CursorAddress - NativeBattleMoogle96IoState.ScriptAddress));
    private static ushort Opcode(NativeBattleMoogle96IoState state) => BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(Cursor(state)));
    private static NativeBattleMoogle96IoState WriteControlWord(NativeBattleMoogle96IoState state, int offset, ushort value)
    {
        byte[] bytes = state.Control.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        return state with { Control = bytes.ToImmutableArray() };
    }
}
