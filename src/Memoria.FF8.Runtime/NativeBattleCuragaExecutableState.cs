using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Original17D5F4C bytes before87EA20's in-place pointer fixups.</summary>
public sealed record NativeBattleCuragaExecutableSource(ImmutableArray<byte> Data)
{
    public uint ResourceAddress { get; init; } = NativeBattleCuragaExecutableState.Address;
}

/// <summary>The actual retained initialization DWORD and lazily materialized Curaga executable bank. Missing old state remains unknown.</summary>
public sealed record NativeBattleCuragaExecutableState([property: JsonRequired] uint InitializationFlag, [property: JsonRequired] ImmutableArray<byte>? Data)
{
    public const uint Address = 0x17D5F4C;
    public const int Length = 0x81B4;

    public uint ResourceAddress { get; init; } = Address;

    [JsonIgnore]
    public int ByteLength => ResourceAddress switch
    {
        0x17EA4F0 => 0x3D64,
        0x17F37BC => 0x4760,
        0x17C8420 => 0x430C,
        0x178557C or 0x176E194 => 0x3DC8,
        0x17FC68C => 0x5944,
        0x17F7F1C => 0x4770,
        0x1795000 => 0x2CC0,
        0x1744A1C => 0x5138,
        0x1741028 => 0x39F4,
        0x17D5F4C or 0x17AB0E8 => Length,
        _ => throw new InvalidDataException("Native generic node bank has no original module identity.")
    };

    public static NativeBattleCuragaExecutableState FromColdProcess(ushort effect = 28) => new(0, null) { ResourceAddress = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect).ExecutableAddress };

    public void Validate()
    {
        if (ResourceAddress is not (0x17D5F4C or 0x17AB0E8 or 0x17FC68C or 0x17F7F1C or 0x1795000 or 0x1744A1C or 0x1741028 or 0x178557C or 0x176E194 or 0x17C8420 or 0x17F37BC or 0x17EA4F0))
        {
            throw new InvalidDataException("Native Curaga family executable bank has no original module identity.");
        }
        if (Data is { } bytes && (bytes.IsDefault || bytes.Length != ByteLength || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != InitializationFlag))
        {
            throw new InvalidDataException("Native Curaga executable bank contradicts its retained initialization DWORD or original extent.");
        }
    }

    /// <summary>87E230 calls87EA20 only while the retained resourceDWORD is0, then writes1.</summary>
    public NativeBattleCuragaExecutableState Initialize87EA20(NativeBattleCuragaExecutableSource source)
    {
        Validate();
        if (source.ResourceAddress != ResourceAddress)
        {
            throw new InvalidDataException("Native Curaga family source belongs to another executable bank.");
        }
        if (InitializationFlag != 0)
        {
            if (Data is null)
            {
                throw new NotSupportedException("Native87EA20 requires the previously fixed executable bytes; a nonzero retained flag cannot recreate them.");
            }
            return this;
        }
        if (source.Data.IsDefault || source.Data.Length != ByteLength || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native Curaga first-use materialization requires the authenticated uninitialized17D5F4C resource.");
        }
        byte[] data = NativeBattleEffectNodeBankFixups.Relocate(Data ?? source.Data, ResourceAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        return new(1, data.ToImmutableArray()) { ResourceAddress = ResourceAddress };
    }

    public byte[] Read(uint address, int count)
    {
        Validate();
        ImmutableArray<byte> bytes = Data ?? throw new NotSupportedException("Native Curaga requires its materialized executable resource bytes.");
        if (address < ResourceAddress || count < 0 || address - ResourceAddress > ByteLength || count > ByteLength - (address - ResourceAddress))
        {
            throw new InvalidDataException("Native Curaga executable read leaves17D5F4C's original resource.");
        }
        return bytes.AsSpan().Slice(checked((int)(address - ResourceAddress)), count).ToArray();
    }
}
