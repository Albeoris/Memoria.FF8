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
    private NativeFieldTextureAnimations? eyeTextureAnimations;

    private IReadOnlyList<PixelImage>? RegisteredEyeTextures(ActorState actor, int modelId)
    {
        if (modelId < 0 || !Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId))
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
        bool nativePlanes = Field?.NativeRegistration?.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash;
        if (!nativePlanes && !Events.Actors.Any(item => item.NativeEyes is { Mode: not 0 }))
        {
            return null;
        }
        FieldAssets source = Field ?? throw new InvalidDataException("Native eye drawing lost its original field assets.");
        NativeFieldRegisteredInstance instance = registration.Instances.Single(item => item.ObjectId == actor.ObjectId);
        if (instance.ModelId != modelId)
        {
            throw new NotSupportedException("Native eye drawing requires the actually registered model binding.");
        }
        NativeFieldTextureAnimations? animations = nativePlanes ? assets.PrepareNativeFieldMaterialBinding(source, registration, process.Memory.FieldTextureAnimations, modelId) : process.Memory.FieldTextureAnimations;
        if (!ReferenceEquals(animations, process.Memory.FieldTextureAnimations))
        {
            Events.PublishNativeFieldTextureAnimations(animations!);
        }
        if (eyeTextureState != textures || eyeTextureSource != source || nativePlanes && !ReferenceEquals(eyeTextureAnimations, animations))
        {
            eyeTextureModels = assets.BindNativeRegisteredTextures(source, registration, textures, animations).Models;
            eyeTextureState = textures;
            eyeTextureSource = source;
            eyeTextureAnimations = animations;
        }
        if (nativePlanes && eyeTextureModels![modelId].Textures is ResourceTextureList selected)
        {
            return selected.Snapshot();
        }
        return modelId >= 0 ? eyeTextureModels![modelId].Textures : null;
    }
}
