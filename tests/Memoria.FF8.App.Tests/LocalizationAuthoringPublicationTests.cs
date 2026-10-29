using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringPublicationTests
{
    private const string FieldKey = "$en_example_0000";
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void UnrelatedAuthoringOverlayPreservesHigherGenericOverLowerScopedField()
    {
        Dictionary<string, LocalizationValue> lower = new() { [FieldKey + "_ctx_lower"] = Field("lower scoped", "lower", FieldKey) };
        Dictionary<string, LocalizationValue> higher = new() { [FieldKey] = Field("higher generic", "higher") };
        LocalizationCatalog normal = Layered(lower, higher);
        Assert.Equal("higher generic", ResolveField(normal)!.Body);

        LocalizationCatalog authoring = normal.WithOverlay(new LocalizationCatalog(new Dictionary<string, LocalizationValue> { ["unrelated"] = new("preview", "selected", "selected-project") }));

        Assert.Equal("higher generic", ResolveField(authoring)!.Body);
        Assert.Equal("preview", authoring.Resolve("unrelated", "native"));
        Assert.Null(authoring.ResolveField(FieldKey, 18, Hash, null, null, null, null));
    }

    [Fact]
    public void LegalScopedVariantsInSeparateLayersRemainLegalAfterOverlay()
    {
        Dictionary<string, LocalizationValue> lower = new() { [FieldKey + "_ctx_lower"] = Field("lower scoped", "lower", FieldKey) };
        Dictionary<string, LocalizationValue> higher = new() { [FieldKey + "_ctx_higher"] = Field("higher scoped", "higher", FieldKey) };
        LocalizationCatalog normal = Layered(lower, higher);

        LocalizationCatalog authoring = normal.WithOverlay(new LocalizationCatalog(new Dictionary<string, LocalizationValue> { ["other"] = new("overlay", "selected", "selected-project") }));

        Assert.Equal("higher scoped", ResolveField(authoring)!.Body);
        Assert.Equal("higher scoped", ResolveField(normal)!.Body);
        Assert.Equal(3, authoring.Values.Count);
    }

    [Fact]
    public void ClearingSelectedRowExposesLowerContributionAndPreservesItsOtherRowsAndAliases()
    {
        string semantic = SystemTextCatalog.Key("ui.connectguardianforceeditor.entry.label.cancel");
        string legacy = SystemTextCatalog.Key("common.cancel");
        Dictionary<string, LocalizationValue> lower = new()
        {
            [FieldKey] = Field("lower field", "lower"),
            [semantic] = new("lower cancel", "lower", "lower-project"),
            ["native-only"] = new("retained lower row", "lower", "lower-project")
        };
        Dictionary<string, LocalizationValue> selected = new()
        {
            [FieldKey] = Field("selected field", "selected"),
            [legacy] = new("selected cancel", "selected", "selected-project"),
            ["selected-other"] = new("retained selected row", "selected", "selected-project")
        };
        LocalizationCatalog visible = Layered(lower, selected).WithOverlay(new LocalizationCatalog(selected));

        LocalizationCatalog cleared = visible.WithoutContribution("selected", FieldKey);

        Assert.Equal("lower field", ResolveField(cleared)!.Body);
        Assert.Equal("lower field", cleared.Resolve(FieldKey, "native field"));
        Assert.Equal("retained selected row", cleared.Resolve("selected-other", "native"));
        Assert.Equal("retained lower row", cleared.Resolve("native-only", "native"));
        Assert.Equal("selected cancel", cleared.ResolveText(semantic, "Cancel").Text);
        LocalizationCatalog clearedAlias = cleared.WithoutContribution("selected", legacy);
        BoundLocalizationText aliasFallback = clearedAlias.ResolveText(semantic, "Cancel");
        Assert.Equal("lower cancel", aliasFallback.Text);
        Assert.Equal(semantic, aliasFallback.Binding.Key);
        Assert.Equal("lower", aliasFallback.Binding.PackageId);
        Assert.Equal("selected field", ResolveField(visible)!.Body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceResolutionReportsActualAliasWinnerIdentityAndContributor(bool legacyInHigherLayer)
    {
        string semantic = SystemTextCatalog.Key("ui.connectguardianforceeditor.entry.label.cancel");
        string legacy = SystemTextCatalog.Key("common.cancel");
        string lowerKey = legacyInHigherLayer ? semantic : legacy;
        string higherKey = legacyInHigherLayer ? legacy : semantic;
        Dictionary<string, LocalizationValue> lower = new() { [lowerKey] = new("lower wording", "lower", "lower-project") };
        Dictionary<string, LocalizationValue> higher = new() { [higherKey] = new("winning wording", "higher", "higher-project") };
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        resources.PrepareLocalization(Layered(lower, higher)).Commit();

        BoundLocalizationText resolved = resources.ResolveText(semantic, "Cancel");

        Assert.Equal("winning wording", resolved.Text);
        Assert.Equal(higherKey, resolved.Binding.Key);
        Assert.Equal("higher", resolved.Binding.PackageId);
        Assert.Equal("higher-project", resolved.Binding.ProjectId);
        Assert.Equal("Cancel", resolved.Binding.Source);
        Assert.Equal("winning wording", resolved.Binding.Template);
        Assert.Equal("System", resolved.Binding.Category);
        Assert.Equal(SystemTextCatalog.File, resolved.Binding.File);
        Assert.Equal(resources.Localization.Fingerprint, resolved.Binding.ResourceFingerprint);
    }

    [Fact]
    public void IdenticalWordingFromANewContributorHasDistinctPublicationEvidence()
    {
        const string key = "$en_sample_0000";
        LocalizationCatalog before = new(new Dictionary<string, LocalizationValue> { [key] = new("same wording", "first-package", "first-project") });
        LocalizationCatalog after = new(new Dictionary<string, LocalizationValue> { [key] = new("same wording", "second-package", "second-project") });
        Assert.Equal(before.Resolve(key, "native"), after.Resolve(key, "native"));
        Assert.NotEqual(before.Fingerprint, after.Fingerprint);
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        resources.PrepareLocalization(before).Commit();
        BoundLocalizationText original = resources.ResolveText(key, "native");
        resources.PrepareLocalization(after).Commit();
        BoundLocalizationText current = resources.ResolveText(key, "native");
        Assert.Equal(original.Text, current.Text);
        Assert.Equal("first-package", original.Binding.PackageId);
        Assert.Equal("second-package", current.Binding.PackageId);
        Assert.Equal("second-project", current.Binding.ProjectId);
        Assert.NotEqual(original.Binding.ResourceFingerprint, current.Binding.ResourceFingerprint);
    }

    private static LocalizationValue Field(string body, string package, string? baseKey = null) => new(body, package, package + "-project")
    {
        Field = new(body, Hash, null, null, null, null, null, null, null, null, [new(17, 0, false, null, null, null, null)]) { BaseKey = baseKey }
    };

    private static LocalizationFieldText? ResolveField(LocalizationCatalog catalog) => catalog.ResolveField(FieldKey, 17, Hash, null, null, null, null);

    private static LocalizationCatalog Layered(params IReadOnlyDictionary<string, LocalizationValue>[] layers)
    {
        Dictionary<string, LocalizationValue> visible = new(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, LocalizationValue> layer in layers)
        {
            foreach (var pair in layer)
            {
                visible[pair.Key] = pair.Value;
            }
        }
        return new(visible, layers);
    }
}
