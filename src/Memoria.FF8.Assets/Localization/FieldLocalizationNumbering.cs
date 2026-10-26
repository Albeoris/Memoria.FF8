namespace Memoria.FF8.Assets.Localization;

public sealed record FieldLocalizationNumberingInput(string Key, string Language, string Field, int Ordinal);

public sealed record FieldLocalizationNumberingEntry(string OldKey, string NewKey, string Language, string Field, int SourceOrdinal);

/// <summary>Names each key independently using its language, field, and original source ordinal.</summary>
public static class FieldLocalizationNumbering
{
    public static IReadOnlyList<FieldLocalizationNumberingEntry> Allocate(IReadOnlyList<FieldLocalizationNumberingInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        HashSet<string> oldKeys = new(StringComparer.Ordinal);
        HashSet<string> newKeys = new(StringComparer.Ordinal);
        HashSet<(string Language, string Field, int Ordinal)> sourceOrdinals = [];
        List<FieldLocalizationNumberingEntry> entries = new(inputs.Count);
        foreach (FieldLocalizationNumberingInput input in inputs)
        {
            if (input is null || string.IsNullOrWhiteSpace(input.Key))
            {
                throw new ArgumentException("Numbering inputs must have nonempty keys.", nameof(inputs));
            }
            string language = FieldLocalizationKey.CanonicalLanguage(input.Language);
            string newKey = FieldLocalizationKey.Create(language, input.Field, input.Ordinal);
            if (!oldKeys.Add(input.Key))
            {
                throw new ArgumentException("Numbering inputs must have unique keys.", nameof(inputs));
            }
            if (!sourceOrdinals.Add((language, input.Field, input.Ordinal)))
            {
                throw new ArgumentException("Source ordinals must be unique within each language and field.", nameof(inputs));
            }
            if (!newKeys.Add(newKey))
            {
                throw new ArgumentException("Numbering inputs must produce unique canonical keys.", nameof(inputs));
            }
            entries.Add(new FieldLocalizationNumberingEntry(input.Key, newKey, language, input.Field, input.Ordinal));
        }
        return entries;
    }
}
