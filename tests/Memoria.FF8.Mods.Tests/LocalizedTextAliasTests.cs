using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

[Collection("Localized text alias tracing")]
public sealed class LocalizedTextAliasTests
{
    private const string SourceKey = "$en_interface_system_status.original";
    private const string TargetKey = "$en_kernel_status_target";

    [Fact]
    public void AliasPublicationIsAtomicAndExplicitEmptyRestoresOriginalIdentity()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        ResourceService.PreparedResources prepared = service.Prepare(null, localization: Catalog(TargetKey, "Translated target"), textAliases: Aliases());
        Assert.Equal("Native source", service.Localize(SourceKey, "Native source"));
        prepared.Commit();
        Assert.Equal("Translated target", service.Localize(SourceKey, "Native source"));
        Assert.Equal("Other native", service.Localize("other", "Other native"));
        service.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias>()).Commit();
        Assert.Equal("Native source", service.Localize(SourceKey, "Native source"));
        Assert.Equal(SourceKey, service.ResolveText(SourceKey, "Native source").Binding.Key);
    }

    [Fact]
    public void LocalizationChangesAndOrdinaryPreparationRetainAliasesAndNativeTargetFallback()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, textAliases: Aliases()).Commit();
        Assert.Equal("Native target", service.Localize(SourceKey, "Native source"));
        Assert.Equal("Native target", service.ResolveText(SourceKey, "Native source").Binding.Source);
        service.PrepareLocalization(Catalog(TargetKey, "First translation")).Commit();
        Assert.Equal("First translation", service.Localize(SourceKey, "Native source"));
        service.PrepareLocalization(Catalog(TargetKey, "Second translation")).Commit();
        Assert.Equal("Second translation", service.Localize(SourceKey, "Native source"));
        service.Prepare(null).Commit();
        Assert.Equal("Second translation", service.Localize(SourceKey, "Native source"));
        service.PrepareLocalization(LocalizationCatalog.Empty).Commit();
        Assert.Equal("Native target", service.Localize(SourceKey, "Native source"));
    }

    [Fact]
    public void TraceBindingUsesTargetIdentityAndContributor()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null, localization: Catalog(TargetKey, "Translated target"), textAliases: Aliases()).Commit();
        bool previous = LocalizationTextTrace.Enabled;
        try
        {
            LocalizationTextTrace.Enabled = true;
            string displayed = service.Localize(SourceKey, "Native source");
            LocalizationTextBinding binding = Assert.IsType<LocalizationTextBinding>(LocalizationTextTrace.Find(displayed));
            Assert.Equal(TargetKey, binding.Key);
            Assert.Equal("Native target", binding.Source);
            Assert.Equal("translation-package", binding.PackageId);
            Assert.Equal("translation-project", binding.ProjectId);
            Assert.Equal("Translated target", binding.Template);
        }
        finally
        {
            LocalizationTextTrace.Enabled = previous;
        }
    }

    [Fact]
    public void ExplicitAliasPrecedesSystemSemanticFallback()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        string semanticKey = SystemTextCatalog.Key("magic.Cancel");
        service.Prepare(null, localization: Catalog(SystemTextCatalog.Key("common.cancel"), "Ordinary cancel"), textAliases: new Dictionary<string, LocalizedTextAlias> { [semanticKey] = new(TargetKey, "Native target") }).Commit();
        Assert.Equal("Native target", service.Localize(semanticKey, "Cancel"));
        Assert.Equal(TargetKey, service.ResolveText(semanticKey, "Cancel").Binding.Key);
    }

    [Fact]
    public void ChainsResolveFinalNativeFallbackAndRejectCyclesWithoutPublishing()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        Dictionary<string, LocalizedTextAlias> aliases = Aliases();
        aliases[TargetKey] = new("final", "Final native");
        service.Prepare(null, textAliases: aliases).Commit();
        Assert.Equal("Final native", service.Localize(SourceKey, "Native source"));
        Assert.Equal("final", service.ResolveText(SourceKey, "Native source").Binding.Key);
        aliases["final"] = new(SourceKey, "Native source");
        Assert.Throws<InvalidDataException>(() => service.Prepare(null, textAliases: aliases));
        Assert.Throws<InvalidDataException>(() => service.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias> { [SourceKey] = new(SourceKey, "Native source") }));
        Assert.Equal("Final native", service.Localize(SourceKey, "Native source"));
    }

    [Theory]
    [InlineData("", "target")]
    [InlineData("source", "")]
    [InlineData("source", " ")]
    public void InvalidKeysAreRejected(string key, string target)
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        Assert.Throws<InvalidDataException>(() => service.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias> { [key] = new(target, "Native target") }));
    }

    private static Dictionary<string, LocalizedTextAlias> Aliases() => new(StringComparer.Ordinal) { [SourceKey] = new(TargetKey, "Native target") };

    private static LocalizationCatalog Catalog(string key, string text) => new(new Dictionary<string, LocalizationValue> { [key] = new(text, "translation-package", "translation-project") });

    private static InMemoryResourceSource Source() => new(new("memory", GameDataEdition.Classic, ["test-layout"]));
}

[CollectionDefinition("Localized text alias tracing", DisableParallelization = true)]
public sealed class LocalizedTextAliasTracingCollection
{
}
