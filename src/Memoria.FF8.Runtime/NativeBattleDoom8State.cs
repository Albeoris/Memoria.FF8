using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? IoScript)
{
    [JsonRequired]
    public NativeBattleCuragaExecutableState? NodeProgram { get; init; }

    [JsonRequired]
    public ImmutableArray<NativeBattleEffectOwnedModelState>? Models { get; init; }

    [JsonRequired]
    public bool? ReaperSourceLoaded { get; init; }

    public const uint BaseAddress = 0x2758500;
    public const int GlobalLength = 0x767C;
    public const uint SharedAddress = 0x275F240;
    public const int SharedLength = 84;

    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } =
    [
        new(0x2758958, 0x2758890, 100, 2),
        new(0x275E498, 0x275E6C8, 88, 4),
        new(0x2758860, 0x2758788, 72, 3),
        new(0x275E1F8, 0x275DF50, 676, 1),
        new(0x275DF38, 0x275D230, 1112, 3),
        new(0x2758968, 0x2758978, 176, 50),
        new(0x275ABE0, 0x275CB30, 88, 8),
        new(0x275E488, 0x275E208, 64, 10),
        new(0x275F3D8, 0x275F298, 320, 1),
        new(0x275F918, 0x275F568, 48, 2)
    ];

    public static NativeBattleDoom8State FromColdProcess(ImmutableArray<byte> script)
    {
        NativeBattleDoom8State state = new(NativeBattleEffectArena.Clear(), script) { NodeProgram = new(0, null) { ResourceAddress = 0x1818B78 }, Models = [], ReaperSourceLoaded = false };
        state.Validate();
        return state;
    }

    public static NativeBattleDoom8State FromUnknown()
    {
        return new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalLength), null);
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Globals);
        Globals.Validate();
        if (Models is { } models && (models.IsDefault || models.Any(model => model is null || model.TaskAddress == 0 || model.ResourceAddress == 0) || models.Select(model => model.TaskAddress).Distinct().Count() != models.Length))
        {
            throw new InvalidDataException("Doom requires distinct addressed effect model owners.");
        }
        if (IoScript is { } script && (script.IsDefault || script.Length != NativeBattleDoom8Io.ScriptLength))
        {
            throw new InvalidDataException("Doom requires its complete retained IO script.");
        }
        if (NodeProgram is { } program)
        {
            program.Validate();
            if (program.ResourceAddress != 0x1818B78)
            {
                throw new InvalidDataException("Doom requires its actual retained node source identity.");
            }
        }
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            RequireRange(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            if (region.Offset < 0 || region.Offset > GlobalLength - region.Length)
            {
                throw new InvalidDataException("Doom unknown global bytes exceed their physical owner.");
            }
        }
    }

    public byte[] Read(uint address, int length)
    {
        RequireRange(address, length);
        return Globals.Read(checked((int)(address - BaseAddress)), length);
    }

    public NativeBattleDoom8State Write(uint address, ReadOnlySpan<byte> data)
    {
        RequireRange(address, data.Length);
        return this with { Globals = Globals.Write(checked((int)(address - BaseAddress)), data) };
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        RequireRange(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Globals, checked((int)(address - BaseAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleDoom8State WriteView(uint address, NativeBattleEffectArena view, int length)
    {
        RequireRange(address, length);
        view.Validate();
        return this with { Globals = NativeBattleEffectNodeContextMemory.Copy(view, 0, Globals, checked((int)(address - BaseAddress)), length) };
    }

    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleDoom8State WriteWord(uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public NativeBattleDoom8State WriteDword(uint address, uint value)
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

    public NativeBattleDoom8State WithSharedWord(int offset, ushort value)
    {
        RequireShared(offset);
        return WriteWord(SharedAddress + (uint)offset, value);
    }

    public NativeBattleDoom8State InitializeIo()
    {
        ImmutableArray<byte> script = IoScript ?? throw new NotSupportedException("Doom IO initialization requires its retained script bytes.");
        NativeBattleDoom8IoInitialization initialized = NativeBattleDoom8Io.Initialize8C4E60(script, ReadDword(NativeBattleDoom8Io.NextAddressGlobal));
        return WriteDword(NativeBattleDoom8Io.ControlPointerGlobal, NativeBattleDoom8Io.ControlAddress)
            .WriteDword(NativeBattleDoom8Io.NextAddressGlobal, initialized.NextAddress)
            .WithIo(initialized.State);
    }

    internal ushort ReadIoControlWord(int offset)
    {
        if (offset < 0 || offset > 0x16A)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        RequireIoControl();
        return ReadWord(NativeBattleDoom8Io.ControlAddress + (uint)offset);
    }

    private void RequireIoControl()
    {
        if (ReadDword(NativeBattleDoom8Io.ControlPointerGlobal) != NativeBattleDoom8Io.ControlAddress)
        {
            throw new NotSupportedException("Doom IO requires its executed control allocation.");
        }
    }

    internal NativeBattleProfiledIoState ReadIo()
    {
        RequireIoControl();
        return new(Read(NativeBattleDoom8Io.ControlAddress, 0x16C).ToImmutableArray(), IoScript ?? throw new NotSupportedException("Doom IO script bytes are unresolved."), ReadDword(NativeBattleDoom8Io.CursorGlobal));
    }

    internal NativeBattleDoom8State WithIo(NativeBattleProfiledIoState io)
    {
        NativeBattleProfiledIoCore.Validate(NativeBattleIoProfile.Doom8, io);
        return Write(NativeBattleDoom8Io.ControlAddress, io.Control.AsSpan()).WriteDword(NativeBattleDoom8Io.CursorGlobal, io.CursorAddress) with { IoScript = io.Script };
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
            throw new InvalidDataException("Doom global access exceeds its retained owner.");
        }
        foreach (NativeBattleCureFamilyPool pool in Pools)
        {
            if (Overlaps(pool.Descriptor, 16) || Overlaps(pool.Bank, checked(pool.Stride * pool.Capacity)))
            {
                throw new InvalidDataException("Doom tasks and descriptors require their canonical hierarchy owner.");
            }
        }
        bool Overlaps(uint start, int count) => length != 0 && address < (ulong)start + (uint)count && start < (ulong)address + (uint)length;
    }
}
