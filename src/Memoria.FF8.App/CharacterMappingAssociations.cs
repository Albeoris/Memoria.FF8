using Memoria.FF7.Runtime;

namespace Memoria.FF8;

/// <summary>A directed presentation association. Editing it never changes either game's native actor or progress.</summary>
internal static class CharacterMappingAssociations
{
    internal static void Validate(IEnumerable<CharacterAppearanceMapping> mappings)
    {
        HashSet<int> stories = [];
        foreach (CharacterAppearanceMapping mapping in mappings)
        {
            bool legacy = mapping.RigProfile is Ff7ModelAppearance.StandardFieldSquallProfileVersion or Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion;
            if (mapping.StoryGame != SessionGame.Ff7 || mapping.AppearanceGame != SessionGame.Ff8 || mapping.StoryCharacter is < 0 or > 8 || mapping.AppearanceCharacter is < 0 or > 10 || !stories.Add(mapping.StoryCharacter) || (!legacy && mapping.RigProfile != Ff7ModelAppearance.GeneralFieldProfileVersion) || (legacy && (mapping.StoryCharacter != 0 || mapping.AppearanceCharacter != 0)))
            {
                throw new NotSupportedException("This association requires an unsupported character or appearance profile.");
            }
        }
    }

    internal static CharacterAppearanceMapping[] Assign(IEnumerable<CharacterAppearanceMapping> mappings, int story, int appearance)
    {
        CharacterAppearanceMapping[] result = mappings.Where(mapping => mapping.StoryCharacter != story).Append(new(SessionGame.Ff7, story, SessionGame.Ff8, appearance, Ff7ModelAppearance.GeneralFieldProfileVersion)).OrderBy(mapping => mapping.StoryCharacter).ToArray();
        Validate(result);
        return result;
    }
}
