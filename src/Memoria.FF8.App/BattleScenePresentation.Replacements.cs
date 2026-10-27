using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class BattleScenePresentation
{
    private object[]? stageReplacementMaterials;
    private IReadOnlyList<NativeBattleTextureReplacement> stageReplacements = [];

    private IReadOnlyList<NativeBattleTextureReplacement> NativeModelReplacements(NativeBattlePreparedPresentation prepared)
    {
        List<NativeBattleTextureReplacement> result = [];
        BattleStage replacementStage = prepared.StageResourceId is int stageId ? assets.LoadStage(stageId) : stage;
        object[] stageMaterials = [.. Materials(replacementStage.Textures), .. replacementStage.NativePages.Values.Select(handle => (object)handle.Value)];
        if (stageReplacementMaterials is null || !stageReplacementMaterials.SequenceEqual(stageMaterials))
        {
            stageReplacementMaterials = stageMaterials;
            stageReplacements = NativeBattleTextureReplacements.Bind(replacementStage);
        }
        long animationCallback = Math.Max(0, prepared.Callback - (prepared.StageTextureAnimationStartCallback ?? prepared.Callback));
        if (stageReplacements.Any(replacement => replacement.SourceOwner is not null) && prepared.StageResourceId is null)
        {
            throw new NotSupportedException("Stage page replacements require the retained current stage resource identity; explicitly migrate this snapshot or load the original stage resource.");
        }
        if (stageReplacements.Any(replacement => replacement.Animation is not null) && prepared.StageTextureAnimationStartCallback is null)
        {
            throw new NotSupportedException("Animated stage pages require their retained presentation clock epoch; explicitly migrate this snapshot or load the original stage resource.");
        }
        TimeSpan animationClock = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(animationCallback);
        result.AddRange(stageReplacements.Select(replacement => replacement.Animation is { } animation ? replacement with { Image = animation.At(animationClock) } : replacement));
        foreach (NativeBattlePreparedActor actor in prepared.Actors)
        {
            string actorId = actor.Id;
            if (!definitions.ContainsKey(actorId))
            {
                definitions.Add(actorId, new() { Id = actor.Id, Name = actor.Name, IsPlayer = actor.IsPlayer, ModelId = actor.ModelId, Costume = actor.Costume, WeaponId = actor.WeaponId, SceneSlot = actor.SceneSlot });
            }
            EnsureActorResources(actorId, actor.GuardianFormVisual);
            Add(models[actorId], actor.BodyTextures, false);
            if (weapons[actorId] is { } weapon && actor.LinkedTextures is { } weaponBinding)
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
