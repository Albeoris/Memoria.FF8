using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleForeignModelDrawing
{
    public static NativeBattleGfGeometryBatch ClipPlane(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, NativeBattleShivaPlane plane, ImmutableArray<int> selectors, uint color, short zsf3, short zsf4, short dqa, int dqb, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Host plane clipping requires its current view matrix.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int faceIndex = 0; faceIndex < geometry.Faces.Length; faceIndex++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[faceIndex];
            NativeBattleVector[] points = [face.A, face.B, face.C];
            ImmutableArray<NativeBattleShivaClipVertex> triangle = points.Select(point => new NativeBattleShivaClipVertex(NativeBattleGfMatrices.Transform460840(geometry.World, point), 0)).ToImmutableArray();
            NativeBattleModuleProjectedVertex[] projected = triangle.Select(vertex => NativeBattleModuleProjection.Project45FE10(view, vertex.Position, projection)).ToArray();
            if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) != 0 || NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
            {
                continue;
            }
            Dictionary<NativeBattleShivaClipVertex, (Vector2 Uv, Vector4 Color)> corners = new(ReferenceEqualityComparer.Instance);
            for (int corner = 0; corner < 3; corner++)
            {
                corners.Add(triangle[corner], (face.Surface.Uv[corner], face.Surface.Colors[corner]));
            }
            ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0(triangle, plane, selectors, (first, second, intersection, ratio) =>
            {
                (Vector2 firstUv, Vector4 firstColor) = corners[first];
                (Vector2 secondUv, Vector4 secondColor) = corners[second];
                float fraction = ratio / 4096f;
                corners.Add(intersection, (Vector2.Lerp(firstUv, secondUv, fraction), Vector4.Lerp(firstColor, secondColor, fraction)));
            });
            if (clipped.IsEmpty)
            {
                continue;
            }
            NativeBattleGfGeometryAttempt emitted = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, view, projection, color, 0, 0, zsf3, zsf4, dqa, dqb, allocated);
            if (emitted.Primitive is { } primitive)
            {
                int topology = NativeBattleShivaVisualIdentity.ClipTopology(triangle.Select(vertex => vertex.Position), plane);
                NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked((identity.Primitive * 65537 + faceIndex) * 31 + topology) };
                NativeBattleForeignSurface surface = new(face.Surface.Material, clipped.Select(vertex => corners[vertex].Uv).ToImmutableArray(), clipped.Select(vertex => corners[vertex].Color).ToImmutableArray());
                Vector3 Point(int corner) => new(clipped[corner].Position.X, clipped[corner].Position.Y, clipped[corner].Position.Z);
                emitted = emitted with { Primitive = primitive with { PilotGeometry = new(selected, Point(0), Point(1), Point(2), clipped.Length == 4 ? Point(3) : default, checked((byte)clipped.Length)) { ForeignSurface = surface } } };
            }
            attempts.Add(emitted);
            allocated = checked(allocated + emitted.PacketLength);
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
