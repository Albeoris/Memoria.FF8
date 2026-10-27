using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTextTermTests
{
    [Fact]
    public void QualifiedTermMustUseTheMatchingNativeDictionary()
    {
        NativeTextTerms german = new("de", Enumerable.Range(0, 41).Select(index => "DE" + index));
        Assert.Equal("DE20", DialogueTextFormatter.Format("{TermDE AttackOneEnemy}", terms: german));
        Assert.Throws<InvalidDataException>(() => DialogueTextFormatter.Format("{TermEN Status}", terms: german));
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Format("{Term Status}", terms: german));
    }

    [Theory]
    [InlineData(32, "Entry 0")]
    [InlineData(63, "Entry 31")]
    [InlineData(64, "")]
    [InlineData(65, "")]
    [InlineData(89, "")]
    [InlineData(0, "")]
    [InlineData(31, "")]
    public void NativeOperandSubtracts32AsAByteAndOmitsOnlyOutOfDictionaryEntries(byte operand, string expected)
    {
        NativeTextTerms terms = new("en", Enumerable.Range(0, 32).Select(index => $"Entry {index}"));
        Assert.Equal("Before" + expected + "After", DialogueTextFormatter.Format($"Before{{TermEN {operand}}}After", terms: terms));
    }

    [Fact]
    public void NativeTermRequiresSourceContextAndDoesNotTurnUnknownTagsIntoOmissions()
    {
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Format("{TermEN 64}"));
        NativeTextTerms terms = new("en", ["Entry 0"]);
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Format("{TermEN Unrecognized}", terms: terms));
        Assert.Equal("{Unrecognized 64}", DialogueTextFormatter.Format("{Unrecognized 64}", terms: terms));
    }

    [Fact]
    public void SelectedNamedDictionaryAndModernPresentationUseCurrentResourceGeneration()
    {
        using InMemoryResourceSource source = new(new("terms", GameDataEdition.Classic, [], "en"));
        byte[] original = NamedicBin.WriteStrings(["Original"], FF8Encoding.CreateEuropean(FF8TextLanguage.EN));
        source.Set(new("main/namedic.bin"), original, "bin");
        ResourceService baseline = new(source);
        Assert.Equal("Original", NativeTextTerms.FromResources(baseline).Resolve(0));
        ResourceService selected = new(source);
        byte[] replacement = NamedicBin.WriteStrings(["Selected"], FF8Encoding.CreateEuropean(FF8TextLanguage.EN));
        selected.Prepare(null, encodedResources: [new(new("main/namedic.bin"), "bin", 1, "selected", replacement)]).Commit();
        ITextTermSource terms = NativeTextTerms.FromResources(selected);
        Assert.Equal("Selected", DialogueTextFormatter.Format("{TermEN 32}", terms: terms));
        long generation = terms.Generation;
        string key = LocalizationSourceExporter.IndexedKey("en", "namedic_menu", 0);
        selected.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("Modern", "presentation", "presentation") })).Commit();
        Assert.True(terms.Generation > generation);
        Assert.Equal("Modern", DialogueTextFormatter.Format("{TermEN 32}", terms: terms));
        selected.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal("Selected", DialogueTextFormatter.Format("{TermEN 32}", terms: terms));
        Assert.Throws<ResourceReloadRequiredException>(() => selected.Prepare(null, encodedResources: []));
    }

    [Fact]
    public void LiveMenuExpandsRawTemplatesAfterAuthoredLocalizationUsingSelectedNamedDictionary()
    {
        using ClassicGameDataSource source = new(TestResources.GameDirectory);
        ResourceService resources = new(source);
        FF8Encoding encoding = FF8Encoding.CreateEuropean(FF8TextLanguage.EN);
        KernelData kernel = KernelData.ReadFromArray(resources.Read(new("main/kernel.bin")), encoding);
        kernel.JunctionAbilities[0].Description = "Before{TermEN 51}After{TermEN 65}";
        byte[] dictionary = NamedicBin.WriteStrings(Enumerable.Range(0, 32).Select(index => $"Selected {index}").ToArray(), encoding);
        ResourceService selected = new(source);
        selected.Prepare(null, encodedResources: [new(new("main/kernel.bin"), "bin", 1, "selected", kernel.WriteToArray(encoding)), new(new("main/namedic.bin"), "bin", 1, "selected", dictionary)]).Commit();
        using GameAssets assets = new(TestResources.GameDirectory, resources: selected);
        LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
        Assert.Equal("BeforeSelected 19After", menu.AbilityDescription(0));
        string key = LocalizationSourceExporter.IndexedKey("en", LocalizationSourceExporter.KernelTable(nameof(kernel.JunctionAbilities), "Description"), 0);
        selected.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("Modern {TermEN 52}", "presentation", "presentation") })).Commit();
        Assert.Equal("Modern Selected 20", menu.AbilityDescription(0));
        selected.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal("BeforeSelected 19After", menu.AbilityDescription(0));
    }
}
