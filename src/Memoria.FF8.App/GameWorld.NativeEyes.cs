using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeBattleEffectTextureState? registeredTextureState;
    private FieldAssets? registeredTextureSource;
    private IReadOnlyList<FieldModel>? registeredTextureModels;
    private NativeFieldTextureAnimations? registeredTextureAnimations;

    private IReadOnlyList<PixelImage>? RegisteredNativeTextures(ActorState actor, int modelId)
    {
        if (modelId < 0 || !Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId))
        {
            return null;
        }
        if (Events.Scene.NativeEyeProcess is not { } process || Events.Scene.NativeFieldRegistration is not { } registration)
        {
            if (actor.NativeEyes is { Mode: not 0 })
            {
                throw new NotSupportedException("Native model drawing requires its actual registered CPU and VRAM owners.");
            }
            return null;
        }
        bool activeField = !session.HasActiveBattle && session.State.FieldId == Events.FieldId && session.State.Scene is SceneKind.Field or SceneKind.Movie;
        NativeBattleRetainedMemory memory = activeField ? session.CurrentNativeBattleMemory ?? throw new InvalidDataException("Native model drawing lost its active physical memory owner.") : process.Memory;
        NativeBattleEffectTextureState textures = memory.EffectMemory?.Textures ?? throw new InvalidDataException("Native model drawing lost its registered physical texture memory.");
        bool nativePlanes = Field?.NativeRegistration?.ExecutableHash == NativeFieldRuntimeProfile.RemasteredEnImageHash;
        FieldAssets source = Field ?? throw new InvalidDataException("Native model drawing lost its original field assets.");
        NativeFieldRegisteredInstance instance = registration.Instances.Single(item => item.ObjectId == actor.ObjectId);
        if (instance.ModelId != modelId)
        {
            throw new NotSupportedException("Native model drawing requires the actually registered model binding.");
        }
        NativeFieldTextureAnimations? animations = nativePlanes ? assets.PrepareNativeFieldMaterialBinding(source, registration, memory.FieldTextureAnimations, modelId) : memory.FieldTextureAnimations;
        if (!ReferenceEquals(animations, memory.FieldTextureAnimations))
        {
            Events.PublishNativeFieldTextureAnimations(animations!);
        }
        if (registeredTextureState != textures || registeredTextureSource != source || nativePlanes && !ReferenceEquals(registeredTextureAnimations, animations))
        {
            registeredTextureModels = assets.BindNativeRegisteredTextures(source, registration, textures, animations).Models;
            registeredTextureState = textures;
            registeredTextureSource = source;
            registeredTextureAnimations = animations;
        }
        if (nativePlanes && registeredTextureModels![modelId].Textures is ResourceTextureList selected)
        {
            return selected.Snapshot();
        }
        return registeredTextureModels![modelId].Textures;
    }
}
