using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicActorVertexColor20
{
    /// <summary>Writes the active RGB bytes or the complete inactive DWORD, returning the written byte count.</summary>
    public static int WriteGlow(Span<byte> destination, ushort storedDistance, short radius, short intensity, short phase, uint actorColor, NativeBattleVector reference, ImmutableArray<uint> ramp)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("The vertex color requires its complete retained DWORD.", nameof(destination));
        }
        if (unchecked((short)storedDistance) >= radius)
        {
            destination.Slice(0, 4).Clear();
            return 4;
        }
        uint color = NativeBattleCinematicMonsterVertices.Glow(storedDistance, radius, intensity, phase, actorColor, reference, ramp);
        destination[0] = (byte)color;
        destination[1] = (byte)(color >> 8);
        destination[2] = (byte)(color >> 16);
        return 3;
    }
}
