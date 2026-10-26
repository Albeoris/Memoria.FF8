namespace Memoria.FF8.Assets.Localization;

public sealed record FieldLocalizationNumberingInput(string Key, string Language, string Field, int Ordinal, bool IsLanguageOnly, IReadOnlyList<string>? EnglishKeys = null, string? InsertionGroup = null, string? InsertionAnchorEnglishKey = null);

public sealed record FieldLocalizationNumberingEntry(string OldKey, string NewKey, string Language, string Field, int SourceOrdinal, int AnchorOrdinal, int Insertion);

/// <summary>Allocates shared English bases and globally reserved insertion suffixes while retaining each key's source ordinal.</summary>
public static class FieldLocalizationNumbering
{
    public static IReadOnlyList<FieldLocalizationNumberingEntry> Allocate(IReadOnlyList<FieldLocalizationNumberingInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Dictionary<string, int> keys = new(StringComparer.Ordinal);
        Dictionary<(string Field, int Ordinal), int> englishOrdinals = [];
        HashSet<(string Language, string Field, int Ordinal)> sourceOrdinals = [];
        string[] languages = new string[inputs.Count];
        Components components = new(inputs.Count);
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
            if (!keys.TryAdd(input.Key, index))
            {
                throw new ArgumentException("Numbering inputs must have unique keys.", nameof(inputs));
            }
            if (!sourceOrdinals.Add((language, input.Field, input.Ordinal)))
            {
                throw new ArgumentException("Source ordinals must be unique within each language and field.", nameof(inputs));
            }
            if (language == "en" && input.IsLanguageOnly)
            {
                throw new ArgumentException("English keys cannot be language-only.", nameof(inputs));
            }
            if (input.IsLanguageOnly && input.EnglishKeys is { Count: > 0 })
            {
                throw new ArgumentException("Language-only keys cannot have English references.", nameof(inputs));
            }
            if (input.InsertionGroup is not null && (string.IsNullOrWhiteSpace(input.InsertionGroup) || !input.IsLanguageOnly))
            {
                throw new ArgumentException("Insertion groups must be nonempty and may only classify language-only keys.", nameof(inputs));
            }
            if (input.InsertionAnchorEnglishKey is not null && (string.IsNullOrWhiteSpace(input.InsertionAnchorEnglishKey) || !input.IsLanguageOnly))
            {
                throw new ArgumentException("Insertion anchors must be nonempty and may only anchor language-only keys.", nameof(inputs));
            }
            languages[index] = language;
            if (language == "en")
            {
                englishOrdinals.Add((input.Field, input.Ordinal), index);
            }
        }

        Dictionary<(string Field, int Ordinal), int> virtualAnchors = [];
        int GetVirtualAnchor(string field, int ordinal)
        {
            if (!virtualAnchors.TryGetValue((field, ordinal), out int node))
            {
                node = components.Add();
                virtualAnchors.Add((field, ordinal), node);
                if (englishOrdinals.TryGetValue((field, ordinal), out int english))
                {
                    components.Join(node, english);
                }
            }
            return node;
        }

        for (int index = 0; index < inputs.Count; index++)
        {
            FieldLocalizationNumberingInput input = inputs[index];
            if (input.EnglishKeys is { Count: > 0 })
            {
                foreach (string englishKey in input.EnglishKeys)
                {
                    if (englishKey is null || !keys.TryGetValue(englishKey, out int english) || languages[english] != "en" || inputs[english].Field != input.Field)
                    {
                        throw new ArgumentException("English references must name existing English keys in the same field.", nameof(inputs));
                    }
                    components.Join(index, english);
                }
            }
            else if (!input.IsLanguageOnly && languages[index] != "en")
            {
                // An ordinal-only association is provisional; it does not establish semantic equivalence.
                components.Join(index, GetVirtualAnchor(input.Field, input.Ordinal));
            }
        }

        foreach (IGrouping<(string Language, string Field), int> group in Enumerable.Range(0, inputs.Count).GroupBy(index => (languages[index], inputs[index].Field)))
        {
            int preceding = -1;
            foreach (int index in group.OrderBy(index => inputs[index].Ordinal))
            {
                if (inputs[index].IsLanguageOnly)
                {
                    if (inputs[index].InsertionAnchorEnglishKey is string anchorKey)
                    {
                        if (!keys.TryGetValue(anchorKey, out int english) || languages[english] != "en" || inputs[english].Field != inputs[index].Field)
                        {
                            throw new ArgumentException("Insertion anchors must name existing English keys in the same field.", nameof(inputs));
                        }
                        components.Join(index, english);
                    }
                    else
                    {
                        components.Join(index, preceding >= 0 ? preceding : GetVirtualAnchor(inputs[index].Field, 0));
                    }
                }
                else
                {
                    preceding = index;
                }
            }
        }

        Dictionary<string, List<int>> insertionGroups = new(StringComparer.Ordinal);
        for (int index = 0; index < inputs.Count; index++)
        {
            if (inputs[index].InsertionGroup is not string identity)
            {
                continue;
            }
            if (!insertionGroups.TryGetValue(identity, out List<int>? members))
            {
                members = [];
                insertionGroups.Add(identity, members);
            }
            if (members.Count > 0 && (inputs[members[0]].Field != inputs[index].Field || components.Find(members[0]) != components.Find(index)))
            {
                throw new ArgumentException("Insertion group members must share a field and common anchor component.", nameof(inputs));
            }
            if (members.Any(member => languages[member] == languages[index]))
            {
                throw new ArgumentException("Insertion groups may contain at most one key per canonical language.", nameof(inputs));
            }
            members.Add(index);
        }

