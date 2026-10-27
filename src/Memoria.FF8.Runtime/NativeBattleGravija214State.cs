using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGravija214State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena CopiedSource, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 12), (ParametersGlobal, 4), (CursorGlobal, 4), (MatrixAddress, 32)];

    public static NativeBattleGravija214State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleGravija214State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, EntryCopyLength), NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateUnknownRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        ValidateBounded(CopiedSource, EntryCopyLength);
        if (RootBankStorage is { } bank)
        {
            bank.Validate();
            bank.ValidateExtent(RootBankLength);
        }
    }

    public NativeBattleGravija214State CopyEntrySnapshot(NativeBattleGravija214Resources resources)
    {
        if (resources.EntryCopySeed.Data.IsDefault || resources.EntryCopySeed.Data.Length != EntryCopyLength)
        {
            throw new InvalidDataException("Gravija entry requires its complete original MOVSD source.");
        }
        return this with { CopiedSource = NativeBattleEffectArena.Clear().Write(0, resources.EntryCopySeed.Data.AsSpan()) };
    }

    public byte[] Read(uint address, int length) => IsCopy(address, length) ? CopiedSource.Read(checked((int)(address - CopiedMeshAddress)), length) : Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleGravija214State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (IsCopy(address, bytes.Length))
        {
            CopiedSource.Write(checked((int)(address - CopiedMeshAddress)), bytes);
        }
        else
        {
            Globals.Write(Require(address, bytes.Length), bytes);
        }
        return this;
    }
    public NativeBattleGravija214State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleGravija214State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleGravija214State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(IsCopy(address, length) ? CopiedSource : Globals, IsCopy(address, length) ? checked((int)(address - CopiedMeshAddress)) : Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleGravija214State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsCopy(address, length))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, CopiedSource, checked((int)(address - CopiedMeshAddress)), length);
        }
        else
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        }
        return this;
    }

    private static bool IsCopy(uint address, int length) => length >= 0 && address >= CopiedMeshAddress && (ulong)address + (uint)length <= (ulong)CopiedMeshAddress + EntryCopyLength;

    private static void ValidateBounded(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Gravija globals exclude module banks, descriptors, COMMON storage and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
