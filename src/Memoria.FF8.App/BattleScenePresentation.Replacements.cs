using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class BattleScenePresentation
{
    private object[]? stageReplacementMaterials;
    private IReadOnlyList<NativeBattleTextureReplacement> stageReplacements = [];

    private IReadOnlyList<NativeBattleTextureReplacement> NativeModelReplacements(BattleSession session, NativeBattleGraphicsState graphics)
    {
        List<NativeBattleTextureReplacement> result = [];
        object[] stageMaterials = Materials(stage.Textures);
        if (stageReplacementMaterials is null || !stageReplacementMaterials.SequenceEqual(stageMaterials))
        {
            stageReplacementMaterials = stageMaterials;
            stageReplacements = NativeBattleTextureReplacements.Bind(stage);
        }
        result.AddRange(stageReplacements);
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            if (graphics.ActorIds[index] is not string actorId || graphics.Models[index].BodyTextureSlots is not { } bodyBinding)
            {
                continue;
            }
            if (!definitions.ContainsKey(actorId))
            {
                definitions.Add(actorId, session.GetCombatantDefinition(actorId));
            }
            EnsureActorResources(actorId, session.GetGuardianFormVisual(actorId));
            Add(models[actorId], bodyBinding, false);
            if (weapons[actorId] is { } weapon && graphics.Models[index].LinkedTextureSlots is { } weaponBinding)
            {
                Add(weapon, weaponBinding, true);
            }
        }
        return result;

        void Add(BattleModel model, NativeBattleModelTextureBinding binding, bool weapon)
        {
            var key = (model, binding, weapon);
            object[] materials = Materials(model.Textures);
            if (!replacementBindings.TryGetValue(key, out var cached) || !cached.Materials.SequenceEqual(materials))
            {
                cached = (materials, NativeBattleTextureReplacements.Bind(model, binding, weapon));
                replacementBindings[key] = cached;
            }
            result.AddRange(cached.Replacements);
        }
    }

    private static object[] Materials(IReadOnlyList<PixelImage> textures) => textures is ResourceTextureList resources ? Enumerable.Range(0, resources.Count).Select(index => (object)resources.GetTexture(index)).ToArray() : textures.Cast<object>().ToArray();
}
