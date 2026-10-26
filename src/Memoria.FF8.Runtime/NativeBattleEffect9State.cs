using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint BaseAddress = 0x274EFE8;
    public const int GlobalLength = 0x9514;
    public const uint ControlAddress = 0x2757FC8;
    public const int ControlLength = 0x54;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } =
    [
        new(0x274F220, 0x274F158, 100, 2),
        new(0x2757450, 0x2757460, 88, 4),
        new(0x274F128, 0x274F050, 72, 3),
        new(0x27571A0, 0x27565F0, 996, 3),
        new(0x274F230, 0x274F240, 124, 8),
        new(0x274F628, 0x27553F8, 92, 50),
        new(0x2757440, 0x27571C0, 64, 10),
        new(0x2758160, 0x2758020, 320, 1)
    ];

    public static NativeBattleEffect9State FromColdProcess()
    {
        return new(NativeBattleEffectArena.Clear());
    }

    public static NativeBattleEffect9State FromUnknown()
    {
        return new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalLength));
    }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Globals);
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            RequireRange(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Globals.UnknownRegions)
        {
            RequireRange(checked(BaseAddress + (uint)region.Offset), region.Length);
        }

    }

    public byte[] Read(uint address, int length)
    {
        RequireRange(address, length);
        return Globals.Read(checked((int)(address - BaseAddress)), length);
    }

    public NativeBattleEffect9State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        RequireRange(address, bytes.Length);
        return this with { Globals = Globals.Write(checked((int)(address - BaseAddress)), bytes) };
    }

    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect9State WriteWord(uint address, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    public NativeBattleEffect9State WriteDword(uint address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Write(address, bytes);
    }

    private static void RequireRange(uint address, int length)
    {
        if (length < 0 || address < BaseAddress || (ulong)address + (uint)length > (ulong)BaseAddress + GlobalLength)
        {
            throw new InvalidDataException("Effect9 global bytes leave their physical owner.");
        }
    }
}