        Dictionary<int, int> virtualOrdinals = [];
        foreach (KeyValuePair<(string Field, int Ordinal), int> pair in virtualAnchors)
        {
            int root = components.Find(pair.Value);
            if (!virtualOrdinals.TryGetValue(root, out int ordinal) || pair.Key.Ordinal < ordinal)
            {
                virtualOrdinals[root] = pair.Key.Ordinal;
            }
        }

        FieldLocalizationNumberingEntry[] entries = new FieldLocalizationNumberingEntry[inputs.Count];
        foreach (IGrouping<int, int> group in Enumerable.Range(0, inputs.Count).GroupBy(components.Find))
        {
            int[] ordered = group.OrderBy(index => inputs[index].Ordinal).ThenBy(index => LanguageOrder(languages[index])).ThenBy(index => inputs[index].Key, StringComparer.Ordinal).ToArray();
            int[] english = ordered.Where(index => languages[index] == "en").ToArray();
            int anchor = english.Length > 0
                ? inputs[english[0]].Ordinal
                : virtualOrdinals[group.Key];
            Dictionary<int, int> englishTokens = english.Select((index, token) => (index, token)).ToDictionary(item => item.index, item => item.token);
            Dictionary<string, HashSet<int>> occupied = new(StringComparer.Ordinal);
            List<int> extras = [];
            void Assign(int index, int insertion)
            {
                if (insertion > 99)
                {
                    throw new ArgumentException("An anchor cannot require more than 99 insertion suffixes.", nameof(inputs));
                }
                if (!occupied.TryGetValue(languages[index], out HashSet<int>? tokens))
                {
                    tokens = [];
                    occupied.Add(languages[index], tokens);
                }
                tokens.Add(insertion);
                FieldLocalizationNumberingInput input = inputs[index];
                entries[index] = new FieldLocalizationNumberingEntry(input.Key, FieldLocalizationKey.Create(languages[index], input.Field, anchor, insertion), languages[index], input.Field, input.Ordinal, anchor, insertion);
            }

            foreach (int index in english)
            {
                Assign(index, englishTokens[index]);
            }
            foreach (int index in ordered.Where(index => languages[index] != "en").OrderBy(index => inputs[index].EnglishKeys is { Count: > 0 } ? 0 : 1))
            {
                FieldLocalizationNumberingInput input = inputs[index];
                if (input.IsLanguageOnly)
                {
                    extras.Add(index);
                    continue;
                }
                int preferred = 0;
                if (input.EnglishKeys is { Count: > 0 })
                {
                    int reference = input.EnglishKeys.Select(key => keys[key]).OrderBy(reference => inputs[reference].Ordinal).First();
                    preferred = englishTokens[reference];
                }
                else if (englishOrdinals.TryGetValue((input.Field, input.Ordinal), out int sourceEnglish))
                {
                    preferred = englishTokens[sourceEnglish];
                }
                if (occupied.TryGetValue(languages[index], out HashSet<int>? tokens) && tokens.Contains(preferred))
                {
                    extras.Add(index);
                }
                else
                {
                    Assign(index, preferred);
                }
            }
            int nextInsertion = Math.Max(1, english.Length);
            HashSet<string> assignedGroups = new(StringComparer.Ordinal);
            foreach (int index in extras.OrderBy(index => inputs[index].Ordinal).ThenBy(index => LanguageOrder(languages[index])).ThenBy(index => inputs[index].Key, StringComparer.Ordinal))
            {
                if (inputs[index].InsertionGroup is string identity)
                {
                    if (!assignedGroups.Add(identity))
                    {
                        continue;
                    }
                    foreach (int member in insertionGroups[identity])
                    {
                        Assign(member, nextInsertion);
                    }
                }
                else
                {
                    Assign(index, nextInsertion);
                }
                nextInsertion++;
            }
        }
        return entries;
    }

    // Fixed language order makes globally reserved suffixes independent of input enumeration order.
    private static int LanguageOrder(string language)
    {
        return language switch
        {
            "en" => 0,
            "fr" => 1,
            "de" => 2,
            "es" => 3,
            "it" => 4,
            "jp" => 5,
            _ => throw new ArgumentException("Unsupported language.", nameof(language))
        };
    }

    private sealed class Components
    {
        private readonly List<int> parents;

        public Components(int count)
        {
            parents = Enumerable.Range(0, count).ToList();
        }

        public int Add()
        {
            int index = parents.Count;
            parents.Add(index);
            return index;
        }

        public int Find(int index)
        {
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }
            while (parents[index] != index)
            {
                int next = parents[index];
                parents[index] = root;
                index = next;
            }
            return root;
        }

        public void Join(int first, int second)
        {
            int firstRoot = Find(first);
            int secondRoot = Find(second);
            if (firstRoot != secondRoot)
            {
                parents[Math.Max(firstRoot, secondRoot)] = Math.Min(firstRoot, secondRoot);
            }
        }
    }
}
