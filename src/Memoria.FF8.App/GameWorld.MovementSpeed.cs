using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private int worldMovementSpeedPercent = PlayerMovementSpeed.DefaultPercent;

    public void ConfigureMovementSpeeds(int fieldPercent, int worldPercent)
    {
        PlayerMovementSpeed.ValidatePercent(fieldPercent);
        PlayerMovementSpeed.ValidatePercent(worldPercent);
        Events.PlayerMovementSpeedPercent = fieldPercent;
        worldMovementSpeedPercent = worldPercent;
        if (worldScene is not null)
        {
            worldScene.PlayerMovementSpeedPercent = worldPercent;
        }
    }
}
