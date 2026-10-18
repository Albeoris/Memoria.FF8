using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

/// <summary>Screen-space geometry. Depth and color interpolate affinely; only UV uses ReciprocalW.</summary>
public readonly record struct TriangleVertex(Vector2 Position, float Depth, float ReciprocalW, Vector2 Uv, Vector4 Color)
{
    /// <summary>Optional screen-space rectangle and source texel rectangle for point-sampled atlas quads.</summary>
    public Vector4 TextureScreenRectangle { get; init; }
    public Vector4 TextureTexelRectangle { get; init; }
}
public enum TriangleBlend
{
    Replace,
    Alpha,
    Add,
    AlphaAdd,
    HalfAdd,
    Subtract,
    QuarterAdd,
    SourceAlphaBoth
}
public enum TriangleDepthComparison
{
    Always,
    Less,
    LessEqual,
    Greater,
    GreaterEqual
}
public enum TriangleSampler
{
    PointClamp,
    PointWrap,
    LinearClamp,
    LinearWrap,
    CutoutLinearClamp
}
public enum TriangleDepthMapping
{
    Normalized,
    Positive
}

public readonly record struct TriangleMaterial(PixelImage? Texture, TriangleBlend Blend = TriangleBlend.Replace, TriangleDepthComparison DepthComparison = TriangleDepthComparison.LessEqual, bool DepthWrite = true, float MinimumAlpha = 1f / 255, TriangleSampler Sampler = TriangleSampler.PointWrap)
{
    public bool DynamicTexture { get; init; }
    public bool TextureSelectsAlpha { get; init; } = true;
    public float MaximumAlpha { get; init; } = 1;
    public float AlphaMultiplier { get; init; } = 1;
    public bool ColorWrite { get; init; } = true;
    public bool PreserveTargetAlpha { get; init; } = true;
    public bool PerspectiveDepth { get; init; }
    public Vector4? ClipRectangle { get; init; }
    public TriangleDepthMapping DepthMapping { get; init; }
    public float DepthScale { get; init; } = 1f / 65536;
    public Vector3 FogColor { get; init; }
    public float FogStart { get; init; }
    public float FogEnd { get; init; }
    public float MinimumReciprocalW { get; init; }
}

/// <summary>A scene's ordered triangle stream, independent of its raster backend.</summary>
public interface ITriangleRenderer
{
    void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1);
    void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material);
    void End();
}

/// <summary>Optional readback of the active scene after pending triangles have been submitted.</summary>
public interface IFramebufferCaptureRenderer
{
    bool TryCaptureArgb(out uint[] pixels);
}
