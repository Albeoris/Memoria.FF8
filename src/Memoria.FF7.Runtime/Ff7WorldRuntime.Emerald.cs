namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private const int EmeraldWeaponModel = 30;
    private const int ApproachBubbleLifetime = 127;
    private Ff7WorldApproachBubble? approachBubble;
    private int whiteFadeCallbacks;
    private bool whiteFadeDisplayed;

    private void AdvanceEmeraldPresentation()
    {
        if (approachBubble is { } bubble)
        {
            approachBubble = bubble.Age + 1 < ApproachBubbleLifetime ? bubble with { Age = bubble.Age + 1, Y = bubble.Y + 50 } : null;
        }
        if (Resources.World != 2 || !entities.TryGetValue(EmeraldWeaponModel, out Ff7WorldEntity? emerald))
        {
            return;
        }
        emerald = TurnWorldPlayer(emerald, emerald.TargetDirection);
        if (mainState >= 1 && (emerald.Flags & 8) == 0 && entities.TryGetValue(controlledModel, out Ff7WorldEntity? player)
            && visibility.RequiredChunks(player.X, player.Z, camera.Heading, underwater: true).Any(chunk => chunk.ChunkX == emerald.X >> 13 && chunk.ChunkZ == emerald.Z >> 13))
        {
            emerald = AdvanceWorldModelAnimation(emerald with { Flags = emerald.Flags | 2 });
        }
        UpdateEntity(emerald);
    }
}
