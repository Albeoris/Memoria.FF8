using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeBattleEffectTextureState? eyeTextureState;
    private FieldAssets? eyeTextureSource;
    private IReadOnlyList<FieldModel>? eyeTextureModels;

    private IReadOnlyList<PixelImage>? RegisteredEyeTextures(ActorState actor, int modelId)
    {
        if (modelId < 0 || !Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || !Events.Actors.Any(item => item.NativeEyes is { Mode: not 0 }))
        {
            return null;
        }
        if (Events.Scene.NativeEyeProcess is not { } process || Events.Scene.NativeFieldRegistration is not { } registration || process.Memory.EffectMemory?.Textures is not { } textures)
        {
            if (actor.NativeEyes is { Mode: not 0 })
            {
                throw new NotSupportedException("Native eye drawing requires its actual registered CPU and VRAM owners.");
            }
            return null;
        }
        FieldAssets source = Field ?? throw new InvalidDataException("Native eye drawing lost its original field assets.");
        if (eyeTextureState != textures || eyeTextureSource != source)
        {
            eyeTextureModels = assets.BindNativeRegisteredTextures(source, registration, textures, process.Memory.FieldTextureAnimations).Models;
            eyeTextureState = textures;
            eyeTextureSource = source;
        }
        NativeFieldRegisteredInstance instance = registration.Instances.Single(item => item.ObjectId == actor.ObjectId);
        if (instance.ModelId != modelId)
        {
            throw new NotSupportedException("Native eye drawing requires the actually registered model binding.");
        }
        return modelId >= 0 ? eyeTextureModels![modelId].Textures : null;
    }
}
