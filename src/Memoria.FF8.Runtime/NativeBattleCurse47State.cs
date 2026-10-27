using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleCurse47State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleCurse47Step(NativeBattleCurse47Frame Frame, int Result);
public abstract record NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47ObservedMode(uint DirectorAddress, short Mode) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47Sound(uint Value, int Pan) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47Pause(int Value) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47DirectorInputs(uint SoundValue, Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> Execute);

public sealed record NativeBattleCurse47State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? IoScript)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? NodeProgram { get; init; }

    public const uint BaseAddress = 0x264B3C8;
    public const int GlobalLength = 0x3894;
    public const uint SharedAddress = 0x264E6B0;
    public const int SharedLength = 84;

    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } =
    [
        new(0x264B5A8, 0x264B4E0, 100, 2),
        new(0x264E528, 0x264E538, 88, 4),
        new(0x264B4B0, 0x264B3D8, 72, 3),
        new(0x264E288, 0x264DFE0, 676, 1),
        new(0x264DFC8, 0x264D830, 648, 3),
        new(0x264B5B8, 0x264B5C8, 176, 50),
        new(0x264E518, 0x264E298, 64, 10),
        new(0x264EAC8, 0x264E718, 48, 2)
    ];

    public static NativeBattleCurse47State FromColdProcess(ImmutableArray<byte> script)
    {
        NativeBattleCurse47State state = new(NativeBattleEffectArena.Clear(), script) { NodeProgram = new(0, null) { ResourceAddress = NativeBattleCurse47Nodes.SourceAddress } };
        state.Validate();
        return state;
    }

    public static NativeBattleCurse47State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalLength), null);

    public void Validate()
    {
        NodeProgram?.Validate();
        if (NodeProgram is { } program && program.ResourceAddress != NativeBattleCurse47Nodes.SourceAddress)
        {
            throw new InvalidDataException("Curse node state belongs to another original executable bank.");
        }
        ArgumentNullException.ThrowIfNull(Globals);
        Globals.Validate();
        if (IoScript is { } script && (script.IsDefault || script.Length != NativeBattleCurse47Io.ScriptLength))
        {
            throw new InvalidDataException("Curse requires its complete retained IO script.");
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            RequireRange(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateUnknownRanges())
        {
            if (region.Offset < 0 || region.Length < 0 || region.Offset > GlobalLength - region.Length)
            {
                throw new InvalidDataException("Curse unknown global bytes exceed their physical owner.");
            }
        }
    }

    public byte[] Read(uint address, int length)
    {
        RequireRange(address, length);
        return Globals.Read(checked((int)(address - BaseAddress)), length);
    }

    public NativeBattleCurse47State Write(uint address, ReadOnlySpan<byte> data)
    {
        RequireRange(address, data.Length);
        Globals.Write(checked((int)(address - BaseAddress)), data);
        return this;
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        RequireRange(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Globals, checked((int)(address - BaseAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleCurse47State WriteView(uint address, NativeBattleEffectArena view, int length)
    {
        RequireRange(address, length);
        view.Validate();
        NativeBattleEffectNodeContextMemory.Copy(view, 0, Globals, checked((int)(address - BaseAddress)), length);
        return this;
    }

    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleCurse47State WriteWord(uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public NativeBattleCurse47State WriteDword(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public ushort SharedWord(int offset)
    {
        RequireShared(offset);
        return ReadWord(SharedAddress + (uint)offset);
    }

    public NativeBattleCurse47State WithSharedWord(int offset, ushort value)
    {
        RequireShared(offset);
        return WriteWord(SharedAddress + (uint)offset, value);
    }

    internal NativeBattleProfiledIoState ReadIo()
    {
        if (ReadDword(NativeBattleCurse47Io.ControlPointerGlobal) != NativeBattleCurse47Io.ControlAddress)
        {
            throw new NotSupportedException("Curse IO requires its executed control allocation.");
        }
        return new(Read(NativeBattleCurse47Io.ControlAddress, 364).ToImmutableArray(), IoScript ?? throw new NotSupportedException("Curse IO script bytes are unresolved."), ReadDword(NativeBattleCurse47Io.CursorGlobal));
    }

    internal NativeBattleCurse47State WithIo(NativeBattleProfiledIoState state)
    {
        NativeBattleProfiledIoCore.Validate(NativeBattleCurse47Io.Profile, state);
        return Write(NativeBattleCurse47Io.ControlAddress, state.Control.AsSpan()).WriteDword(NativeBattleCurse47Io.CursorGlobal, state.CursorAddress) with { IoScript = state.Script };
    }

    public ushort ReadIoControlWord(int offset)
    {
        if (offset < 0 || offset > 362)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        _ = ReadIo();
        return ReadWord(NativeBattleCurse47Io.ControlAddress + (uint)offset);
    }

    private static void RequireShared(int offset)
    {
        if (offset < 0 || offset > SharedLength - 2)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }

    private static void RequireRange(uint address, int length)
    {
        if (length < 0 || address < BaseAddress || (ulong)address + (uint)length > (ulong)BaseAddress + GlobalLength)
        {
            throw new InvalidDataException("Curse global access exceeds its retained owner.");
        }
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            if (Overlaps(pool.Descriptor, 16) || Overlaps(pool.Bank, checked(pool.Stride * pool.Capacity)))
            {
                throw new InvalidDataException("Curse tasks and descriptors require their canonical hierarchy owner.");
            }
        }
        bool Overlaps(uint start, int count) => length != 0 && address < (ulong)start + (uint)count && start < (ulong)address + (uint)length;
    }
}
