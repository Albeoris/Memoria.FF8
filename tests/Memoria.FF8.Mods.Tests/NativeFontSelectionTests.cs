using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class NativeFontSelectionTests
{
    private static readonly NativeTextContext Context = new("Classic", "Fields", "map", "en");

    [Fact]
    public void CrossProfileReferencesRemainIndependentFromExplicitTargetSelection()
    {
        NativeFontProfile target = Profile("ru") with { Assignments = [new("Target", "ru", "ru", Context), new("Reference", "jp", "jp", Context with { DictionaryLanguage = "jp" })] };
        NativeFontProfileCatalog catalog = new([Entry(target), Entry(Profile("jp"))], null, "revision");
        Assert.Equal("ru", catalog.Resolve("ru", Context, "Target", "ru").Entry!.Profile.Id);
        Assert.Equal("jp", catalog.Resolve("ru", Context with { DictionaryLanguage = "jp" }, "Reference", "jp").Entry!.Profile.Id);
        Assert.Equal("ru", catalog.Resolve("Auto", Context, "Target", "ru").Entry!.Profile.Id);
        Assert.False(catalog.Resolve("Auto", Context, "Reference", "other").Success);
        Assert.Equal(new[] { "Auto", "jp", "ru" }, catalog.AvailableProfileIds);
    }

    [Fact]
    public void FieldSpecificAssignmentsOverrideGenericOnesAndAmbiguityDoesNotChooseByOrder()
    {
        NativeFontProfile generic = Profile("generic") with { Assignments = [new("Target", "ru", "generic", Context with { Field = null }), new("Target", "ru", "field", Context)] };
        NativeFontProfileCatalog catalog = new([Entry(generic), Entry(Profile("field"))], null, "revision");
        Assert.Equal("field", catalog.Resolve("Auto", Context, "Target", "ru").Entry!.Profile.Id);
        Assert.Equal("generic", catalog.Resolve("Auto", Context with { Field = "other" }, "Target", "ru").Entry!.Profile.Id);
        NativeFontProfile collision = Profile("collision") with { Assignments = [new("Target", "ru", "collision", Context)] };
        foreach (NativeFontProfileEntry[] order in new[] { new[] { Entry(generic), Entry(Profile("field")), Entry(collision) }, new[] { Entry(collision), Entry(Profile("field")), Entry(generic) } })
        {
            catalog = new(order, null, "revision");
            Assert.Contains("Ambiguous", catalog.Resolve("Auto", Context, "Target", "ru").Diagnostic);
        }
    }

    [Fact]
    public void IdenticalCopiesAreAllowedButIncompatibleIdsMissingReferencesAndDomainsFail()
    {
        NativeFontProfile profile = Profile("one") with { Domains = [new("Classic", "Fields", DictionaryLanguage: "en")] };
        NativeFontProfileCatalog catalog = new([Entry(profile), Entry(profile)], Entry(Profile("installed")), "revision");
        Assert.Contains("does not support", catalog.Resolve("one", Context with { DictionaryLanguage = "jp" }, "Target", "ru").Diagnostic);
        Assert.Throws<InvalidDataException>(() => new NativeFontProfileCatalog([Entry(profile), new(profile, new Dictionary<string, PixelImage>(), "changed")], null, "revision"));
        Assert.Throws<InvalidDataException>(() => new NativeFontProfileCatalog([Entry(Profile("Auto"))], null, "revision"));
        Assert.Throws<InvalidDataException>(() => new NativeFontProfileCatalog([Entry(profile with { Assignments = [new("Target", "ru", "missing", Context)] })], null, "revision"));
    }

    [Fact]
    public void LogicalHeightOverflowAndPenAdvanceUseTheSharedNativeMetrics()
    {
        NativeFontProfile profile = Profile("metrics") with { Atlases = [new("font", "font.png", 12, 12)], Images = [new(7, "font", 0, 0, 12, 12, 7)], Glyphs = [new("A", 7)], LineHeight = 16 };
        NativeFontLayoutResult result = NativeFontLayout.Layout(profile, "AA", Context);
        Assert.Equal(19, result.Width);
        Assert.Equal(14, result.FinalAdvance);
        Assert.Contains(NativeFontLayout.Layout(profile, "A{Line}A", Context, maximumHeight: 16).Diagnostics, issue => issue.Code == "overflow" && issue.Position == 7);
    }

    private static NativeFontProfile Profile(string id) => new() { Id = id, DisplayName = id };
    private static NativeFontProfileEntry Entry(NativeFontProfile profile) => new(profile, new Dictionary<string, PixelImage>(), "same");
}
