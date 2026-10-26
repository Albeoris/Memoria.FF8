namespace Memoria.FF8.Assets.Localization;

public sealed record FieldLocalizationNumberingInput(string Key, string Language, string Field, int Ordinal, bool IsLanguageOnly);

public sealed record FieldLocalizationNumberingEntry(string OldKey, string NewKey, string Language, string Field, int SourceOrdinal, int AnchorOrdinal, int Insertion);

/// <summary>Allocates two-digit insertion suffixes while retaining each key's source ordinal.</summary>
public static class FieldLocalizationNumbering
{
    public static IReadOnlyList<FieldLocalizationNumberingEntry> Allocate(IReadOnlyList<FieldLocalizationNumberingInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Dictionary<(string Language, string Field), List<(FieldLocalizationNumberingInput Input, int Index)>> groups = [];
        HashSet<string> oldKeys = new(StringComparer.Ordinal);
        HashSet<(string Language, string Field, int Ordinal)> sourceOrdinals = [];
        for (int index = 0; index < inputs.Count; index++)
        {
            FieldLocalizationNumberingInput input = inputs[index];
            if (input is null || string.IsNullOrWhiteSpace(input.Key))
            {
                throw new ArgumentException("Numbering inputs must have nonempty keys.", nameof(inputs));
            }
            string language = FieldLocalizationKey.CanonicalLanguage(input.Language);
            FieldLocalizationKey.ValidateField(input.Field);
            ArgumentOutOfRangeException.ThrowIfNegative(input.Ordinal);
            if (!oldKeys.Add(input.Key))
            {
                throw new ArgumentException("Numbering inputs must have unique keys.", nameof(inputs));
            }
            if (!sourceOrdinals.Add((language, input.Field, input.Ordinal)))
            {
                throw new ArgumentException("Source ordinals must be unique within each language and field.", nameof(inputs));
            }
            if (!groups.TryGetValue((language, input.Field), out List<(FieldLocalizationNumberingInput Input, int Index)>? group))
            {
                group = [];
                groups.Add((language, input.Field), group);
            }
            group.Add((input, index));
        }

        FieldLocalizationNumberingEntry[] entries = new FieldLocalizationNumberingEntry[inputs.Count];
        HashSet<string> newKeys = new(StringComparer.Ordinal);
        foreach (KeyValuePair<(string Language, string Field), List<(FieldLocalizationNumberingInput Input, int Index)>> group in groups)
        {
            int anchor = 0;
            int insertion = 0;
            foreach ((FieldLocalizationNumberingInput input, int index) in group.Value.OrderBy(item => item.Input.Ordinal))
            {
                if (input.IsLanguageOnly)
                {
                    insertion++;
                    if (insertion > 99)
                    {
                        throw new ArgumentException("An anchor cannot have more than 99 language-only insertions.", nameof(inputs));
                    }
                }
                else
                {
                    anchor = input.Ordinal;
                    insertion = 0;
                }
                string newKey = FieldLocalizationKey.Create(group.Key.Language, group.Key.Field, anchor, insertion);
                if (!newKeys.Add(newKey))
                {
                    throw new ArgumentException("Numbering inputs produce duplicate destination keys.", nameof(inputs));
                }
                entries[index] = new FieldLocalizationNumberingEntry(input.Key, newKey, group.Key.Language, group.Key.Field, input.Ordinal, anchor, insertion);
            }
        }
        return entries;
    }
}
