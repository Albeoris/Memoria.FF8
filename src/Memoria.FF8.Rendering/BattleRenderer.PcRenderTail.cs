using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private NativeBattlePcFramebufferCaptureState pcCapture = NativeBattlePcFramebufferCaptureState.FromColdProcess();
    private NativeBattlePcFramebufferFormat? pcCaptureFormat;

    public NativeBattlePcFramebufferCaptureState PcFramebufferCapture => pcCapture;

    private void DrawNativePcRenderTail(NativeBattlePcRenderTailReceipt? receipt, NativeBattlePrimitiveCapabilities? capabilities)
    {
        if (receipt is null)
        {
            return;
        }
        receipt.Validate();
        NativeBattlePcFramebufferCaptureProfile profile = capabilities?.PcFramebufferCaptureProfile ?? throw new NotSupportedException("Native PC framebuffer tail requires its explicitly selected capture profile.");
        NativeBattlePcFramebufferCaptureMaterial material = profile.ResolveMaterial();
        if (material.LinearFiltering)
        {
            throw new NotSupportedException("The selected PC framebuffer tail requires the current point-filtered portable capture profile.");
        }
        material.ModulateSourceArgb(0, receipt.ColorArgb);
        TriangleBlend selectedBlend = material.Blend switch
        {
            { Source: 5, Destination: 6 } => TriangleBlend.Alpha,
            { Source: 5, Destination: 5 } => TriangleBlend.SourceAlphaBoth,
            { Source: 2, Destination: 1 } => TriangleBlend.Replace,
            { Source: 2, Destination: 2 } => TriangleBlend.Add,
            { Source: 5, Destination: 2 } => TriangleBlend.AlphaAdd,
            _ => throw new NotSupportedException("The PC framebuffer tail selected an unsupported explicit driver blend pair.")
        };
        if (pcCaptureFormat is not null && pcCaptureFormat != profile.Format)
        {
            throw new NotSupportedException("A retained PC capture texture cannot change device channel masks without an explicit resource recreation.");
        }
        pcCaptureFormat = profile.Format;
        uint[]? nativeSurface = null;
        bool locked;
        if (TriangleRenderer is null)
        {
            uint[] source = new uint[Width * Height];
            for (int index = 0; index < source.Length; index++)
            {
                int offset = index * 4;
                source[index] = (uint)(pixels[offset + 3] << 24 | pixels[offset] << 16 | pixels[offset + 1] << 8 | pixels[offset + 2]);
            }
            nativeSurface = IFramebufferCaptureRenderer.SampleArgbCentres(source, Width, Height, ProjectionBounds, profile.SurfaceWidth, profile.SurfaceHeight);
            locked = true;
        }
        else if (TriangleRenderer is IFramebufferCaptureRenderer capture)
        {
            locked = capture.TryCaptureArgb(Width, Height, ProjectionBounds, profile.SurfaceWidth, profile.SurfaceHeight, out nativeSurface);
        }
        else
        {
            throw new NotSupportedException("The current triangle backend does not provide the required active framebuffer capture service.");
        }
        if (locked)
        {
            if (nativeSurface is null || nativeSurface.Length != checked(profile.SurfaceWidth * profile.SurfaceHeight))
            {
                throw new InvalidDataException("PC framebuffer capture requires the complete selected native surface.");
            }
            byte[] words = NativeBattlePcFramebufferCaptureState.EncodeArgbSurface(nativeSurface, profile);
            pcCapture = pcCapture.Capture56D530(words, profile.SurfaceWidth * 2, receipt.Viewport, true);
        }
        else
        {
            pcCapture = pcCapture.Capture56D530([], 0, receipt.Viewport, false);
        }
        ImmutableArray<uint> captured = pcCapture.DecodeArgb(profile);
        Rgba32[] texels = new Rgba32[captured.Length];
        for (int index = 0; index < texels.Length; index++)
        {
            uint color = material.ModulateSourceArgb(captured[index], receipt.ColorArgb);
            texels[index] = new((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24));
        }
        PixelImage image = new(256, 256, texels);
        ImmutableArray<NativeBattlePcRenderVertex> native = receipt.QuadVertices56D5F0();
        TriangleVertex[] vertices = native.Select(vertex => new TriangleVertex(new(ProjectionBounds.X + (float)(((double)vertex.X + 0.5) * ProjectionBounds.Width / profile.SurfaceWidth), ProjectionBounds.Y + (float)(((double)vertex.Y + 0.5) * ProjectionBounds.Height / profile.SurfaceHeight)), vertex.Z, vertex.ReciprocalW, new(vertex.U, vertex.V), Vector4.One)).ToArray();
        if (TriangleRenderer is { } renderer)
        {
            TriangleMaterial triangleMaterial = new(image, selectedBlend, TriangleDepthComparison.LessEqual, false, 0, TriangleSampler.PointClamp);
            renderer.Draw(vertices[0], vertices[1], vertices[2], triangleMaterial);
            renderer.Draw(vertices[1], vertices[3], vertices[2], triangleMaterial);
        }
        else
        {
            RasterPcRenderTail(vertices, image, material.Blend);
        }
        DrawnTriangles += 2;
    }

    private void RasterPcRenderTail(TriangleVertex[] vertices, PixelImage image, NativeBattlePcFramebufferBlend blend)
    {
        float left = vertices[0].Position.X;
        float top = vertices[0].Position.Y;
        float right = vertices[3].Position.X;
        float bottom = vertices[3].Position.Y;
        if (left == right || top == bottom)
        {
            return;
        }
        float z = vertices[0].Depth;
        int x0 = Math.Clamp((int)MathF.Floor(Math.Min(left, right)), 0, Width);
        int x1 = Math.Clamp((int)MathF.Ceiling(Math.Max(left, right)), 0, Width);
        int y0 = Math.Clamp((int)MathF.Floor(Math.Min(top, bottom)), 0, Height);
        int y1 = Math.Clamp((int)MathF.Ceiling(Math.Max(top, bottom)), 0, Height);
        for (int y = y0; y < y1; y++)
        {
            float v = (y + 0.5f - top) / (bottom - top);
            if (v is < 0 or >= 1)
            {
                continue;
            }
            for (int x = x0; x < x1; x++)
            {
                float u = (x + 0.5f - left) / (right - left);
                int pixel = y * Width + x;
                if (u is < 0 or >= 1 || z > depth[pixel])
                {
                    continue;
                }
                Rgba32 texel = image[Math.Clamp((int)(u * 256), 0, 255), Math.Clamp((int)(v * 256), 0, 255)];
                float alpha = texel.A / 255f;
                float sourceFactor = Factor(blend.Source, alpha);
                float destinationFactor = Factor(blend.Destination, alpha);
                for (int component = 0; component < 3; component++)
                {
                    byte value = component == 0 ? texel.R : component == 1 ? texel.G : texel.B;
                    pixels[pixel * 4 + component] = (byte)Math.Clamp((int)(value * sourceFactor + pixels[pixel * 4 + component] * destinationFactor), 0, 255);
                }
            }
        }

        static float Factor(uint factor, float alpha) => factor switch
        {
            1 => 0,
            2 => 1,
            5 => alpha,
            6 => 1 - alpha,
            _ => throw new NotSupportedException("The PC framebuffer tail requires an admitted explicit driver blend factor.")
        };
    }
}
