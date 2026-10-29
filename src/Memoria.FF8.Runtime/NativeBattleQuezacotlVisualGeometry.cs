using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleQuezacotlVisualGeometry
{
    internal static NativeBattlePilotVisualIdentity RowIdentity(NativeBattleEffectArena arena, uint bank, int first, int second)
    {
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(116, arena, bank, first, second);
        return identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(116, arena, bank, second).Generation };
    }

    internal static NativeBattleGfGeometryAttempt CaptureOutline(NativeBattleGfGeometryAttempt attempt, NativeBattlePilotVisualIdentity identity, Vector3 first, Vector3 second, uint firstCenter, uint secondCenter, uint leftA, uint rightA, uint leftB, uint rightB, ImmutableArray<int> sourceDepths)
    {
        if (attempt.Primitive is not { } primitive)
        {
            return attempt;
        }
        Vector2 Offset(uint corner, uint center) => new(unchecked((short)corner) - unchecked((short)center), unchecked((short)(corner >> 16)) - unchecked((short)(center >> 16)));
        // The native outline is authored in projected eighth-pixels. Retain its offsets independently of the moving source centers.
        NativeBattlePilotVisualGeometry geometry = new(identity, first, first, second, second, 4)
        {
            ScreenOffsets = [Offset(leftA, firstCenter), Offset(rightA, firstCenter), Offset(leftB, secondCenter), Offset(rightB, secondCenter)],
            SourceDepths = new(sourceDepths[0], sourceDepths[1], sourceDepths[2], sourceDepths[3]),
            UseSourceDepths = true
        };
        return attempt with { Primitive = primitive with { PilotGeometry = geometry } };
    }
}
