using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class BattleScenePresentation
{
    private IReadOnlyList<NativeBattleTextureReplacement> NativeModelReplacements(BattleSession session, NativeBattleGraphicsState graphics)
    {
        List<NativeBattleTextureReplacement> result = [];
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
            EnsureActorResources(actorId);
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
            PixelImage[] images = model.Textures.ToArray();
            if (!replacementBindings.TryGetValue(key, out var cached) || !cached.Images.SequenceEqual(images))
            {
                cached = (images, NativeBattleTextureReplacements.Bind(model, binding, weapon));
                replacementBindings[key] = cached;
            }
            result.AddRange(cached.Replacements);
        }
    }
}
