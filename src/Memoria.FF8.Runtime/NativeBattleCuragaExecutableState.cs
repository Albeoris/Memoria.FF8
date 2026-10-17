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
        byte[] data = (Data ?? source.Data).ToArray();
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int offset = 4 + table * 64 + index * 4;
                uint pointer = Read(offset);
                if (pointer != 0)
                {
                    Write(offset, unchecked(pointer + Address));
                }
            }
        }
        for (int index = 0; index < 16; index++)
        {
            uint pointer = Read(4 + index * 4);
            if (pointer == 0)
            {
                continue;
            }
            if (pointer < Address || pointer - Address > Length - 0x148)
            {
                throw new InvalidDataException("Native87EA20 fixed node leaves the original executable resource.");
            }
            int node = checked((int)(pointer - Address));
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                //Unlike the four directories, the32 node channel pointers are all relocated, including zero values.
                Write(node + field, unchecked(Read(node + field) + Address));
            }
        }
        Write(0, 1);
        return new(1, data.ToImmutableArray());

        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        void Write(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
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
