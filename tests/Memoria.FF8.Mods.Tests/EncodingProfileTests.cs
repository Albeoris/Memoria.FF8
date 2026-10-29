using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tdw;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources.Text;
using ImageMagick;

namespace Memoria.FF8.Mods.Tests;

public sealed class EncodingProfileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-encoding-" + Guid.NewGuid().ToString("N"));
    private static readonly NativeTextContext Context = new("synthetic", "field", "map", "en");

    [Fact]
    public void MixedScriptsUseIndependentImageAndNativeByteIdentities()
    {
        NativeFontProfile profile = Profile();
        NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, "AЯ日😀", Context);
        Assert.True(layout.Success);
        Assert.Equal(new[] { "latin", "cyrillic", "japanese", "latin" }, layout.Glyphs.Select(run => run.Image.AtlasId));
        Assert.Equal(new[] { 7, 123, 500, 900 }, layout.Glyphs.Select(run => run.Image.Index));
        Assert.Equal(new float[] { 0, 3, 8, 15 }, layout.Glyphs.Select(run => run.X));
        Assert.Equal(19, layout.Width);
        Assert.Equal(2, layout.Glyphs[^1].TextLength);
        NativeEncodedText encoded = NativeStrictCodec.Encode(profile, "AЯ日😀", Context);
        Assert.True(encoded.Success);
        Assert.Equal(new byte[] { 0x45, 0x80, 0x19, 0x21, 0x19, 0x22 }, encoded.Bytes);
    }

    [Fact]
    public void EnabledProfilesDoNotLeakCharacterOrByteMappings()
    {
        NativeFontProfile first = Profile();
        NativeFontProfile second = first with { Id = "second", Codec = [new("45", "Я")], Glyphs = [new("Я", 7)] };
        Assert.Equal("A", NativeStrictCodec.Decode(first, [0x45], Context).Text);
        Assert.Equal("Я", NativeStrictCodec.Decode(second, [0x45], Context).Text);
        Assert.False(NativeStrictCodec.Encode(second, "A", Context).Success);
        Assert.False(NativeFontLayout.Layout(second, "A", Context).Success);
    }

    [Fact]
    public void StrictFailuresHaveExactPositionsAndExposeNoPartialOutput()
    {
        NativeEncodedText result = NativeStrictCodec.Encode(Profile(), "A☃{Page}", Context);
        Assert.False(result.Success);
        Assert.Empty(result.Bytes);
        Assert.Equal(new[] { 1, 2 }, result.Diagnostics.Select(diagnostic => diagnostic.Position));
        Assert.Equal(new[] { 1, 6 }, result.Diagnostics.Select(diagnostic => diagnostic.Length));
        Assert.Contains(NativeStrictCodec.Decode(Profile(), [0x19], Context).Diagnostics, diagnostic => diagnostic.Code == "unknown-sequence" && diagnostic.Position == 0);
        Assert.Contains(NativeStrictCodec.Encode(Profile(), "AA", Context, maximumBytes: 1).Diagnostics, diagnostic => diagnostic.Code == "byte-limit" && diagnostic.Position == 1);
    }

    [Fact]
    public void AliasesPreserveBytesAndCanonicalEncodingIsExplicit()
    {
        NativeFontProfile profile = Profile() with { Codec = [new("45", "A"), new("46", "A", Canonical: false)] };
        NativeDecodedText decoded = NativeStrictCodec.Decode(profile, [0x46, 0x45], Context);
        Assert.True(decoded.Success);
        Assert.Equal("AA", decoded.Text);
        Assert.Equal(new byte[] { 0x46, 0x45 }, decoded.OriginalBytes());
        Assert.Equal(new byte[] { 0x45, 0x45 }, NativeStrictCodec.Encode(profile, decoded.Text, Context).Bytes);
        Assert.Contains(NativeFontProfileValidation.Validate(profile with { Codec = [new("45", "A"), new("46", "A")] }), diagnostic => diagnostic.Code == "canonical");
    }

    [Fact]
    public void FieldTablesAndLogicalPageControlsAreExplicit()
    {
        NativeFontProfile profile = Profile() with { Codec = [new("1C20", " ", Field: "map"), new("1C20", "日", Field: "other")], Glyphs = [new(" ", 7, "map"), new(" ", 123, "other")] };
        Assert.Equal(" ", NativeStrictCodec.Decode(profile, [0x1C, 0x20], Context).Text);
        Assert.Equal("日", NativeStrictCodec.Decode(profile, [0x1C, 0x20], Context with { Field = "other" }).Text);
        Assert.False(NativeStrictCodec.Decode(profile, [0x1C, 0x20], Context with { Field = "missing" }).Success);
        Assert.Equal(7, NativeFontLayout.Layout(profile, " ", Context).Glyphs.Single().Image.Index);
        NativeFontLayoutResult pages = NativeFontLayout.Layout(Profile(), "A\nЯ{Next}日{End}", Context);
        Assert.True(pages.Success);
        Assert.Equal(2, pages.Pages);
        Assert.Equal(new[] { 0, 0, 1 }, pages.Glyphs.Select(run => run.Page));
        Assert.Equal(new float[] { 0, 12, 0 }, pages.Glyphs.Select(run => run.Y));
    }

    [Fact]
    public void OverflowAndUnknownControlsNeverTriggerSubstitutionOrReflow()
    {
        NativeFontLayoutResult result = NativeFontLayout.Layout(Profile(), "AA☃{Name}", Context, maximumWidth: 3);
        Assert.Equal(2, result.Glyphs.Count);
        Assert.Equal(new float[] { 0, 3 }, result.Glyphs.Select(run => run.X));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "overflow" && diagnostic.Position == 1);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "missing-glyph" && diagnostic.Position == 2);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "missing-glyph" && diagnostic.Position == 3 && diagnostic.Length == 6);
    }

    [Fact]
    public void RejectsControlReassignmentAndEscapedSequencePrefixAmbiguity()
    {
        NativeFontProfile profile = Profile() with { ReservedPrefixes = ["03"], Codec = [new("0301", "A"), new("19", "Я"), new("1921", "日")] };
        var issues = NativeFontProfileValidation.Validate(profile);
        Assert.Contains(issues, diagnostic => diagnostic.Code == "reserved-control");
        Assert.Contains(issues, diagnostic => diagnostic.Code == "sequence-conflict");
    }

    [Fact]
    public void NativeCompatibilityRequiresSupportedEditionResourceAndDictionary()
    {
        Assert.Contains(NativeStrictCodec.Encode(Profile(), "A", Context with { DictionaryLanguage = "jp" }).Diagnostics, diagnostic => diagnostic.Code == "native-domain");
        Assert.Contains(NativeStrictCodec.Encode(Profile() with { Domains = [] }, "A", Context).Diagnostics, diagnostic => diagnostic.Code == "native-domain");
    }

    [Fact]
    public void ExportIncludesOnlyEnabledCompleteSelfContainedClosures()
    {
        string input = WriteProfile("input", PackageProfile());
        string mod = Path.Combine(root, "mod");
        IReadOnlyList<string> exported = EncodingProfilePackage.Export([new(input, "Mixed"), new("missing", "Disabled", false)], mod);
        Assert.Single(exported);
        string path = Path.Combine(mod, "Encoding", "Mixed", "profile.json");
        Assert.True(File.Exists(path));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(input)!, "tables", "dictionary.bin")), File.ReadAllBytes(Path.Combine(mod, "Encoding", "Mixed", "tables", "dictionary.bin")));
        Assert.Empty(NativeFontProfileIO.ValidateDependencies(path, NativeFontProfileIO.Load(path)));
        Assert.False(Directory.Exists(Path.Combine(mod, "Encoding", "Disabled")));
    }

    [Theory]
    [InlineData("../escape.bin")]
    [InlineData("C:/absolute.bin")]
    [InlineData("fonts\\font.png")]
    [InlineData("./font.png")]
    [InlineData("fonts/CON.png")]
    [InlineData("fonts/LPT1")]
    public void RejectsNonportableDependencyPaths(string path)
    {
        Assert.Contains(NativeFontProfileValidation.Validate(Profile() with { Dependencies = [new(path)] }), diagnostic => diagnostic.Code == "dependency-path");
        Assert.Throws<InvalidDataException>(() => NativeFontProfileIO.ResolveDependency(root, path));
    }

    [Fact]
    public void ExportRejectsMissingDependenciesAndCaseCollisionsBeforePublication()
    {
        string input = WriteProfile("input", PackageProfile());
        string mod = Path.Combine(root, "mod");
        Assert.Throws<InvalidDataException>(() => EncodingProfilePackage.Export([new(input, "English"), new(input, "english")], mod));
        File.Delete(Path.Combine(Path.GetDirectoryName(input)!, "tables", "dictionary.bin"));
        Assert.Throws<InvalidDataException>(() => EncodingProfilePackage.Export([new(input, "Mixed")], mod));
        Assert.False(Directory.Exists(Path.Combine(mod, "Encoding", "Mixed")));
    }

    [Fact]
    public void DependencyIntegrityAndAtlasRemappingAreValidated()
    {
        string input = WriteProfile("input", PackageProfile());
        NativeFontProfile profile = NativeFontProfileIO.Load(input);
        Assert.Contains(NativeFontProfileIO.ValidateDependencies(input, profile with { Dependencies = [new("tables/dictionary.bin", new string('0', 64))] }), diagnostic => diagnostic.Code == "integrity");
        Assert.Contains(NativeFontProfileIO.ValidateDependencies(input, profile with { Atlases = [new("tiny", "fonts/tiny.png", 2, 1)] }), diagnostic => diagnostic.Code == "atlas-remapping");
    }

    [Fact]
    public void InstalledEuropeanBuilderUsesTdwWidthsAndPrintableByteMinus20()
    {
        byte[] widths = Enumerable.Range(0, 512).Select(index => (byte)(index % 11)).ToArray();
        NativeFontProfile profile = NativeInstalledFontProfile.CreateEuropean("installed", "Installed", "en", Context, "font.tex", new PixelImage(256, 256), new TdwFile(widths, null).Write());
        NativeGlyphMapping mapping = profile.Glyphs.Single(mapping => mapping.Text == "A");
        NativeCodecMapping bytes = profile.Codec.Single(mapping => mapping.Text == "A" && mapping.Canonical);
        int value = Convert.ToInt32(bytes.Hex, 16);
        Assert.Equal(value - 0x20, mapping.ImageIndex);
        NativeGlyphImage image = profile.Images.Single(image => image.Index == mapping.ImageIndex);
        Assert.Equal(widths[mapping.ImageIndex], image.Advance);
        Assert.Equal(mapping.ImageIndex % 21 * 12, image.X);
        Assert.Equal(mapping.ImageIndex / 21 * 12, image.Y);
        Assert.Empty(NativeFontProfileValidation.Validate(profile));
        Assert.Equal("{Line}", NativeStrictCodec.Decode(profile, [0x02], Context).Text);
        Assert.Equal(new byte[] { 0x02 }, NativeStrictCodec.Encode(profile, "{Line}", Context).Bytes);
        Assert.Equal(16, NativeFontLayout.Layout(profile, "A{Line}A", Context).Glyphs[1].Y);
        NativeEncodedText character = NativeStrictCodec.Encode(profile, "{Char Squall}", Context);
        Assert.True(character.Success);
        Assert.Equal("{Char Squall}", NativeStrictCodec.Decode(profile, character.Bytes, Context).Text);
        Assert.Throws<NotSupportedException>(() => NativeInstalledFontProfile.CreateEuropean("jp", "JP", "jp", Context, "font.tex", new PixelImage(256, 256), new TdwFile(widths, null).Write()));
    }

    [Fact]
    public void FieldOverridesCannotIntroduceHiddenSequenceConflictsOrAmbiguousAliases()
    {
        NativeFontProfile profile = Profile() with { Codec = [new("19", "A"), new("1921", "日", Field: "map")] };
        Assert.Contains(NativeFontProfileValidation.Validate(profile), diagnostic => diagnostic.Code == "sequence-conflict");
        profile = Profile() with { Codec = [new("45", "A"), new("46", "A", Field: "map")] };
        Assert.Contains(NativeFontProfileValidation.Validate(profile), diagnostic => diagnostic.Code == "canonical");
    }

    [Fact]
    public void InvalidProfileJsonAndNullCollectionsFailWithDiagnostics()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.json");
        File.WriteAllText(path, "{\"id\":\"first\",\"id\":\"second\"}");
        Assert.Throws<InvalidDataException>(() => NativeFontProfileIO.Load(path));
        NativeFontProfile invalid = Profile() with { Atlases = null! };
        Assert.Contains(NativeFontProfileValidation.Validate(invalid), diagnostic => diagnostic.Code == "schema");
        Assert.False(NativeFontLayout.Layout(invalid, "A", Context).Success);
        Assert.False(NativeStrictCodec.Encode(invalid, "A", Context).Success);
    }

    private NativeFontProfile PackageProfile() => new()
    {
        Id = "package",
        DisplayName = "Mixed",
        Atlases = [new("tiny", "fonts/tiny.png", 1, 1)],
        Images = [new(123, "tiny", 0, 0, 1, 1, 1)],
        Glyphs = [new("Я", 123)],
        Dependencies = [new("tables/dictionary.bin")]
    };

    private string WriteProfile(string folder, NativeFontProfile profile)
    {
        string directory = Path.Combine(root, folder);
        Directory.CreateDirectory(Path.Combine(directory, "fonts"));
        Directory.CreateDirectory(Path.Combine(directory, "tables"));
        // A generated 1×1 PNG fixture, unrelated to original game assets.
        using (MagickImage pixel = new(MagickColors.White, 1, 1))
        {
            File.WriteAllBytes(Path.Combine(directory, "fonts", "tiny.png"), pixel.ToByteArray(MagickFormat.Png));
        }
        File.WriteAllBytes(Path.Combine(directory, "tables", "dictionary.bin"), [1, 2, 3]);
        string path = Path.Combine(directory, "profile.json");
        NativeFontProfileIO.Save(path, profile);
        return path;
    }

    private static NativeFontProfile Profile() => new()
    {
        Id = "mixed",
        DisplayName = "Mixed synthetic",
        Domains = [new("synthetic", "field", DictionaryLanguage: "en")],
        Atlases = [new("latin", "latin.png", 32, 16), new("cyrillic", "cyrillic.png", 32, 16), new("japanese", "japanese.png", 32, 16)],
        Images = [new(7, "latin", 0, 0, 3, 8, 3), new(123, "cyrillic", 0, 0, 5, 8, 5), new(500, "japanese", 0, 0, 7, 8, 7), new(900, "latin", 8, 0, 4, 8, 4)],
        Glyphs = [new("A", 7), new("Я", 123), new("日", 500), new("😀", 900)],
        Codec = [new("45", "A"), new("80", "Я"), new("1921", "日"), new("1922", "😀"), new("01", "{Next}", Control: true), new("02", "\n", Control: true), new("00", "{End}", Control: true)]
    };

    public void Dispose()
    {
        if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("memoria-encoding-", StringComparison.Ordinal))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
