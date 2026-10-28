using System.Collections.Immutable;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Publishes the primary runtime's native GF page authority independently of model-resource selection.</summary>
public interface INativeBattleGfAtlasSource
{
    TextureSourceAtlasAddress? TryGetGfAtlasAddress(ImmutableArray<NativeBattleTextureRegistration> registrations, ushort page, ushort clut, int x, int y, int width, int height, TexturePrimitiveSample? sample = null);
}
