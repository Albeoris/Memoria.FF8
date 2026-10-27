using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect30State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleNodeSource? ExecutableBytes, [property: JsonRequired] bool ColdSourcePending)
{
    public const uint BaseAddress = 0x26D40B8;
    public const int Length = 0x2988;
    private static readonly (uint Address, int Length)[] Regions = [(0x26D40B8, 10), (0x26D40D8, 8), (0x26D4768, 2), (0x26D476C, 4), (0x26D5E00, 8), (0x26D6A08, 16), (0x26D6A20, 4), (0x26D6A28, 20)];

    public static NativeBattleEffect30State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true);
    public static NativeBattleEffect30State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, false);

    public void Validate()
    {
        Globals.Validate();
        Globals.ValidateExtent(Length);
        if (ExecutableBytes is { } bytes && (bytes.IsDefault || bytes.Length != NativeBattleEffect30Profile.SourceLength) || ColdSourcePending && ExecutableBytes is not null)
        {
            throw new InvalidDataException("Effect30 retained globals or addressed node bank leave their physical owner.");
        }
    }

    public NativeBattleEffect30State InitializeSource(NativeBattleEffect30Resources resources)
    {
        using NativeBattleNodeInitialization<NativeBattleEffect30State> initialization = PrepareInitializeSource(resources);
        return initialization.Publish();
    }

    internal NativeBattleNodeInitialization<NativeBattleEffect30State> PrepareInitializeSource(NativeBattleEffect30Resources resources)
    {
        Validate();
        resources.Validate();
        NativeBattleNodeSource bytes = ExecutableBytes ?? (ColdSourcePending ? new(resources.Executable.Data) : throw new NotSupportedException("Effect30 requires its retained node bank before first-use fixups."));
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(bytes.Arena ?? throw new InvalidDataException("Effect30 executable requires its addressed arena owner."));
        NativeBattleEffect30State initialized = this with { ExecutableBytes = bytes, ColdSourcePending = false };
        if (bytes.ReadUInt32(0) != 0)
        {
            return new(initialized, null);
        }
        NativeBattleNodeBankOperation operation = new(bytes);
        try
        {
            NativeBattleEffectNodeBankFixups.Relocate(operation, NativeBattleEffect30Profile.SourceAddress);
            operation.WriteUInt32(0, 1);
            return new(initialized, operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }

    public uint ReadDword(uint address) => Globals.ReadUInt32(Require(address, 4));
    public ushort ReadWord(uint address) => Globals.ReadUInt16(Require(address, 2));
    public NativeBattleEffect30State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }
    public NativeBattleEffect30State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public NativeBattleResourcePointer? ReadResourcePointer(uint address) => Globals.ReadResourcePointer(Require(address, 4));
    public NativeBattleEffect30State WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        Globals.WriteResourcePointer(Require(address, 4), pointer);
        return this;
    }

    private static int Require(uint address, int length)
    {
        if (length < 0 || !Regions.Any(region => address >= region.Address && (ulong)address + (uint)length <= (ulong)region.Address + (uint)region.Length))
        {
            throw new InvalidDataException("Effect30 access leaves its selected global fields.");
        }
        return checked((int)(address - BaseAddress));
    }
}
