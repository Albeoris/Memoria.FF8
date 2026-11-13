using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Resolves deferred arena images in an owned offscreen renderer, independently of the display camera.</summary>
public sealed class CrossoverArenaSurfaceCapture(Func<ITriangleRenderer> createRenderer) : IDisposable
{
    private const int MaximumImages = 64;
    private const long MaximumImageBytes = 64L * 1024 * 1024;
    private readonly Dictionary<NativeBattleArenaCaptureMaterial, NativeBattleForeignRenderMaterial> images = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<NativeBattleArenaCaptureMaterial> order = [];
    private ITriangleRenderer? renderer;
    private long imageBytes;
    private bool disposed;

    public int RetainedImages => images.Count;
    public long RetainedImageBytes => imageBytes;

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        (renderer as IDisposable)?.Dispose();
        renderer = null;
        images.Clear();
        order.Clear();
        imageBytes = 0;
    }

    /// <summary>Prepare captures before opening the display render pass.</summary>
    public void Prepare(IEnumerable<INativeBattleForeignMaterial> materials)
    {
        foreach (INativeBattleForeignMaterial material in materials)
        {
            Resolve(material);
        }
    }

    public NativeBattleForeignRenderMaterial Resolve(INativeBattleForeignMaterial material)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (material is NativeBattleForeignRenderMaterial original)
        {
            return original;
        }
        if (material is not NativeBattleArenaCaptureMaterial request)
        {
            throw new NotSupportedException("Arena sampling requires an original host material or an explicit surface capture request.");
        }
        if (images.TryGetValue(request, out NativeBattleForeignRenderMaterial? retained))
        {
            return retained;
        }
        request.Validate();
        long bytes = checked((long)request.Width * request.Height * 4);
        if (bytes > MaximumImageBytes)
        {
            throw new NotSupportedException("The arena surface image exceeds the bounded capture budget.");
        }
        renderer ??= createRenderer();
        if (renderer is not IFramebufferCaptureRenderer capture || renderer is not IDisposable)
        {
            throw new NotSupportedException("Arena capture requires an owned offscreen renderer with framebuffer readback.");
        }
        renderer.Begin(request.Width, request.Height, new Vector4(0, 0, 0, 1), 0);
        uint[] pixels;
        try
        {
            Draw(request, renderer);
            if (!capture.TryCaptureArgb(out pixels) || pixels.Length != checked(request.Width * request.Height))
            {
                throw new NotSupportedException("The original arena surface could not be captured from its offscreen framebuffer.");
            }
        }
        finally
        {
            renderer.End();
        }
        PixelImage image = new(request.Width, request.Height);
        for (int index = 0; index < pixels.Length; index++)
        {
            uint pixel = pixels[index];
            image.Pixels[index] = new((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel, (byte)(pixel >> 24));
        }
        NativeBattleForeignRenderMaterial result = new(new(image, DepthComparison: TriangleDepthComparison.GreaterEqual, Sampler: TriangleSampler.PointClamp), false);
        while (images.Count >= MaximumImages || imageBytes > MaximumImageBytes - bytes)
        {
            NativeBattleArenaCaptureMaterial oldest = order.Dequeue();
            images.Remove(oldest);
            imageBytes -= checked((long)oldest.Width * oldest.Height * 4);
        }
        images.Add(request, result);
        order.Enqueue(request);
        imageBytes += bytes;
        return result;
    }

    private static void Draw(NativeBattleArenaCaptureMaterial request, ITriangleRenderer target)
    {
        NativeBattleActionMatrix view = request.Projection.View!;
        view.Validate();
        float focal = request.Projection.FocalLength!.Value;
        short offsetX = request.Projection.OffsetX ?? throw new NotSupportedException("Arena capture requires its explicit horizontal projection offset.");
        short offsetY = request.Projection.OffsetY ?? throw new NotSupportedException("Arena capture requires its explicit vertical projection offset.");
        ITriangleRenderer projected = new ProjectionOffset(target, new(offsetX - request.Width / 2f, offsetY - request.Height / 2f));
        foreach (NativeBattleArenaFace face in request.Geometry.Faces)
        {
            NativeBattleForeignSurface surface = face.Surface;
            if (surface.Material is not NativeBattleForeignRenderMaterial material || surface.Uv.Length != 3 || surface.Colors.Length != 3)
            {
                throw new NotSupportedException("Arena capture requires original host triangle materials and complete corner attributes.");
            }
            CameraSpaceVertex Vertex(Vector3 point, int corner)
            {
                float Row(int row) => view.Rotation[row * 3] / 4096f * point.X + view.Rotation[row * 3 + 1] / 4096f * point.Y + view.Rotation[row * 3 + 2] / 4096f * point.Z;
                Vector3 position = new(Row(0) + view.X, Row(1) + view.Y, Row(2) + view.Z);
                return new(new(position.X, -position.Y, position.Z), surface.Uv[corner], surface.Colors[corner]);
            }
            CameraSpaceTriangleSubmission.Draw(projected, Vertex(face.A, 0), Vertex(face.B, 1), Vertex(face.C, 2), request.Width, request.Height, focal, material.Material, material.Cull, clockwiseFront: true);
        }
    }

    private sealed class ProjectionOffset(ITriangleRenderer target, Vector2 offset) : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            target.Draw(a with { Position = a.Position + offset }, b with { Position = b.Position + offset }, c with { Position = c.Position + offset }, material);
        }

        public void End()
        {
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        Reset();
        disposed = true;
    }
}

public sealed partial class BattleRenderer
{
    public Func<INativeBattleForeignMaterial, NativeBattleForeignRenderMaterial>? ForeignMaterialResolver { get; set; }
}
