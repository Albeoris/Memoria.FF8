using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

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
    /// <summary>Keep texture interpolation inside covered primitives at multisampled edges without moving the depth evaluation point.</summary>
    public bool CentroidTextureSampling { get; init; }
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

/// <summary>Ordered screen-space line segments using the same material and depth state as triangles.</summary>
public interface ILineRenderer
{
    void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material);
}

/// <summary>Optional readback of the active scene after pending triangles have been submitted.</summary>
public interface IFramebufferCaptureRenderer
{
    bool TryCaptureArgb(out uint[] pixels);

    bool TryCaptureArgb(int sourceWidth, int sourceHeight, PixelBounds sourceBounds, int destinationWidth, int destinationHeight, out uint[] pixels)
    {
        ValidateCaptureBounds(sourceWidth, sourceHeight, sourceBounds, destinationWidth, destinationHeight);
        if (!TryCaptureArgb(out uint[] source))
        {
            pixels = [];
            return false;
        }
        pixels = SampleArgbCentres(source, sourceWidth, sourceHeight, sourceBounds, destinationWidth, destinationHeight);
        return true;
    }

    static void ValidateCaptureBounds(int sourceWidth, int sourceHeight, PixelBounds sourceBounds, int destinationWidth, int destinationHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0 || sourceBounds.X < 0 || sourceBounds.Y < 0 || sourceBounds.Width <= 0 || sourceBounds.Height <= 0 || sourceBounds.X > sourceWidth - sourceBounds.Width || sourceBounds.Y > sourceHeight - sourceBounds.Height)
        {
            throw new InvalidDataException("Framebuffer centre capture requires positive dimensions and a complete crop inside its source raster.");
        }
        _ = checked(sourceWidth * sourceHeight);
        _ = checked(destinationWidth * destinationHeight);
    }

    static uint[] SampleArgbCentres(ReadOnlySpan<uint> source, int sourceWidth, int sourceHeight, PixelBounds sourceBounds, int destinationWidth, int destinationHeight)
    {
        ValidateCaptureBounds(sourceWidth, sourceHeight, sourceBounds, destinationWidth, destinationHeight);
        if (source.Length != checked(sourceWidth * sourceHeight))
        {
            throw new InvalidDataException("Framebuffer centre capture requires the complete current source raster.");
        }
        uint[] pixels = new uint[checked(destinationWidth * destinationHeight)];
        for (int y = 0; y < destinationHeight; y++)
        {
            int sourceY = sourceBounds.Y + Math.Min(sourceBounds.Height - 1, (int)(((long)y * 2 + 1) * sourceBounds.Height / (2L * destinationHeight)));
            for (int x = 0; x < destinationWidth; x++)
            {
                int sourceX = sourceBounds.X + Math.Min(sourceBounds.Width - 1, (int)(((long)x * 2 + 1) * sourceBounds.Width / (2L * destinationWidth)));
                pixels[y * destinationWidth + x] = source[sourceY * sourceWidth + sourceX];
            }
        }
        return pixels;
    }
}
