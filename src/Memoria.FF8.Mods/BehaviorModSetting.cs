using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public sealed record BehaviorModSetting(string Behavior, string Parameter, string Label, int Min, int Max, int DefaultValue, int Value)
{
    public string Category { get; init; } = string.Empty;
    public string? ParentComponent { get; init; }
    public NativeModDescriptor? Descriptor { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Control { get; init; } = "slider";
    public string Unit { get; init; } = "%";
    public int Step { get; init; } = 1;
    public BattleStatId? Stat { get; init; }
    public IReadOnlyDictionary<string, string>? LabelTranslations { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionTranslations { get; init; }

    public string LabelForLanguage(string? language)
    {
        return LabelTranslations is null ? Label : NativeModDescriptor.TextForLanguage(LabelTranslations, language);
    }

    public string DescriptionForLanguage(string? language)
    {
        return DescriptionTranslations is null ? Description : NativeModDescriptor.TextForLanguage(DescriptionTranslations, language);
    }
}
