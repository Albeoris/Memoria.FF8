using System.Numerics;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.GameUi;

internal sealed class Ff7TargetScanModel
{
    private readonly Ff7BattleActorGeometry geometry;
    private readonly Vector3 center;
    private readonly float radius;

    public Ff7TargetScanModel(Ff7BattleActorGeometry geometry)
    {
        this.geometry = geometry;
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        foreach ((Vector3 a, Vector3 b, Vector3 c) in geometry.FaceVertices())
        {
            min = Vector3.Min(min, Vector3.Min(a, Vector3.Min(b, c)));
            max = Vector3.Max(max, Vector3.Max(a, Vector3.Max(b, c)));
        }
        center = (min + max) / 2;
        radius = Vector3.Distance(min, max) / 2;
    }

    public bool IsVisible => float.IsFinite(radius) && radius > 0;

    public void Render(ITriangleRenderer renderer, float yaw, float pitch)
    {
        const int width = TargetScanScreen.ModelWidth;
        const int height = TargetScanScreen.ModelHeight;
        float distance = Math.Max(2, radius * 8);
        float focal = height * 0.46f * distance / radius;
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
        renderer.Begin(width, height, Vector4.Zero, clearDepth: 0);
        try
        {
            foreach (Ff7BattleActorFace face in geometry.Faces)
            {
                if (face.Surface is not { Material: NativeBattleForeignRenderMaterial surface } geometryMaterial)
                {
                    continue;
                }
                TriangleMaterial material = surface.Material with { PreserveTargetAlpha = false, CompositeTargetAlpha = true, Blend = TriangleBlend.Alpha };
                CameraSpaceTriangleSubmission.Draw(renderer, Vertex(face.A, 0), Vertex(face.B, 1), Vertex(face.C, 2), width, height, focal, material, surface.Cull, clockwiseFront: true);

                CameraSpaceVertex Vertex(Vector3 point, int index)
                {
                    Vector3 position = Vector3.Transform(point - center, rotation);
                    return new(new(position.X, position.Y, distance - position.Z), geometryMaterial.Uv[index], geometryMaterial.Colors[index]);
                }
            }
        }
        finally
        {
            renderer.End();
        }
    }
}
