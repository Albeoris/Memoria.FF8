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
        BattleStage replacementStage = session.NativeStage?.ResourceStageId is int stageId ? assets.LoadStage(stageId) : stage;
        object[] stageMaterials = [.. Materials(replacementStage.Textures), .. replacementStage.NativePages.Values.Select(handle => (object)handle.Value)];
        if (stageReplacementMaterials is null || !stageReplacementMaterials.SequenceEqual(stageMaterials))
        {
            stageReplacementMaterials = stageMaterials;
            stageReplacements = NativeBattleTextureReplacements.Bind(replacementStage);
        }
        long animationCallback = Math.Max(0, graphics.Callback - (session.NativeStage?.TextureAnimationStartCallback ?? graphics.Callback));
        if (stageReplacements.Any(replacement => replacement.SourceOwner is not null) && session.NativeStage?.ResourceStageId is null)
        {
            throw new NotSupportedException("Stage page replacements require the retained current stage resource identity; explicitly migrate this snapshot or load the original stage resource.");
        }
        if (stageReplacements.Any(replacement => replacement.Animation is not null) && session.NativeStage?.TextureAnimationStartCallback is null)
        {
            throw new NotSupportedException("Animated stage pages require their retained presentation clock epoch; explicitly migrate this snapshot or load the original stage resource.");
        }
        TimeSpan animationClock = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(animationCallback);
        result.AddRange(stageReplacements.Select(replacement => replacement.Animation is { } animation ? replacement with { Image = animation.At(animationClock) } : replacement));
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
