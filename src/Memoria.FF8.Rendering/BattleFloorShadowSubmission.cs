using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Submits an authenticated floor-shadow triangle through the ordinary scene depth contract.</summary>
public static class BattleFloorShadowSubmission
{
    public static void Draw(ITriangleRenderer renderer, Vector3 a, Vector3 b, Vector3 c, Vector4 colorA, Vector4 colorB, Vector4 colorC, Func<Vector3, Vector3> cameraTransform, int width, int height, float focal, TriangleBlend blend, Func<float, float>? depthMapping = null)
    {
        TriangleMaterial material = new(null, blend, TriangleDepthComparison.GreaterEqual, false) { PreserveTargetAlpha = false, TextureSelectsAlpha = false };
        CameraSpaceTriangleSubmission.Draw(depthMapping is null ? renderer : new DepthMappedRenderer(renderer, depthMapping), new(cameraTransform(a), default, colorA), new(cameraTransform(b), default, colorB), new(cameraTransform(c), default, colorC), width, height, focal, material);
    }

    private sealed class DepthMappedRenderer(ITriangleRenderer renderer, Func<float, float> mapping) : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            throw new InvalidOperationException("A shadow depth adapter only submits into an active scene.");
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            float first = mapping(1 / a.Depth);
            float second = mapping(1 / b.Depth);
            float third = mapping(1 / c.Depth);
            if (first <= 0 || second <= 0 || third <= 0)
            {
                return;
            }
            renderer.Draw(a with { Depth = 1 / first }, b with { Depth = 1 / second }, c with { Depth = 1 / third }, material);
        }

        public void End()
        {
            throw new InvalidOperationException("A shadow depth adapter does not own the active scene.");
        }
    }
}
