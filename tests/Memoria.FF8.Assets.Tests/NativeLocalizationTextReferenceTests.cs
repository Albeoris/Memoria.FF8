using Albeoris.Games.FF8.KernelBin;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeLocalizationTextReferenceTests
{
    [Fact]
    public void SystemReferencesAndUnknownIdentitiesDoNotReadKernelResources()
    {
        using InMemoryResourceSource source = new(new("text-references", GameDataEdition.Classic, []));
        NativeLocalizationTextReferences references = new(new(source));

        Assert.Equal("Vit 0", references.ResolveOriginal(SystemTextCatalog.Key("battle.status.vit-0")));
        Assert.Throws<InvalidDataException>(() => references.ResolveOriginal("$status0003"));
        Assert.Throws<InvalidDataException>(() => references.ResolveOriginal("$fr_kernel_magicspells_name_0049"));
    }

    [Fact]
    public void InstalledSpellReferenceUsesTheOriginalKernelIdentityAndRejectsMissingRows()
    {
        using IResourceSource source = OpenInstalled();
        ResourceService resources = new(source);
        NativeLocalizationTextReferences references = new(resources);
        string key = SpellKey(resources, 49);
        KernelData kernel = KernelData.ReadFromArray(resources.Read(new("main/kernel.bin")), NativeTextEncoding.Create(resources.Source.Language));

        Assert.Equal(kernel.MagicSpells[49].Name, references.ResolveOriginal(key));
        if (resources.Source.Language == "en")
        {
            Assert.Equal("Meltdown", references.ResolveOriginal(key));
        }
        Assert.Throws<InvalidDataException>(() => references.ResolveOriginal(SpellKey(resources, 9999)));
        Assert.Throws<InvalidDataException>(() => references.ResolveOriginal(key.Replace("_name_", "_missing_", StringComparison.Ordinal)));
    }

    [Fact]
    public void SelectedEncodedKernelSuppliesTheNativeFallbackName()
    {
        using IResourceSource source = OpenInstalled();
        ResourceService resources = new(source);
        var encoding = NativeTextEncoding.Create(resources.Source.Language);
        KernelData kernel = KernelData.ReadFromArray(resources.Read(new("main/kernel.bin")), encoding);
        kernel.MagicSpells[49].Name = "Selected spell";
        using InMemoryResourceSource selectedSource = new(source.Descriptor);
        ResourceService selected = new(selectedSource);
        selected.Prepare(null, encodedResources: [new EncodedResource(new("main/kernel.bin"), "bin", 1, "selected-kernel", kernel.WriteToArray(encoding))]).Commit();

        Assert.Equal("Selected spell", new NativeLocalizationTextReferences(selected).ResolveOriginal(SpellKey(selected, 49)));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("jp")]
    public void TemplateResolvesSelectedLanguageKernelAndKeepsTranslationIdentity(string language)
    {
        KernelData kernel = new();
        string nativeName = language == "jp" ? "メルトダウン" : $"Native {language}";
        for (int index = 0; index <= 49; index++)
        {
            kernel.MagicSpells.Add(new() { Name = nativeName, Description = string.Empty });
        }
        using InMemoryResourceSource source = new(new("language-kernel", GameDataEdition.Classic, [], language));
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: [new EncodedResource(new("main/kernel.bin"), "bin", 1, "synthetic-kernel", kernel.WriteToArray(NativeTextEncoding.Create(language)))]).Commit();
        NativeLocalizationTextReferences references = new(resources);
        LocalizedTextReference reference = new("${LANG}_kernel_magicspells_name_0049");
        string target = reference.ResolveKey(resources.Source.Language);
        string original = references.ResolveOriginal(reference.Key);
        Assert.Equal(nativeName, original);
        string status = SystemTextCatalog.Key("battle.status.vit-0");
        resources.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias> { [status] = new(target, original) }).Commit();
        Assert.Equal(original, resources.Localize(status, "Vit 0"));
        resources.PrepareLocalization(new(new Dictionary<string, LocalizationValue> { [target] = new("Translated spell", "translation", "ru-project") })).Commit();
        Assert.Equal("Translated spell", resources.Localize(status, "Vit 0"));
        Assert.Equal(target, resources.ResolveText(status, "Vit 0").Binding.Key);
        if (language != "en")
        {
            Assert.Throws<InvalidDataException>(() => references.ResolveOriginal("$en_kernel_magicspells_name_0049"));
        }
    }

    private static string SpellKey(ResourceService resources, int ordinal) => LocalizationSourceExporter.IndexedKey(resources.Source.Language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), ordinal);

    private static IResourceSource OpenInstalled()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        return GameDataSourceFactory.Open(directory!);
    }
}
