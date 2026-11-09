using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class Ff7ModelAppearance
{
    internal static PixelImage? EyeMaterial(Ff7ActorVisual actor, FieldModel target)
    {
        if (actor.NativeEyeLeft == 0 && actor.NativeEyeRight == 0)
        {
            return null;
        }
        if (actor.NativeEyeLeft != actor.NativeEyeRight || actor.NativeEyeLeft is not (1 or 2))
        {
            throw new NotSupportedException("The selected field Squall eye bank requires matching left/right open or closed eye modes.");
        }
        return FieldEyeMaterial.Resolve(target, actor.NativeEyeLeft == 2);
    }
}
