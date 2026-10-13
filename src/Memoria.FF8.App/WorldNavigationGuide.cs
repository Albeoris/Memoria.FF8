using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>A geometric suggestion for ordinary eight-direction input, with no geometry or game-state writes.</summary>
internal static class WorldNavigationGuide
{
    public static PlayerAction Next(WorldState current, int targetX, int targetRawZ)
    {
        double dx = targetX - (double)current.X;
        double dz = targetRawZ - (double)current.Z;
        double angle = current.CameraYaw * Math.Tau / 4096;
        double best = double.NegativeInfinity;
        PlayerAction chosen = new(PlayerActionKind.Wait);
        for (int horizontal = -1; horizontal <= 1; horizontal++)
        {
            for (int vertical = -1; vertical <= 1; vertical++)
            {
                if (horizontal == 0 && vertical == 0)
                {
                    continue;
                }
                double x = Math.Cos(angle) * horizontal - Math.Sin(angle) * vertical;
                double z = Math.Sin(angle) * horizontal + Math.Cos(angle) * vertical;
                double score = (dx * x + dz * z) / Math.Sqrt(x * x + z * z);
                if (score > best)
                {
                    best = score;
                    chosen = new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical);
                }
            }
        }
        return chosen;
    }
}
