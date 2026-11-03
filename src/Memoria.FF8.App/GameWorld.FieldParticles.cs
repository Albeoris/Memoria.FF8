using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Dictionary<int, NativeFieldParticleResources?> fieldParticleResources = [];
    private readonly Dictionary<int, PixelImage> fieldParticleTextures = [];

    private NativeFieldParticleResources? ReadFieldParticleResources(int fieldId)
    {
        if (!fieldParticleResources.TryGetValue(fieldId, out NativeFieldParticleResources? result))
        {
            result = assets.ReadNativeFieldParticleResources(fieldId);
            fieldParticleResources.Add(fieldId, result);
        }
        return result;
    }

    private PixelImage? ReadFieldParticleTexture()
    {
        if (ReadFieldParticleResources(session.State.FieldId) is not { } resources)
        {
            return null;
        }
        if (!fieldParticleTextures.TryGetValue(session.State.FieldId, out PixelImage? texture))
        {
            texture = resources.DecodeSprite(0, 0, 256, 256);
            fieldParticleTextures.Add(session.State.FieldId, texture);
        }
        return texture;
    }
}
