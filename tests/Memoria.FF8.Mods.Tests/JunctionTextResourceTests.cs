using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionTextResourceTests
{
    [Fact]
    public void SelectedTablesUseEffectiveOrderAndPublishWithGenerationThenRestoreOriginal()
    {
        ModFile first = File("first", "direct/exe/card_names.msd", Enumerable.Range(0, 110).Select(id => $"First {id}"));
        ModFile second = File("second", "direct/exe/card_names.msd", Enumerable.Range(0, 110).Select(id => $"Second {id}"));
        ModFile other = File("second", "direct/exe/menu_labels.msd", ["One{End}", "Two{End}{End}"]);
        ModCatalogSnapshot catalog = new(1, [], [first, second, other]);
        Assert.True(JunctionTextResources.TryPrepare(catalog, out var texts, out var diagnostics));
        Assert.Empty(diagnostics);
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        ResourceKey key = StaticTextResource.CardNames;
        Assert.Equal("Original", resources.ReadText(key, 0, "Original"));
        ResourceService.PreparedResources prepared = resources.Prepare(null, texts);
        Assert.Null(resources.FindText(key));
        int notifications = 0;
        using IDisposable subscription = resources.Subscribe(change =>
        {
            notifications++;
            Assert.Contains(key, change.Texts);
            Assert.Equal(change.NewGeneration, resources.Generation);
        });
        prepared.Commit();
        Assert.Equal("first", resources.FindText(key)!.PackageId);
        Assert.Equal("First 109", resources.ReadText(key, 109));
        Assert.Equal("Two", resources.ReadText(new("text/exe/menu_labels"), 1));
        Assert.Equal("Outside", resources.ReadText(key, 110, "Outside"));
        Assert.True(JunctionTextResources.TryPrepare(new(2, [], [second]), out texts, out diagnostics));
        resources.Prepare(null, texts).Commit();
        Assert.Equal("Second 0", resources.ReadText(key, 0));
        resources.Prepare(null, []).Commit();
        Assert.Equal("Original", resources.ReadText(key, 0, "Original"));
        Assert.Equal(3, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedOrWrongCardIdentityCountRejectsPublicationWithOwningDiagnostic(bool wrongCount)
    {
        ModFile invalid = wrongCount ? File("invalid", "direct/exe/card_names.msd", ["Incomplete"]) : new("invalid", ModFormat.Junction, "direct/exe/card_names.msd", 1, () => [1, 2, 3]);
        Assert.False(JunctionTextResources.TryPrepare(new(1, [], [invalid]), out _, out var diagnostics));
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Junction.InvalidTextResource", diagnostic.Code);
        Assert.Equal("invalid", diagnostic.PackageId);
        Assert.Equal("direct/exe/card_names.msd", diagnostic.RelativePath);
    }

    [Theory]
    [InlineData("DIRECT\\EXE\\CARD_NAMES.MSD", "text/exe/card_names")]
    [InlineData("direct/exe/menu_labels.msd", "text/exe/menu_labels")]
    [InlineData("exe/menu_labels.msd", "text/exe/menu_labels")]
    [InlineData("EXE\\CARD_NAMES.MSD", "text/exe/card_names")]
    [InlineData("mods/textures/cardgame/cards.png", null)]
    public void AddressBindsAnyExecutableMsdWithoutPackageNames(string path, string? expected)
    {
        Assert.Equal(expected, StaticTextResource.Address(path)?.Path);
    }

    [Fact]
    public void NativeSpeedZeroTerminatesStaticTextWithoutASeparateEndByte()
    {
        ModFile raw = new("controls", ModFormat.Junction, "direct/exe/control_text.msd", 1, () => [4, 0, 0, 0, 8, 0]);
        Assert.True(JunctionTextResources.TryPrepare(new(1, [], [raw]), out var texts, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal("", Assert.Single(texts).Texts[0]);
        ModFile tags = File("controls", "direct/exe/control_text.msd", ["Before{Speed 0}After", "Before{End}After{Speed 0}", "A{Speed 32}B"]);
        Assert.True(JunctionTextResources.TryPrepare(new(2, [], [tags]), out texts, out diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(["Before", "Before", "A{Speed 32}B"], Assert.Single(texts).Texts);
    }

    [Theory]
    [InlineData("battle_scans", 160)]
    [InlineData("draw_point", 9)]
    [InlineData("card_texts", 29)]
    public void KnownNativeBanksRequireTheirCompleteIndexedIdentityCount(string bank, int count)
    {
        ModFile valid = File("bank", $"direct/exe/{bank}.msd", Enumerable.Range(0, count).Select(index => $"Entry {index}"));
        Assert.True(JunctionTextResources.TryPrepare(new(1, [], [valid]), out _, out var diagnostics));
        Assert.Empty(diagnostics);
        ModFile invalid = File("bank", $"direct/exe/{bank}.msd", Enumerable.Range(0, count - 1).Select(index => $"Entry {index}"));
        Assert.False(JunctionTextResources.TryPrepare(new(2, [], [invalid]), out _, out diagnostics));
        Assert.Equal("Junction.InvalidTextResource", Assert.Single(diagnostics).Code);
    }

    private static ModFile File(string package, string path, IEnumerable<string> texts)
    {
        MsdFile table = new(FF8Encoding.CreateEuropean());
        table.Texts.AddRange(texts);
        return new(package, ModFormat.Junction, path, 1, table.Write);
    }
}
