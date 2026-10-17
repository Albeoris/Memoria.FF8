using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Original17D5F4C bytes before87EA20's in-place pointer fixups.</summary>
public sealed record NativeBattleCuragaExecutableSource(ImmutableArray<byte> Data);

/// <summary>The actual retained initialization DWORD and lazily materialized Curaga executable bank. Missing old state remains unknown.</summary>
public sealed record NativeBattleCuragaExecutableState([property: JsonRequired] uint InitializationFlag, [property: JsonRequired] ImmutableArray<byte>? Data)
{
    public const uint Address = 0x17D5F4C;
    public const int Length = 0x81B4;

    public static NativeBattleCuragaExecutableState FromColdProcess() => new(0, null);

    public void Validate()
    {
        if (Data is { } bytes && (bytes.IsDefault || bytes.Length != Length || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) != InitializationFlag))
        {
            throw new InvalidDataException("Native Curaga executable bank contradicts its retained initialization DWORD or original extent.");
        }
    }

    /// <summary>87E230 calls87EA20 only while the retained resourceDWORD is0, then writes1.</summary>
    public NativeBattleCuragaExecutableState Initialize87EA20(NativeBattleCuragaExecutableSource source)
    {
        Validate();
        if (InitializationFlag != 0)
        {
            if (Data is null)
            {
                throw new NotSupportedException("Native87EA20 requires the previously fixed executable bytes; a nonzero retained flag cannot recreate them.");
            }
            return this;
        }
        if (source.Data.IsDefault || source.Data.Length != Length || BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native Curaga first-use materialization requires the authenticated uninitialized17D5F4C resource.");
        }
        byte[] data = NativeBattleEffectNodeBankFixups.Relocate(Data ?? source.Data, Address);
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        return new(1, data.ToImmutableArray());
    }

    public byte[] Read(uint address, int count)
    {
        Validate();
        ImmutableArray<byte> bytes = Data ?? throw new NotSupportedException("Native Curaga requires its materialized executable resource bytes.");
        if (address < Address || count < 0 || address - Address > Length || count > Length - (address - Address))
        {
            throw new InvalidDataException("Native Curaga executable read leaves17D5F4C's original resource.");
        }
        return bytes.AsSpan().Slice(checked((int)(address - Address)), count).ToArray();
    }
}
