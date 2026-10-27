using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleIoProfile(uint ScriptAddress, int ScriptLength, uint TaskCallback, uint TimCallback, uint DataCallback2, uint DataCallback3, uint DefaultDirectory, bool ExternalFiles, uint DataCallback4 = 0, uint DataCallback5 = 0, bool ExternalDirectoryPresets = true, uint StreamCallback = 0)
{
    public static NativeBattleIoProfile Moogle96 { get; } = new(0x152BBA0, 0x60, 0x732390, 0x732590, 0, 0, 0x1D9B018, false);
    public static NativeBattleIoProfile Choco97 { get; } = new(0x152B930, 0x100, 0x729FF0, 0x72A1F0, 0x72A250, 0x72A260, 0x19DB018, true);
    public static NativeBattleIoProfile Doom8 { get; } = new(0x1636C78, 0x70, 0x8C4F60, 0x8C5160, 0x8C51C0, 0x8C51D0, 0x19EB018, true, 0x8C51E0, 0x8C51F0, false, 0x8C5190);
}
internal sealed record NativeBattleProfiledIoState(ImmutableArray<byte> Control, ImmutableArray<byte> Script, uint CursorAddress);
internal sealed record NativeBattleProfiledIoRequest(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback);
internal sealed record NativeBattleProfiledIoAdvance(NativeBattleModulePoolTask Task, NativeBattleProfiledIoState State, NativeBattleProfiledIoRequest? Request, bool ReadyToComplete);

/// <summary>The shared current-script phase algorithm over explicit module profiles and existing file queues.</summary>
internal static class NativeBattleProfiledIoCore
{
    internal const uint ArenaAddress = 0x20DFAB8;
    internal const int BufferLength = 0xA000;

    internal static NativeBattleProfiledIoState Initialize(NativeBattleIoProfile profile, ImmutableArray<byte> retainedScript, uint nextAddress)
    {
        RequireBuffer(nextAddress);
        if (retainedScript.IsDefault || retainedScript.Length != profile.ScriptLength)
        {
            throw new InvalidDataException("Native script IO requires its explicitly retained complete script bytes.");
        }
        byte[] control = new byte[0x16C];
        Dword(control, 0x100, nextAddress);
        for (int offset = 0x104; offset <= 0x160; offset += 4)
        {
            Dword(control, offset, profile.DefaultDirectory);
        }
        if (profile.ExternalFiles && profile.ExternalDirectoryPresets)
        {
            Dword(control, 0x108, 0x19DFEF8);
            Dword(control, 0x12C, 0x19E4958);
            Dword(control, 0x130, 0x19E8660);
        }
        return new(control.ToImmutableArray(), retainedScript, profile.ScriptAddress);
    }

    internal static void Validate(NativeBattleIoProfile profile, NativeBattleProfiledIoState state)
    {
        if (state.Control.IsDefault || state.Control.Length != 0x16C || state.Script.IsDefault || state.Script.Length != profile.ScriptLength || state.CursorAddress < profile.ScriptAddress || state.CursorAddress >= profile.ScriptAddress + profile.ScriptLength || ((state.CursorAddress - profile.ScriptAddress) & 15) != 0)
        {
            throw new InvalidDataException("Native script IO requires its complete control, current script and aligned cursor owners.");
        }
        RequireBuffer(ControlDword(state, 0x100));
    }

    internal static (NativeBattleProfiledIoState State, bool Released) ReleaseWait(NativeBattleIoProfile profile, NativeBattleProfiledIoState state)
    {
        Validate(profile, state);
        if (ControlWord(state, 0x166) != 0 || Opcode(profile, state) != 0)
        {
            return (state, false);
        }
        return (WriteControlWord(state, 0x166, 1) with { CursorAddress = checked(state.CursorAddress + 16) }, true);
    }

