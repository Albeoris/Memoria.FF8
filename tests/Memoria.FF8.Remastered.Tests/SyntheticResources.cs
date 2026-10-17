using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Memoria.FF8.Remastered.Tests;

internal sealed class SyntheticResources : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Remastered.Tests", Guid.NewGuid().ToString("N"));

    public SyntheticResources()
    {
        Directory.CreateDirectory(directory);
    }

    public string Write(byte[] bytes)
    {
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public static byte[] Zzz(params (string Name, byte[] Data)[] entries)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.UTF8, true);
        writer.Write((uint)entries.Length);
        ulong offset = (ulong)(4 + entries.Sum(entry => 16 + Encoding.UTF8.GetByteCount(entry.Name)));
        foreach ((string name, byte[] data) in entries)
        {
            WriteText(writer, name);
            writer.Write(offset);
            writer.Write((uint)data.Length);
            offset += (uint)data.Length;
        }
        foreach ((string _, byte[] data) in entries)
        {
            writer.Write(data);
        }
        return bytes.ToArray();
    }

    public static byte[] Compress(byte[] data)
    {
        using MemoryStream bytes = new();
        using (ZLibStream compressor = new(bytes, CompressionLevel.Optimal, true))
        {
            compressor.Write(data);
        }
        return bytes.ToArray();
    }

    public static byte[] Dat(params SerializedSection[] sections)
    {
        using MemoryStream bytes = new();
        using BinaryWriter writer = new(bytes, Encoding.ASCII, true);
        byte[] coff = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(coff, 0x14C);
        BinaryPrimitives.WriteUInt16LittleEndian(coff.AsSpan(2), (ushort)sections.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(coff.AsSpan(16), 224);
        writer.Write(coff);
        byte[] optional = new byte[224];
        BinaryPrimitives.WriteUInt16LittleEndian(optional, 0x10B);
        BinaryPrimitives.WriteUInt32LittleEndian(optional.AsSpan(28), 0x400000);
        BinaryPrimitives.WriteUInt32LittleEndian(optional.AsSpan(56), 0x4000);
        writer.Write(optional);
        writer.Write((uint)sections.Length);
        foreach (SerializedSection section in sections)
        {
            byte[] header = new byte[40];
            Encoding.ASCII.GetBytes(section.HeaderName).CopyTo(header, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)section.Data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), section.Rva);
            writer.Write(header);
            writer.Write(section.ExpandedSize ?? (uint)section.Data.Length);
            byte[] compressed = section.Compressed ?? Compress(section.Data);
            writer.Write((uint)compressed.Length);
            writer.Write(compressed);
            WriteText(writer, section.SerializedName ?? section.HeaderName);
        }
        writer.Write(0u);
        writer.Write(0u);
        return bytes.ToArray();
    }

    public static void WriteText(BinaryWriter writer, string text)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(text);
        writer.Write((uint)encoded.Length);
        writer.Write(encoded);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }
}

internal sealed record SerializedSection(string HeaderName, uint Rva, byte[] Data, uint? ExpandedSize = null, byte[]? Compressed = null, string? SerializedName = null);
