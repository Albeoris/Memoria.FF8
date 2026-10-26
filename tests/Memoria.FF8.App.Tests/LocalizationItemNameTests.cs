using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationItemNameTests
{
    [Fact]
    public void NativeItemOperandResolvesItsCurrentNameEveryTimeWithoutChangingOtherTags()
    {
        string current = "Предмет 日本語";
        string? Name(int selector) => selector == 1 ? current : null;
        Assert.Equal("Found Предмет 日本語{Line}{Var 32}", DialogueTextFormatter.ExpandItemNames("Found {Item 33}{Line}{Var 32}", Name));
        current = "Updated";
        Assert.Equal("Found Updated", DialogueTextFormatter.ExpandItemNames("Found {Item 33}", Name));
        Assert.Throws<InvalidDataException>(() => DialogueTextFormatter.ExpandItemNames("{Item 31}", Name));
        Assert.Throws<InvalidDataException>(() => DialogueTextFormatter.ExpandItemNames("{Item 255}", Name));
    }

    [Fact]
    public void TranslationMustRetainTheNativeItemSelectorAndOccurrences()
    {
        LocalizationRecord row = new() { Key = "$en_messages_world_0043", Source = "Found {Item 33}", Target = "Получено: {Item 33}" };
        LocalizationTextValidation.Validate(row);
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "Получено: {Item 34}" }));
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "Получено" }));
    }
}