    internal static NativeBattleProfiledIoAdvance Advance(NativeBattleIoProfile profile, NativeBattleModulePoolTask task, NativeBattleProfiledIoState state)
    {
        Validate(profile, state);
        if (task.Callback != profile.TaskCallback || task.Payload.IsDefault || task.PayloadLength != 0x30 - 12)
        {
            throw new InvalidDataException("Native script IO requires its actual complete hierarchy task.");
        }
        NativeBattleProfiledIoRequest? request = null;
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0:
                ushort opcode = Opcode(profile, state);
                if (opcode == 0)
                {
                    state = WriteControlWord(state, 0x166, 0);
                }
                else if (opcode == 0xFF)
                {
                    state = WriteControlWord(state, 0x166, 0);
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 1);
                }
                else if (opcode == 1 || IsData(profile, opcode) || IsStream(profile, opcode))
                {
                    if (ControlWord(state, 0x164) != 0)
                    {
                        throw new NotSupportedException("Native file begin requires its actual idle single-script consumer.");
                    }
                    int cursor = Cursor(profile, state);
                    ushort index = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor + 4));
                    uint destination = opcode == 1 || IsStream(profile, opcode) ? ControlDword(state, 0x100) : ControlDword(state, checked((opcode == 2 ? 0x104 : opcode == 3 ? 0x124 : opcode == 5 ? 0x114 : 0x144) + 4 * index));
                    byte[] script = state.Script.ToArray();
                    Dword(script, cursor + 12, destination);
                    state = WriteControlWord(state with { Script = script.ToImmutableArray() }, 0x164, 1);
                    request = CurrentFile(profile, state);
                    if (IsData(profile, opcode))
                    {
                        state = Bookkeep(state, destination, request.DeclaredLength);
                    }
                    task = task.WriteByte(0x29, 1);
                }
                else
                {
                    throw new NotSupportedException("Native script IO selected an operation outside its supplied module contract.");
                }
                break;
            case 1:
                if ((task.ReadByte(0x26) & 1) != 0)
                {
                    task = task.WriteByte(0x29, 2);
                }
                else if (ControlWord(state, 0x164) == 0)
                {
                    ushort pollOpcode = Opcode(profile, state);
                    if (pollOpcode != 1 && !IsData(profile, pollOpcode) && !IsStream(profile, pollOpcode))
                    {
                        throw new NotSupportedException("Native file polling requires its actual supported current record.");
                    }
                    if (IsStream(profile, pollOpcode))
                    {
                        if (state.Control[0x16A] == 0)
                        {
                            break;
                        }
                        byte[] control = state.Control.ToArray();
                        control[0x16A] = 0;
                        state = state with { Control = control.ToImmutableArray() };
                    }
                    state = state with { CursorAddress = checked(state.CursorAddress + 16) };
                    Validate(profile, state);
                    task = task.WriteByte(0x29, 0);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native script IO task has an unsupported phase.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, state, request, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    internal static (NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo) CopyAndQueue(NativeBattleIoProfile profile, NativeBattleProfiledIoState state, NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleEffectFileAsset file, Action<NativeBattleProfiledIoRequest, ImmutableArray<byte>>? writeExternal = null)
    {
        Validate(profile, state);
        NativeBattleProfiledIoRequest request = CurrentFile(profile, state);
        if (file.FileId < 0 || file.FileId != request.FileId || file.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native file input must match its actual script ID and contain its supplied file bytes.");
        }
        NativeBattleTextureFileIdentity identity = new(file.Name, NativeBattleTextureDomain.Battle);
        memory = memory with { Textures = memory.Textures?.OpenTextureFile(identity) };
        if (request.Opcode == 1 || IsStream(profile, request.Opcode))
        {
            if (file.Data.Length > BufferLength)
            {
                throw new NotSupportedException("Native TIM file input exceeds its actual allocated buffer owner.");
            }
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native TIM file copy requires its actual allocated arena.");
            int offset = checked((int)(request.DestinationAddress - ArenaAddress));
            memory = memory with { Arena = arena.Write(offset, file.Data.AsSpan()).RememberTextureFile(offset, file.Data.AsSpan(), identity) };
        }
        else
        {
            (writeExternal ?? throw new NotSupportedException("Native non-TIM file copy requires its explicitly supplied external address owner."))(request, file.Data);
        }
        return (memory, fileIo.Queue482610(file.Data.Length, request.CompletionCallback, null));
    }

    internal static (NativeBattleProfiledIoState State, NativeBattleEffectMemory Memory) Complete(NativeBattleIoProfile profile, NativeBattleProfiledIoState state, NativeBattleEffectMemory memory, uint callback, Func<NativeBattleProfiledIoState, NativeBattleEffectMemory, uint, (NativeBattleProfiledIoState State, NativeBattleEffectMemory Memory)>? publishStream = null)
    {
        Validate(profile, state);
        NativeBattleProfiledIoRequest request = CurrentFile(profile, state);
        if (callback != request.CompletionCallback)
        {
            throw new InvalidDataException("Native file completion must match its current record and callback binding.");
        }
        if (request.Opcode == 1)
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native TIM completion requires its retained file buffer.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native TIM completion requires its existing upload queue.");
            (textures, _) = textures.EnqueueArenaTim505E30(arena, checked((int)(request.DestinationAddress - ArenaAddress)));
            memory = memory with { Textures = textures };
        }
        else if (IsStream(profile, request.Opcode))
        {
            (state, memory) = (publishStream ?? throw new NotSupportedException("Native stream completion requires its explicit501860 publisher."))(state, memory, request.DestinationAddress);
        }
        return (WriteControlWord(state, 0x164, 0), memory);
    }

    internal static NativeBattleProfiledIoRequest CurrentFile(NativeBattleIoProfile profile, NativeBattleProfiledIoState state)
    {
        int cursor = Cursor(profile, state);
        ushort opcode = Opcode(profile, state);
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor + 2));
        ushort index = BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(cursor + 4));
        int length = BinaryPrimitives.ReadInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 8));
        uint destination = BinaryPrimitives.ReadUInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 12));
        bool supported = opcode == 1 || IsData(profile, opcode) || IsStream(profile, opcode);
        bool invalidMoogle = !profile.ExternalFiles && (BinaryPrimitives.ReadUInt32LittleEndian(state.Script.AsSpan().Slice(cursor + 4)) != 0 || id is < 438 or > 441);
        if (!supported || invalidMoogle || ControlWord(state, 0x164) == 0 || length <= 0 && !IsStream(profile, opcode) || opcode == 1 && destination != ControlDword(state, 0x100))
        {
            throw new NotSupportedException("Native file operation requires its active owned record and supplied destination extent.");
        }
        return new(opcode, id, index, destination, length, opcode == 1 ? profile.TimCallback : IsStream(profile, opcode) ? profile.StreamCallback : opcode == 2 ? profile.DataCallback2 : opcode == 3 ? profile.DataCallback3 : opcode == 5 ? profile.DataCallback5 : profile.DataCallback4);
    }

    private static bool IsStream(NativeBattleIoProfile profile, ushort opcode) => opcode == 6 && profile.StreamCallback != 0;

    private static bool IsData(NativeBattleIoProfile profile, ushort opcode) => profile.ExternalFiles && (opcode is 2 or 3 || opcode == 4 && profile.DataCallback4 != 0 || opcode == 5 && profile.DataCallback5 != 0);

    internal static void RequireBuffer(uint address)
    {
        if (address < ArenaAddress || address - ArenaAddress > NativeBattleEffectArena.Length - BufferLength)
        {
            throw new InvalidDataException("Native script IO buffer reservation leaves its actual effect arena.");
        }
    }

    private static NativeBattleProfiledIoState Bookkeep(NativeBattleProfiledIoState state, uint destination, int length)
    {
        int index = unchecked((short)ControlWord(state, 0x168));
        int start = checked(4 * index);
        int end = checked(0x80 + 4 * index);
        byte[] bytes = state.Control.ToArray();
        RequireControlRange(start, 4);
        RequireControlRange(end, 4);
        Dword(bytes, start, destination);
        Dword(bytes, end, unchecked(destination + (uint)length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x168), unchecked((ushort)(index + 1)));
        return state with { Control = bytes.ToImmutableArray() };
    }

    internal static ushort ControlWord(NativeBattleProfiledIoState state, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(state.Control.AsSpan().Slice(offset));
    internal static uint ControlDword(NativeBattleProfiledIoState state, int offset)
    {
        RequireControlRange(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(state.Control.AsSpan().Slice(offset));
    }
    private static void RequireControlRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > 0x16C - length)
        {
            throw new NotSupportedException("Native IO selector or range store leaves its explicit control owner.");
        }
    }
    private static NativeBattleProfiledIoState WriteControlWord(NativeBattleProfiledIoState state, int offset, ushort value)
    {
        byte[] bytes = state.Control.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        return state with { Control = bytes.ToImmutableArray() };
    }
    private static void Dword(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static int Cursor(NativeBattleIoProfile profile, NativeBattleProfiledIoState state) => checked((int)(state.CursorAddress - profile.ScriptAddress));
    private static ushort Opcode(NativeBattleIoProfile profile, NativeBattleProfiledIoState state) => BinaryPrimitives.ReadUInt16LittleEndian(state.Script.AsSpan().Slice(Cursor(profile, state)));
}
