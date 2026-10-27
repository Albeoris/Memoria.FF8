using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationWorkbookLimitsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.LocalizationWorkbook.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidLargeWorksheetPreservesRolesAndExtensionsBeyondFormerZipAndXmlLimits()
    {
        LocalizationProject source = WriteProject();
        using (ZipArchive archive = ZipFile.Open(WorkbookPath, ZipArchiveMode.Update))
        {
            ZipArchiveEntry original = archive.Entries.Single(entry => entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal));
            string name = original.FullName;
            byte[] xml;
            using (Stream stream = original.Open())
            using (MemoryStream copy = new())
            {
                stream.CopyTo(copy);
                xml = copy.ToArray();
            }
            original.Delete();
            using Stream output = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
            int declarationEnd = xml.AsSpan().IndexOf("?>"u8);
            int insertion = declarationEnd >= 0 ? declarationEnd + 2 : 0;
            output.Write(xml.AsSpan(0, insertion));
            byte[] whitespace = new byte[1024 * 1024];
            Array.Fill(whitespace, (byte)' ');
            for (int block = 0; block < 257; block++)
            {
                TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
                output.Write(whitespace);
            }
            output.Write(xml.AsSpan(insertion));
        }
        LocalizationProject actual = LocalizationProjectIO.Read(root);
        LocalizationRecord expected = Assert.Single(source.Entries);
        LocalizationRecord row = Assert.Single(actual.Entries);
        Assert.Equal(expected.Key, row.Key);
        Assert.Equal(expected.Source, row.Source);
        Assert.Equal(expected.Target, row.Target);
        Assert.Equal(expected.References["Reference [en]"], row.References["Reference [en]"]);
        Assert.True(JsonElement.DeepEquals(expected.ExtensionData["Context"], row.ExtensionData["Context"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateZipNamesHaveTheirOwnDiagnostic(bool changeCase)
    {
        WriteProject();
        using (ZipArchive archive = ZipFile.Open(WorkbookPath, ZipArchiveMode.Update))
        {
            string name = archive.Entries.First().FullName;
            using Stream output = archive.CreateEntry(changeCase ? name.ToUpperInvariant() : name).Open();
            output.Write("duplicate"u8);
        }
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(root));
        Assert.Contains("duplicate ZIP entry", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcessiveDeclaredExpansionIsRejectedBeforeDecompression()
    {
        WriteProject();
        ChangeDeclaredWorksheetLength(_ => 2U * 1024 * 1024 * 1024);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(root));
        Assert.Contains("decompressed content exceeds", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void IncorrectDeclaredEntryLengthsAreRejected(int difference)
    {
        WriteProject();
        ChangeDeclaredWorksheetLength(length => checked((uint)(length + difference)));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => LocalizationProjectIO.Read(root));
        if (difference > 0)
        {
            Assert.Contains("declared length", error.Message, StringComparison.Ordinal);
        }
        else
        {
            // ZipArchive can cap a shortened entry, leaving the XML validator to reject the truncated document.
            Assert.Contains("Invalid Open XML workbook", error.Message, StringComparison.Ordinal);
        }
    }

    private string WorkbookPath => Path.Combine(root, "Localization", "localization.xlsx");

    private LocalizationProject WriteProject()
    {
        LocalizationProject project = new()
        {
            Manifest = new() { SourceLanguage = "jp", SourceColumn = "Source [jp]", TargetLanguage = "ru", ReferenceLanguages = new Dictionary<string, string> { ["Reference [en]"] = "en" } },
            Entries = [new() { Category = "Fields", File = "synthetic", Key = "$en_synthetic_0000", Source = "日本語 😀", Target = "Перевод　", References = new Dictionary<string, string?> { ["Reference [en]"] = "Reference prose" }, ExtensionData = new Dictionary<string, JsonElement> { ["Context"] = JsonSerializer.SerializeToElement(new { Object = 4, Choices = new[] { 1, 2 } }) } }]
        };
        LocalizationProjectIO.WriteNew(project, root, "xlsx", "single", TestContext.Current.CancellationToken);
        return project;
    }

    private void ChangeDeclaredWorksheetLength(Func<uint, uint> change)
    {
        byte[] bytes = File.ReadAllBytes(WorkbookPath);
        int end = bytes.AsSpan().LastIndexOf("PK\u0005\u0006"u8);
        Assert.True(end >= 0);
        int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(end + 16, 4)));
        while (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) == 0x02014B50)
        {
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28, 2));
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 30, 2));
            int commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 32, 2));
            string name = Encoding.UTF8.GetString(bytes, offset + 46, nameLength);
            if (name.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
            {
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 24, 4));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 24, 4), change(length));
                File.WriteAllBytes(WorkbookPath, bytes);
                return;
            }
            offset += 46 + nameLength + extraLength + commentLength;
        }
        Assert.Fail("Synthetic worksheet ZIP directory entry was not found.");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
