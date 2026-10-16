using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionImporterTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ff8-junction-import-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(0x10000)]
    [InlineData(0x10001)]
    [InlineData(0x10002)]
    public void ReadsVersionedArchivesWithoutCrossingPayloadBoundaries(int version)
    {
        string path = Archive(version, ("movies/test.cam", 0, "first"u8.ToArray()), ("movies/second.cam", 0, "second"u8.ToArray()));
        JunctionArchive archive = new(path);
        using Stream stream = archive.Find("MOVIES\\TEST.CAM")!.OpenRead();
        byte[] buffer = new byte[100];
        Assert.Equal(5, stream.Read(buffer));
        Assert.Equal("first", Encoding.UTF8.GetString(buffer, 0, 5));
        Assert.Equal(0, stream.Read(buffer));
    }

    [Theory]
    [InlineData(1, "B0FCQ+7z")]
    [InlineData(2, "CQAAAAUAAABdAAAQAAAgkISHJf6Bo///YWQAAA==")]
    public void StreamsOriginalLzsAndLzmaContainerPayloads(int flags, string encoded)
    {
        string path = Archive(0x10002, ("movies/test.cam", flags, Convert.FromBase64String(encoded)));
        JunctionArchiveEntry entry = new JunctionArchive(path).Entries.Single();
        using Stream stream = entry.OpenRead();
        using MemoryStream result = new();
        // One-byte reads exercise LZS matches across calls and the streaming LZMA decoder.
        int value;
        while ((value = stream.ReadByte()) >= 0)
        {
            result.WriteByte((byte)value);
        }
        Assert.Equal("ABCABCABC", Encoding.ASCII.GetString(result.ToArray()));
        Assert.Equal(flags == 2 ? 9L : null, entry.Length);
    }

    [Fact]
    public void FollowsDirectoryRedirectAndRejectsCycles()
    {
        string path = Archive(0x10002, ("movies/test.cam", 0, [1, 2, 3]));
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite))
        using (BinaryWriter writer = new(stream))
        {
            stream.Position = stream.Length;
            int redirect = checked((int)stream.Position);
            writer.Write(-1);
            writer.Write(16L);
            stream.Position = 12;
            writer.Write(redirect);
        }
        Assert.Single(new JunctionArchive(path).Entries);
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite))
        using (BinaryWriter writer = new(stream))
        {
            stream.Position = stream.Length - 8;
            writer.Write(stream.Length - 12);
        }
        Assert.Throws<InvalidDataException>(() => new JunctionArchive(path));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("C:\\escape")]
    [InlineData("movies//double")]
    [InlineData("movies/alias.")]
    [InlineData("movies/alias ")]
    public void RejectsUnsafeArchivePaths(string name)
    {
        string path = Archive(0x10002, (name, 0, [1]));
        Assert.Throws<InvalidDataException>(() => new JunctionArchive(path));
    }

    [Fact]
    public void RejectsCaseInsensitiveDuplicatePaths()
    {
        string path = Archive(0x10002, ("movies/test.cam", 0, [1]), ("MOVIES/test.cam", 0, [2]));
        Assert.Throws<InvalidDataException>(() => new JunctionArchive(path));
    }

    [Fact]
    public void RejectsOutOfBoundsAndDirectoryOverlappingPayloads()
    {
        foreach (long offset in new[] { 16L, long.MaxValue })
        {
            string path = Archive(0x10002, ("x", 0, [1]));
            using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite))
            using (BinaryWriter writer = new(stream))
            {
                stream.Position = 20 + 4 + 2 + 4;
                writer.Write(offset);
            }
            Assert.Throws<InvalidDataException>(() => new JunctionArchive(path));
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(65536)]
    public void RejectsUnknownArchiveFlags(int flags)
    {
        string path = Archive(0x10002, ("movies/test.cam", flags, [1]));
        Assert.Throws<NotSupportedException>(() => new JunctionArchive(path));
    }

    [Fact]
    public void AppliesDefaultOptionsAndPreservesLocalizedLogicalPaths()
    {
        string path = Package("""
            <ModInfo>
              <Name>Actual title</Name>
              <ConfigOption><ID>Enabled</ID><Type>Bool</Type><Default>1</Default></ConfigOption>
              <ModFolder Folder="Inactive" ActiveWhen="Enabled = 0" />
              <ModFolder Folder="CRF" ActiveWhen="Enabled = 1" />
              <FFNxConfig><enable_ffmpeg_videos Enabled="1">1</enable_ffmpeg_videos></FFNxConfig>
            </ModInfo>
            """, ("CRF/movies/test.cam", [1]), ("CRF/lang-en/movies/ending.avi", [2]), ("Inactive/movies/test.cam", [3]), ("movies/root.cam", [4]));
        JunctionModPackage package = JunctionModImporter.Open(path);
        Assert.Equal("Actual title", package.Name);
        Assert.Equal(new[] { "movies/test.cam", "lang-en/movies/ending.avi" }, package.Files.Select(file => file.RelativePath));
        Assert.Equal("CRF/movies/test.cam", package.Files[0].SourcePath);
        Assert.Single(package.Diagnostics);
        using MemoryStream bytes = new();
        package.Files[1].CopyTo(bytes);
        Assert.Equal(new byte[] { 2 }, bytes.ToArray());
    }

    [Fact]
    public void RootFallbackAndFolderPackagesUseTheSameStaticManifestContract()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "mod.xml"), "<ModInfo><Name>Folder title</Name><PreviewFile>picture.png</PreviewFile></ModInfo>");
        File.WriteAllBytes(Path.Combine(directory, "picture.png"), [0]);
        Directory.CreateDirectory(Path.Combine(directory, "movies"));
        File.WriteAllBytes(Path.Combine(directory, "movies", "test.cam"), [1, 2]);
        JunctionModPackage package = JunctionModImporter.Open(directory);
        Assert.Equal("Folder title", package.Name);
        JunctionReplacement file = Assert.Single(package.Files);
        Assert.Equal("movies/test.cam", file.RelativePath);
        Assert.Equal("movies/test.cam", file.SourcePath);
    }

    [Theory]
    [InlineData("<LoadLibrary>bad.dll</LoadLibrary>")]
    [InlineData("<LoadPrograms />")]
    [InlineData("<Conditional Folder='Dynamic' />")]
    [InlineData("<Compatibility />")]
    [InlineData("<OrderConstraints />")]
    [InlineData("<Variable Name='Counter'>1</Variable>")]
    [InlineData("<UnknownBehavior />")]
    [InlineData("<FFNxConfig><enable_vsync>1</enable_vsync></FFNxConfig>")]
    public void RejectsUnsupportedManifestBehaviorRegardlessOfDisabledFolder(string behavior)
    {
        string path = Package($"<ModInfo><Name>Disabled</Name><ModFolder Folder='No Change' />{behavior}</ModInfo>");
        Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(path));
    }

    [Theory]
    [InlineData("hext/patch.txt")]
    [InlineData("Inactive/helper.exe")]
    [InlineData("library.dll")]
    public void RejectsExecutablePayloadsEvenWhenTheirFolderIsNotSelected(string name)
    {
        string path = Package("<ModInfo><ModFolder Folder='No Change' /></ModInfo>", (name, [1]));
        Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(path));
    }

    [Theory]
    [InlineData("<GameLanguage>EN</GameLanguage>", true)]
    [InlineData("<GameLanguage>FR, EN</GameLanguage>", true)]
    [InlineData("<GameLanguage>ANY</GameLanguage>", true)]
    [InlineData("<GameLanguage>FR</GameLanguage>", false)]
    public void AppliesEnglishLanguageCompatibility(string language, bool accepted)
    {
        string path = Package($"<ModInfo>{language}</ModInfo>", ("movies/test.cam", [1]));
        if (accepted)
        {
            Assert.Single(JunctionModImporter.Open(path).Files);
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(path));
        }
    }

    [Fact]
    public void EvaluatesNestedDefaultConditionsAndDoesNotHideUnsupportedBranches()
    {
        string option = "<ConfigOption><ID>Enabled</ID><Type>Bool</Type><Default>1</Default></ConfigOption>";
        string condition = "<ActiveWhen><And><Option>Enabled &gt;= 1</Option><Not><Option>Enabled != 1</Option></Not><GameLanguage>EN</GameLanguage></And></ActiveWhen>";
        string path = Package($"<ModInfo>{option}<ModFolder Folder='CRF'>{condition}</ModFolder></ModInfo>", ("CRF/movies/test.cam", [1]));
        Assert.Equal("movies/test.cam", Assert.Single(JunctionModImporter.Open(path).Files).RelativePath);
        path = Package($"<ModInfo>{option}<ModFolder Folder='CRF'><ActiveWhen><Or><Option>Enabled = 1</Option><Unknown /></Or></ActiveWhen></ModFolder></ModInfo>");
        Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(path));
    }

    private string Package(string manifest, params (string Name, byte[] Content)[] files)
    {
        return Archive(0x10002, new[] { ("mod.xml", 0, Encoding.UTF8.GetBytes(manifest)) }.Concat(files.Select(file => (file.Name, 0, file.Content))).ToArray());
    }

    private string Archive(int version, params (string Name, int Flags, byte[] Content)[] files)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".iroj");
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.Unicode);
        writer.Write(0x534f5249);
        writer.Write(version);
        writer.Write(0);
        writer.Write(16);
        writer.Write(files.Length);
        int fixedSize = version == 0x10000 ? 16 : 20;
        long offset = 20 + files.Sum(file => fixedSize + Encoding.Unicode.GetByteCount(file.Name));
        foreach ((string name, int flags, byte[] content) in files)
        {
            byte[] encoded = Encoding.Unicode.GetBytes(name);
            writer.Write((ushort)(fixedSize + encoded.Length));
            writer.Write((ushort)encoded.Length);
            writer.Write(encoded);
            writer.Write(flags);
            if (version == 0x10000)
            {
                writer.Write(checked((int)offset));
            }
            else
            {
                writer.Write(offset);
            }
            writer.Write(content.Length);
            offset += content.Length;
        }
        foreach ((string _, int _, byte[] content) in files)
        {
            writer.Write(content);
        }
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
