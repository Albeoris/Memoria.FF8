using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal static class Ff7AppearanceSelection
{
    internal static IFf7ActorAppearance? Create(CharacterAppearanceMapping[] mappings, Ff7Installation installation, GameAssets? assets)
    {
        if (mappings.Length == 0)
        {
            return null;
        }
        if (mappings.Length != 1 || mappings[0] is not { StoryGame: SessionGame.Ff7, StoryCharacter: 0, AppearanceGame: SessionGame.Ff8, AppearanceCharacter: 0, RigProfile: Ff7ModelAppearance.StandardFieldSquallProfileVersion or Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion })
        {
            throw new NotSupportedException("This save requires an unsupported character appearance profile.");
        }
        return Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets ?? throw new InvalidOperationException("A mapped character requires the primary game resources."));
    }
}
