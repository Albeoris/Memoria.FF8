using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Fits the active GF's actual posed body using the same vertex projector and viewport as its native draw.</summary>
public static class NativeBattleGuardianFormCamera
{
    public static ushort FitFocalLength(ushort maximum, NativeBattleCameraProjection projection, IReadOnlyList<NativeBattleActionMatrix> bones, ImmutableArray<NativeBattleRawMesh> geometry)
    {
        if (maximum == 0 || maximum > short.MaxValue || geometry.IsDefaultOrEmpty || !geometry.Any(mesh => !mesh.Vertices.IsEmpty))
        {
            throw new InvalidDataException("GF camera framing requires a positive source focal length and body vertices.");
        }
        // Native5099D0 projects the body into this same original screen rectangle.
        NativeBattleGeometryViewport viewport = new(0, 0, 320, 216);
        bool Fits(ushort focal)
        {
            NativeBattleCameraProjection candidate = projection with { FocalLength = focal };
            foreach (NativeBattleRawMesh mesh in geometry)
            {
                foreach (NativeBattleRawVertex vertex in mesh.Vertices)
                {
                    if (vertex.Bone >= bones.Count)
                    {
                        throw new InvalidDataException("GF camera body vertex has no posed bone matrix.");
                    }
                    NativeBattleProjectedVertex projected = NativeBattleGeometryProjection.Project50F900(bones[vertex.Bone], vertex.Position, candidate, viewport);
                    if ((projected.DepthAndClip & 0x3020000) != 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        if (Fits(maximum))
        {
            return maximum;
        }
        if (!Fits(1))
        {
            throw new NotSupportedException("GF body cannot fit the current camera's forward view without moving its eye or target.");
        }
        int lower = 1;
        int upper = maximum - 1;
        while (lower < upper)
        {
            int middle = lower + (upper - lower + 1) / 2;
            if (Fits(checked((ushort)middle)))
            {
                lower = middle;
            }
            else
            {
                upper = middle - 1;
            }
        }
        return checked((ushort)lower);
    }
}
