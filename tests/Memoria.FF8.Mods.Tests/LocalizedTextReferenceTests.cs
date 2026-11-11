using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizedTextReferenceTests
{
    [Theory]
    [InlineData("en", "$en_kernel_magicspells_name_0049")]
    [InlineData("fr", "$fr_kernel_magicspells_name_0049")]
    [InlineData("DE", "$de_kernel_magicspells_name_0049")]
    [InlineData("ja", "$jp_kernel_magicspells_name_0049")]
    [InlineData("JP", "$jp_kernel_magicspells_name_0049")]
    public void TemplateUsesCanonicalOriginalResourceLanguage(string sourceLanguage, string expected)
    {
        LocalizedTextReference reference = LocalizedTextReference.Parse("${LANG}_kernel_magicspells_name_0049");

        Assert.True(reference.UsesSourceLanguage);
        Assert.Equal(expected, reference.ResolveKey(sourceLanguage));
    }

    [Theory]
    [InlineData("$en_kernel_magicspells_name_0049", "$en_kernel_magicspells_name_0049")]
    [InlineData("$FR_kernel_magicspells_name_0049", "$fr_kernel_magicspells_name_0049")]
    [InlineData("$ja_kernel_magicspells_name_0049", "$jp_kernel_magicspells_name_0049")]
    public void AbsoluteReferenceRemainsPinned(string key, string expected)
    {
        LocalizedTextReference reference = new(key);

        Assert.False(reference.UsesSourceLanguage);
        Assert.Equal(expected, reference.ResolveKey("de"));
    }

    [Fact]
    public void TranslatedCatalogRetainsOriginalLanguageIdentity()
    {
        LocalizationCatalog catalog = new(new Dictionary<string, LocalizationValue>
        {
            ["$en_kernel_magicspells_name_0049"] = new("Translated spell", "translation", "ru-project"),
            ["$fr_kernel_magicspells_name_0049"] = new("Other source spell", "translation", "ru-project")
        });
        LocalizedTextReference reference = new("${LANG}_kernel_magicspells_name_0049");

        Assert.Equal("Translated spell", catalog.Resolve(reference.ResolveKey("en"), "native"));
        Assert.Equal("Other source spell", catalog.Resolve(reference.ResolveKey("fr"), "native"));
    }

    [Theory]
    [InlineData("${LANG}_interface_system_main.menu", "$de_interface_system_main.menu")]
    [InlineData("$en_bgroom_6_0000_ctx_aabbccdd", "$en_bgroom_6_0000_ctx_aabbccdd")]
    public void SupportsKnownSystemAndContextIdentities(string key, string expected)
    {
        Assert.Equal(expected, new LocalizedTextReference(key).ResolveKey("de"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("$en_kernel_magicspells_name")]
    [InlineData("$en_kernel_magicspells_name_049")]
    [InlineData("$en_interface_system_unknown.identity")]
    [InlineData("${TARGET}_kernel_magicspells_name_0049")]
    [InlineData("${lang}_kernel_magicspells_name_0049")]
    [InlineData("${LANG}_${OTHER}_0049")]
    [InlineData("$ru_kernel_magicspells_name_0049")]
    [InlineData("$xx_kernel_magicspells_name_0049")]
    [InlineData("$en_kernel_magicspells_name_0049\n")]
    public void RejectsIncompleteOrUnsupportedReferences(string key)
    {
        Assert.Throws<InvalidDataException>(() => new LocalizedTextReference(key));
    }

    [Theory]
    [InlineData("ru")]
    [InlineData("auto")]
    [InlineData("{LANG}")]
    [InlineData("")]
    public void TemplateCannotResolveWithoutSupportedOriginalLanguage(string sourceLanguage)
    {
        LocalizedTextReference reference = new("${LANG}_kernel_magicspells_name_0049");

        Assert.Throws<ArgumentException>(() => reference.ResolveKey(sourceLanguage));
    }
}
