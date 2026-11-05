using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal void RestoreGameplayWorldUi(GameplaySaveWorldUi saved)
    {
        if (session.State.Scene != SceneKind.WorldMap || worldScene is null)
        {
            throw new InvalidOperationException("Gameplay world UI requires its allocated world host.");
        }
        worldScene.RestoreGameplayUi(saved);
    }
}
