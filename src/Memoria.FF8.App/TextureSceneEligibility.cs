using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class TextureSceneEligibility
{
    internal static bool CanCapture(SceneKind scene, bool inFf7, bool battlePresented, bool fieldPresented, bool worldPresented, bool faulted, bool exclusivePresentation)
    {
        if (inFf7 || faulted || exclusivePresentation || scene == SceneKind.Movie)
        {
            return false;
        }
        return battlePresented || scene == SceneKind.Field && fieldPresented || scene == SceneKind.WorldMap && worldPresented;
    }
}
