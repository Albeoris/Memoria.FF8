using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSavePreviewSequenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Preview.Tests", Guid.NewGuid().ToString("N"), "user_123abcd");

    [Fact]
    public void Refresh_uses_unsigned_maximum_across_both_banks_and_increments_source_count()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "slot1_save30.ff8"), Image(9, 0x7FFFFFFF));
        File.WriteAllBytes(Path.Combine(directory, "slot2_save30.ff8"), Image(10, 0x80000000));
        File.WriteAllBytes(Path.Combine(directory, "unrelated.ff8"), Image(50, uint.MaxValue));
        byte[] source = Image(123, 700);
        byte[] before = source.ToArray();
        byte[] refreshed = NativePcSaveCodec.Decode(OriginalSavePreviewSequence.Refresh(source, Path.Combine(directory, "slot1_save23.ff8"))).Block;
        Assert.Equal((ushort)124, BinaryPrimitives.ReadUInt16LittleEndian(refreshed.AsSpan(394)));
        Assert.Equal(0x80000001u, BinaryPrimitives.ReadUInt32LittleEndian(refreshed.AsSpan(460)));
        Assert.Equal(before, source);
    }

    [Fact]
    public void Refresh_wraps_original_word_and_dword_and_ignores_corrupt_slots()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "slot2_save01.ff8"), Image(10, uint.MaxValue));
        File.WriteAllText(Path.Combine(directory, "slot1_save01.ff8"), "invalid compressed image");
        byte[] refreshed = NativePcSaveCodec.Decode(OriginalSavePreviewSequence.Refresh(Image(ushort.MaxValue, 1), Path.Combine(directory, "slot1_save23.ff8"))).Block;
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(refreshed.AsSpan(394)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(refreshed.AsSpan(460)));
    }

    [Fact]
    public void Refresh_starts_serial_at_one_when_no_valid_native_slots_exist()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "slot1_save01.ff8"), [1]);
        byte[] refreshed = NativePcSaveCodec.Decode(OriginalSavePreviewSequence.Refresh(Image(0, 900), Path.Combine(directory, "slot2_save05.ff8"))).Block;
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(refreshed.AsSpan(394)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(refreshed.AsSpan(460)));
    }

    [Fact]
    public void Publication_signs_final_reencoded_bytes_after_refresh()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "slot2_save30.ff8"), Image(10, 77));
        string destination = Path.Combine(directory, "slot1_save23.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        byte[] source = Image(42, 1);
        File.WriteAllBytes(temporary, source);
        OriginalSavePublication.Publish(temporary, destination, overwrite: false, refreshPreview: true);
        byte[] final = File.ReadAllBytes(destination);
        byte[] block = NativePcSaveCodec.Decode(final).Block;
        Assert.Equal((ushort)43, BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(394)));
        Assert.Equal(78u, BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(460)));
        Assert.NotEqual(source, final);
        XDocument metadata = XDocument.Load(Path.Combine(directory, "metadata.xml"));
        XElement entry = metadata.Root!.Elements("savefile").Single(element => (string?)element.Attribute("slot") == "1" && (string?)element.Attribute("num") == "23");
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(final.Concat(Encoding.ASCII.GetBytes("123abcd")).ToArray())), entry.Element("signature")!.Value);
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void Raw_publication_preserves_native_image_bytes_exactly()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "slot2_save30.ff8"), Image(10, 77));
        string destination = Path.Combine(directory, "slot1_save23.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        byte[] source = Image(42, 1);
        File.WriteAllBytes(temporary, source);
        OriginalSavePublication.Publish(temporary, destination, overwrite: false);
        Assert.Equal(source, File.ReadAllBytes(destination));
    }

    private static byte[] Image(ushort count, uint serial)
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(386), 0x08FF);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(394), count);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(460), serial);
        return NativePcSaveCodec.Encode(new NativePcSaveData(block));
    }

    public void Dispose()
    {
        string parent = Path.GetDirectoryName(directory)!;
        if (Directory.Exists(parent))
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
