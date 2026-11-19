using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    public PreparedFieldTextures? PrepareTexturePublication(IReadOnlyDictionary<TextureKey, TextureValue> changed, long generation) => renderer?.PrepareTexturePublication(changed, generation);
}
