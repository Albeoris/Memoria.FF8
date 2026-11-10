using Memoria.FF7.Formats;
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
        CharacterMappingAssociations.Validate(mappings);
        return new Selection(mappings, installation, assets ?? throw new InvalidOperationException("A mapped character requires the primary game resources."));
    }

    private sealed class Selection(CharacterAppearanceMapping[] mappings, Ff7Installation installation, GameAssets assets) : IFf7ActorAppearance
    {
        private readonly Dictionary<(int Story, int Appearance, string Hrc, bool World), Ff7ModelAppearance> profiles = [];

        public bool SupportsPartVisibility => true;

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [];
            int character = actor.PartyCharacter is >= 0 and <= 8 ? actor.PartyCharacter : CrossoverCharacterCatalog.IdentifyStoryCharacter(descriptor);
            CharacterAppearanceMapping? mapping = mappings.SingleOrDefault(mapping => mapping.StoryCharacter == character);
            if (mapping is null)
            {
                return false;
            }
            string hrc = Path.GetFileName(descriptor.Hrc).ToLowerInvariant();
            var key = (character, mapping.AppearanceCharacter, hrc, actor.IsWorldModel);
            if (!profiles.TryGetValue(key, out Ff7ModelAppearance? profile))
            {
                bool legacy = !actor.IsWorldModel && mapping.RigProfile is Ff7ModelAppearance.StandardFieldSquallProfileVersion or Ff7ModelAppearance.LegacyStandardFieldSquallProfileVersion;
                profile = legacy ? Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets) : Ff7ModelAppearance.CreateFieldCharacter(assets, mapping.AppearanceCharacter, descriptor, original, actor.IsWorldModel);
                profiles.Add(key, profile);
            }
            return profile.TryCreate(actor, descriptor, original, out triangles);
        }
    }
}
