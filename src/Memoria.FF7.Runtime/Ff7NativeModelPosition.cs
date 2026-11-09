using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeModelPosition
{
    /// <summary>Builds the native model presentation point without changing its logical actor or movement state.</summary>
    public static Vector3 PresentationPoint(Ff7Actor actor, Ff7NativeModelPresentation configuration) => new(
        unchecked((short)((actor.X4096 >> 12) + actor.OffsetX)),
        unchecked((short)((actor.Y4096 >> 12) + actor.OffsetY + configuration.ExtraY)),
        unchecked((short)((actor.Z4096 >> 12) + actor.OffsetZ + configuration.ExtraZ - 10)));

    public static Ff7Actor PrepareActorPresentation(Ff7Actor actor, Ff7NativeModelPresentation configuration)
    {
        Vector3 point = PresentationPoint(actor, configuration);
        return actor with
        {
            X4096 = checked((int)point.X * 4096),
            Y4096 = checked((int)point.Y * 4096),
            Z4096 = checked((int)point.Z * 4096),
            OffsetX = 0,
            OffsetY = 0,
            OffsetZ = 0,
            Visible = actor.Visible && configuration.Mask != 0
        };
    }
}
