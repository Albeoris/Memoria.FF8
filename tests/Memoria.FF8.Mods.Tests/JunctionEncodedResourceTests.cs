using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionEncodedResourceTests
{
    [Theory]
    [InlineData("direct/field/model/main_chr/d000.mch", "field/main_chr/d000.mch")]
    [InlineData("DIRECT\\FIELD\\MODEL\\MAIN_CHR\\D065.MCH", "field/main_chr/d065.mch")]
    [InlineData("direct/field/model/main_chr/a000.mch", "field/main_chr/a000.mch")]
    [InlineData("FIELD\\model\\main_chr\\D002.MCH", "field/main_chr/d002.mch")]
    [InlineData("field/model/main_chr/d000.mch/extra", null)]
    [InlineData("direct/field/model/main_chr/d000.mch/extra", null)]
    [InlineData("direct/field/model/main_chr/d00.mch", null)]
    [InlineData("direct/field/model/main_chr/d0x0.mch", null)]
    [InlineData("direct/field/model/main_chr/../d000.mch", null)]
    [InlineData("direct/field/model/second_chr/d000.mch", null)]
    [InlineData("field/main_chr/d000.mch", null)]
    [InlineData("battle/d0c000.dat", "battle/d0c000.dat")]
    [InlineData("DIRECT\\BATTLE\\DAC021.DAT", "battle/dac021.dat")]
    [InlineData("battle/dfc999.dat", "battle/dfc999.dat")]
    [InlineData("battle/dgc000.dat", null)]
    [InlineData("battle/d0w000.dat", null)]
    [InlineData("battle/c0m000.dat", null)]
    [InlineData("battle/a0stg000.x", null)]
    [InlineData("battle/d0c00.dat", null)]
    [InlineData("battle/d0c000.dat/extra", null)]
    [InlineData("direct/battle/../d0c000.dat", null)]
    public void AddressRecognizesNativeMainCharacterLayouts(string path, string? expected)
    {
        Assert.Equal(expected, JunctionEncodedResources.Address(path)?.Path);
    }

    [Fact]
    public void EffectiveFirstCandidateIsFrozenAndRetainsResourceMetadata()
    {
        byte[] bytes = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        ModFile first = new("first", ModFormat.Junction, "direct/field/model/main_chr/d000.mch", 42, () => bytes);
        ModFile second = new("second", ModFormat.Junction, "FIELD/model/main_chr/D000.MCH", 43, () => throw new IOException("Shadowed payload must not be read."));
        ModFile ignored = new("other", ModFormat.Memoria, "direct/field/model/main_chr/d001.mch", 44, () => throw new IOException("Other formats must not be read."));
        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, second, ignored]), out var prepared, out var diagnostics));
        Assert.Empty(diagnostics);
        EncodedResource resource = Assert.Single(prepared);
        Assert.Equal("field/main_chr/d000.mch", resource.Key.Path);
        Assert.Equal(JunctionEncodedResources.MainCharacterFormat, resource.Content.Format);
        Assert.Equal(42, resource.Content.Revision);
        Assert.Equal("first:direct/field/model/main_chr/d000.mch", resource.Content.Provenance);
        string fingerprint = resource.Fingerprint;
        Array.Fill(bytes, (byte)255);
        using Stream stream = resource.Content.OpenRead();
        using MemoryStream actual = new();
        stream.CopyTo(actual);
        Assert.Equal(Enumerable.Range(0, 16).Select(value => (byte)value), actual.ToArray());
        Assert.Equal(fingerprint, resource.Fingerprint);
    }

    [Fact]
    public void BattleBodyAliasesUseFirstFrozenWinnerAndDatFormat()
    {
        byte[] bytes = new byte[16];
        ModFile first = new("first", ModFormat.Junction, "battle/d0c000.dat", 42, () => bytes);
        ModFile second = new("second", ModFormat.Junction, "DIRECT/BATTLE/D0C000.DAT", 43, () => throw new IOException("Shadowed body must not be read."));

        Assert.True(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var prepared, out var diagnostics));

        EncodedResource selected = Assert.Single(prepared);
        Assert.Empty(diagnostics);
        Assert.Equal(new ResourceKey("battle/d0c000.dat"), selected.Key);
        Assert.Equal("dat", selected.Content.Format);
        Assert.Equal(42, selected.Content.Revision);
        bytes[0] = 99;
        using Stream input = selected.Content.OpenRead();
        Assert.Equal(0, input.ReadByte());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidBattleBodyWinnerRejectsAliasesWithoutFallback(bool readFailure)
    {
        ModFile first = new("broken", ModFormat.Junction, "direct/battle/d0c000.dat", 1, () => readFailure ? throw new IOException("Unreadable replacement.") : [1, 2, 3]);
        ModFile second = new("second", ModFormat.Junction, "battle/d0c000.dat", 2, () => throw new IOException("Shadowed body must not be read."));

        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var prepared, out var diagnostics));

        Assert.Empty(prepared);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Junction.InvalidEncodedResource", diagnostic.Code);
        Assert.Equal(first.PackageId, diagnostic.PackageId);
        Assert.Equal(first.RelativePath, diagnostic.RelativePath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidFirstCandidateRejectsPreparationWithOwningDiagnostic(bool readFailure)
    {
        ModFile first = new("broken", ModFormat.Junction, "direct/field/model/main_chr/d000.mch", 1, () => readFailure ? throw new IOException("Unreadable replacement.") : [1, 2, 3]);
        ModFile second = new("second", ModFormat.Junction, "direct/field/model/main_chr/d000.mch", 2, () => throw new IOException("Shadowed payload must not be read."));
        Assert.False(JunctionEncodedResources.TryPrepare(new(1, [], [first, second]), out var prepared, out var diagnostics));
        Assert.Empty(prepared);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Junction.InvalidEncodedResource", diagnostic.Code);
        Assert.Equal("broken", diagnostic.PackageId);
        Assert.Equal(first.RelativePath, diagnostic.RelativePath);
    }

    [Fact]
    public void SelectedDirectResourceFolderRetainsItsNamespace()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-junction-direct-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "direct", "field", "model", "main_chr"));
            File.WriteAllText(Path.Combine(directory, "mod.xml"), "<ModInfo><Name>Direct resources</Name><ModFolder Folder=\"direct/field/model/main_chr\" ActiveWhen=\"1\" /></ModInfo>");
            File.WriteAllBytes(Path.Combine(directory, "direct", "field", "model", "main_chr", "d000.mch"), new byte[16]);
            JunctionReplacement replacement = Assert.Single(JunctionModImporter.Open(directory).Files);
            Assert.Equal("direct/field/model/main_chr/d000.mch", replacement.RelativePath);
            Assert.Equal("field/main_chr/d000.mch", JunctionEncodedResources.Address(replacement.RelativePath)?.Path);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
