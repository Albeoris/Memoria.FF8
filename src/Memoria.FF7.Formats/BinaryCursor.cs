using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Memoria.FF7.Formats;

internal ref struct BinaryCursor
{
    private readonly ReadOnlySpan<byte> source;
    public BinaryCursor(ReadOnlySpan<byte> source)
    {
        this.source = source;
    }
    public int Position { get; private set; }
    public int Remaining => source.Length - Position;
    public void Seek(int position)
    {
        if ((uint)position > source.Length)
        {
            throw new InvalidDataException("FF7 resource offset is outside the input.");
        }
        Position = position;
    }
    public void Skip(int count)
    {
        Take(count);
    }
    public ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > Remaining)
        {
            throw new InvalidDataException($"FF7 resource is truncated at offset {Position}, requiring {count} bytes.");
        }
        ReadOnlySpan<byte> result = source.Slice(Position, count);
        Position += count;
        return result;
    }
    public byte U8() => Take(1)[0];
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public float F32()
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(Take(4));
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("FF7 resource contains a non-finite coordinate.");
        }
        return value;
    }
    public Vector3 Vector16() => new(I16(), I16(), I16());
    public Vector3 Vector32() => new(I32(), I32(), I32());
    public Vector3 VectorFloat() => new(F32(), F32(), F32());
    public string Ascii(int count)
    {
        ReadOnlySpan<byte> bytes = Take(count);
        int end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
    }
    public int Count32(int minimumRecordBytes)
    {
        int count = I32();
        if (count < 0 || count > Remaining / minimumRecordBytes)
        {
            throw new InvalidDataException("FF7 resource record count exceeds its input.");
        }
        return count;
    }
}
