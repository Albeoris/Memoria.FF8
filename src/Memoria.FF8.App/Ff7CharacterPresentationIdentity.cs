using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record Ff7CharacterDisplayIdentity(string Name, string DefaultName, SessionGame PortraitGame, int PortraitCharacter, bool PresentationOwned);

/// <summary>Projects an association into presentation without modifying either game's native character records.</summary>
internal sealed class Ff7CharacterPresentationIdentity
{
    private static readonly string[] NativeDefaultNames = ["Cloud", "Barret", "Tifa", "Aeris", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid"];
    private readonly IReadOnlyDictionary<int, CharacterAppearanceMapping> mappings;
    private readonly IReadOnlyDictionary<int, string> overrides;
    private readonly IReadOnlyDictionary<int, string> ff8Names;

    internal Ff7CharacterPresentationIdentity(IEnumerable<CharacterAppearanceMapping> mappings, IReadOnlyDictionary<int, string>? overrides = null, IReadOnlyDictionary<int, string>? ff8Names = null)
    {
        CharacterAppearanceMapping[] associations = mappings.ToArray();
        CharacterMappingAssociations.Validate(associations);
        this.mappings = associations.ToDictionary(mapping => mapping.StoryCharacter);
        this.overrides = overrides?.ToDictionary() ?? [];
        this.ff8Names = ff8Names?.ToDictionary() ?? [];
    }

    internal static Ff7CharacterPresentationIdentity FromSave(CombinedSessionSave save)
    {
        Dictionary<int, string> names = [];
        if (save.Ff8Payload is { Length: > 0 })
        {
            GameplaySaveProgression? progression = CrossoverSaveAdapter.ReadFf8(save).Progression;
            foreach ((int subject, string name) in progression?.OriginalResidue?.Names ?? [])
            {
                names[subject] = name;
            }
            foreach ((int subject, string name) in progression?.SceneFacts?.Names ?? [])
            {
                names[subject] = name;
            }
        }
        return new(save.Mappings, save.CharacterNameOverrides, names);
    }

    internal Ff7CharacterDisplayIdentity Resolve(int storyCharacter, string nativeName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(storyCharacter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(storyCharacter, 8);
        CharacterAppearanceMapping? mapping = mappings.GetValueOrDefault(storyCharacter);
        string defaultName = NativeDefaultNames[storyCharacter];
        if (mapping is not null)
        {
            int subject = GameNames.CharacterSubject(mapping.AppearanceCharacter);
            defaultName = ff8Names.TryGetValue(subject, out string? selected) && !string.IsNullOrWhiteSpace(selected) ? selected : GameNames.DefaultName(subject);
        }
        bool nativeDefault = nativeName == NativeDefaultNames[storyCharacter];
        string name = overrides.GetValueOrDefault(storyCharacter) ?? (mapping is not null && nativeDefault ? defaultName : nativeName);
        return new(name, defaultName, mapping?.AppearanceGame ?? SessionGame.Ff7, mapping?.AppearanceCharacter ?? storyCharacter, mapping is not null || overrides.ContainsKey(storyCharacter));
    }

    internal static CombinedSessionSave AcceptPresentationName(CombinedSessionSave save, int storyCharacter, string name, bool edited)
    {
        if (!edited)
        {
            return save;
        }
        string normalized = GameNames.Normalize(name);
        Dictionary<int, string> overrides = save.CharacterNameOverrides?.ToDictionary() ?? [];
        overrides[storyCharacter] = normalized;
        CombinedSessionSave result = save with { CharacterNameOverrides = overrides };
        result.ValidateCharacterNameOverrides();
        return result;
    }
}
