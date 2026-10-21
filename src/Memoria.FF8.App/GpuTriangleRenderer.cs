using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector4N = System.Numerics.Vector4;

namespace Memoria.FF8;

/// <summary>Ordered hardware triangle rasterizer; scene decoding remains in the shared rendering library.</summary>
public sealed partial class GpuTriangleRenderer : ITriangleRenderer, ILineRenderer, IFramebufferCaptureRenderer, IWorldTerrainRenderer, IDisposable
{
    private readonly GraphicsDevice device;
    private readonly Effect effect;
    private readonly Dictionary<PixelImage, Texture2D> textures = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<PixelImage> usedTextures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(TriangleBlend, bool, bool), BlendState> blends = [];
    private readonly Dictionary<(TriangleDepthComparison, bool), DepthStencilState> depths = [];
    private readonly GpuVertex[] vertices = new GpuVertex[12288];
    private readonly HashSet<PixelImage> dynamicUploads = new(ReferenceEqualityComparer.Instance);
    private TriangleMaterial pendingMaterial;
    private PrimitiveType pendingPrimitiveType;
    private int vertexCount;
    private RenderTargetBinding[] previousTargets = [];
    private Viewport previousViewport;
    private RenderTarget2D? target;
    private RenderTarget2D? workingTarget;
    private bool active;
    private int width;
    private int height;
    private const int MaximumCachedTextures = 512;

    public GpuTriangleRenderer(GraphicsDevice device)
    {
        this.device = device;
        using Stream stream = typeof(GpuTriangleRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.Shaders.Triangles.mgfxo") ?? throw new InvalidOperationException("The GPU triangle effect resource is missing.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        effect = new Effect(device, bytes.ToArray());
    }

    public RenderTarget2D Target => target ?? throw new InvalidOperationException("No scene has been rendered.");
    internal long PresentationVersion { get; private set; }
    internal int FrameDrawCalls { get; private set; }
    internal int FrameTextureUploads { get; private set; }

    public void Begin(int width, int height, Vector4N clearColor, float clearDepth = 1)
    {
        if (active)
        {
            throw new InvalidOperationException("A triangle scene is already active.");
        }
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        device.Adapter.QueryRenderTargetFormat(device.GraphicsProfile, SurfaceFormat.Color, DepthFormat.Depth24, device.PresentationParameters.MultiSampleCount, out _, out _, out int multiSampleCount);
        if (workingTarget is null || workingTarget.Width != width || workingTarget.Height != height || workingTarget.MultiSampleCount != multiSampleCount)
        {
            workingTarget?.Dispose();
            workingTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, multiSampleCount, RenderTargetUsage.PreserveContents);
        }
        this.width = width;
        this.height = height;
        usedTextures.Clear();
        dynamicUploads.Clear();
        vertexCount = 0;
        FrameDrawCalls = 0;
        FrameTextureUploads = 0;
        FrameTerrainUploads = 0;
        terrainFrame++;
        terrainView = null;
        previousTargets = device.GetRenderTargets();
        previousViewport = device.Viewport;
        active = true;
        try
        {
            device.SetRenderTarget(workingTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W), clearDepth, 0);
            device.RasterizerState = RasterizerState.CullNone;
            effect.Parameters["ViewportSize"].SetValue(new Vector2(width, height));
        }
        catch
        {
            Abort();
            throw;
        }
    }

    public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
    {
        if (!active)
        {
            throw new InvalidOperationException("Begin must precede Draw.");
        }
        if (vertexCount > 0 && (pendingPrimitiveType != PrimitiveType.TriangleList || pendingMaterial != material || vertexCount + 3 > vertices.Length))
        {
            Flush();
        }
        pendingMaterial = material;
        pendingPrimitiveType = PrimitiveType.TriangleList;
        vertices[vertexCount++] = new(a);
        vertices[vertexCount++] = new(b);
        vertices[vertexCount++] = new(c);
    }

    public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
    {
        if (!active)
        {
            throw new InvalidOperationException("Begin must precede DrawLine.");
        }
        if (vertexCount > 0 && (pendingPrimitiveType != PrimitiveType.LineList || pendingMaterial != material || vertexCount + 2 > vertices.Length))
        {
            Flush();
        }
        pendingMaterial = material;
        pendingPrimitiveType = PrimitiveType.LineList;
        vertices[vertexCount++] = new(a);
        vertices[vertexCount++] = new(b);
    }

    public bool TryCaptureArgb(out uint[] pixels)
    {
        if (!active || workingTarget is null)
        {
            throw new InvalidOperationException("Active framebuffer capture requires a current triangle scene.");
        }
        Flush();
        Color[] colors = new Color[width * height];
        Viewport viewport = device.Viewport;
        try
        {
            // Unbinding resolves multisampled color before readback. PreserveContents keeps the scene available for subsequent draws.
            device.SetRenderTarget(null);
            workingTarget.GetData(colors);
        }
        catch (InvalidOperationException)
        {
            pixels = [];
            return false;
        }
        finally
        {
            device.SetRenderTarget(workingTarget);
            device.Viewport = viewport;
        }
        pixels = new uint[colors.Length];
        for (int index = 0; index < colors.Length; index++)
        {
            Color color = colors[index];
            pixels[index] = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
        }
        return true;
    }

    private void Flush()
    {
        if (vertexCount == 0)
        {
            return;
        }
        effect.CurrentTechnique = effect.Techniques["TriangleTechnique"];
        SamplerState sampler = ApplyMaterial(pendingMaterial);
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.SamplerStates[0] = sampler;
            device.DrawUserPrimitives(pendingPrimitiveType, vertices, 0, vertexCount / (pendingPrimitiveType == PrimitiveType.LineList ? 2 : 3));
            FrameDrawCalls++;
        }
        vertexCount = 0;
    }

    private SamplerState ApplyMaterial(in TriangleMaterial material)
    {
        device.BlendState = Blend(material.Blend, material.ColorWrite, material.PreserveTargetAlpha);
        device.DepthStencilState = Depth(material.DepthComparison, material.DepthWrite);
        SamplerState sampler = material.Sampler switch
        {
            TriangleSampler.CutoutLinearClamp => SamplerState.PointClamp,
            TriangleSampler.PointClamp => SamplerState.PointClamp,
            TriangleSampler.PointWrap => SamplerState.PointWrap,
            TriangleSampler.LinearClamp => SamplerState.LinearClamp,
            TriangleSampler.LinearWrap => SamplerState.LinearWrap,
            _ => throw new NotSupportedException("Unsupported triangle sampler.")
        };
        float multiplier = material.Blend == TriangleBlend.HalfAdd ? -1f : material.Blend == TriangleBlend.QuarterAdd ? 0.25f : 1;
        effect.Parameters["MaterialParameters"].SetValue(new Vector4(material.Texture is null ? 0 : 1, material.TextureSelectsAlpha ? 1 : 0, material.AlphaMultiplier, material.DepthMapping == TriangleDepthMapping.Positive ? 1 : 0));
        effect.Parameters["AlphaParameters"].SetValue(new Vector4(material.MinimumAlpha, material.MaximumAlpha, material.MinimumReciprocalW, multiplier));
        effect.Parameters["FogParameters"].SetValue(new Vector4(material.FogStart, material.FogEnd, material.PerspectiveDepth ? 1 : 0, material.Sampler == TriangleSampler.CutoutLinearClamp ? 1 : 0));
        effect.Parameters["DepthScale"].SetValue(material.DepthScale);
        effect.Parameters["FogColor"].SetValue(new Vector3(material.FogColor.X, material.FogColor.Y, material.FogColor.Z));
        Vector4N clip = material.ClipRectangle ?? new Vector4N(0, 0, width, height);
        effect.Parameters["ClipRectangle"].SetValue(new Vector4(clip.X, clip.Y, clip.Z, clip.W));
        if (material.Texture is { } image)
        {
            bool refresh = material.DynamicTexture && dynamicUploads.Add(image);
            effect.Parameters["SourceTexture"].SetValue(Texture(image, refresh));
            effect.Parameters["TextureSize"].SetValue(new Vector2(image.Width, image.Height));
        }
        return sampler;
    }

    public void End()
    {
        if (!active)
        {
            throw new InvalidOperationException("No triangle scene is active.");
        }
        try
        {
            Flush();
        }
        finally
        {
            RestoreDevice();
        }
        (target, workingTarget) = (workingTarget, target);
        PresentationVersion++;
        TrimTerrainBuffers();
        foreach (PixelImage image in textures.Keys.Where(image => !usedTextures.Contains(image)).ToArray())
        {
            textures[image].Dispose();
            textures.Remove(image);
        }
    }

    /// <summary>Invalidate mutable source pixels explicitly before their next use.</summary>
    public void InvalidateTexture(PixelImage image)
    {
        PresentationVersion++;
        if (textures.Remove(image, out Texture2D? texture))
        {
            texture.Dispose();
        }
    }

    private Texture2D Texture(PixelImage image, bool refresh)
    {
        usedTextures.Add(image);
        if (textures.TryGetValue(image, out Texture2D? texture))
        {
            if (refresh)
            {
                texture.SetData(image.Pixels);
                FrameTextureUploads++;
            }
            return texture;
        }
        if (textures.Count >= MaximumCachedTextures)
        {
            PixelImage oldest = textures.Keys.First();
            textures[oldest].Dispose();
            textures.Remove(oldest);
        }
        texture = new Texture2D(device, image.Width, image.Height, false, SurfaceFormat.Color);
        texture.SetData(image.Pixels);
        FrameTextureUploads++;
        textures.Add(image, texture);
        return texture;
    }

    private BlendState Blend(TriangleBlend blend, bool colorWrite, bool preserveTargetAlpha)
    {
        if (!Enum.IsDefined(blend))
        {
            throw new NotSupportedException("Unsupported triangle blend equation.");
        }
        if (blends.TryGetValue((blend, colorWrite, preserveTargetAlpha), out BlendState? state))
        {
            return state;
        }
        state = new BlendState
        {
            ColorSourceBlend = blend is TriangleBlend.Alpha or TriangleBlend.AlphaAdd or TriangleBlend.HalfAdd or TriangleBlend.SourceAlphaBoth ? Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha : Microsoft.Xna.Framework.Graphics.Blend.One,
            ColorDestinationBlend = blend == TriangleBlend.Replace ? Microsoft.Xna.Framework.Graphics.Blend.Zero : blend == TriangleBlend.SourceAlphaBoth ? Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha : blend is TriangleBlend.Alpha or TriangleBlend.HalfAdd ? Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha : Microsoft.Xna.Framework.Graphics.Blend.One,
            ColorBlendFunction = blend == TriangleBlend.Subtract ? BlendFunction.ReverseSubtract : BlendFunction.Add,
            AlphaSourceBlend = preserveTargetAlpha ? Microsoft.Xna.Framework.Graphics.Blend.Zero : Microsoft.Xna.Framework.Graphics.Blend.One,
            AlphaDestinationBlend = preserveTargetAlpha ? Microsoft.Xna.Framework.Graphics.Blend.One : Microsoft.Xna.Framework.Graphics.Blend.Zero,
            ColorWriteChannels = colorWrite ? ColorWriteChannels.All : ColorWriteChannels.None
        };
        blends.Add((blend, colorWrite, preserveTargetAlpha), state);
        return state;
    }

    private DepthStencilState Depth(TriangleDepthComparison comparison, bool write)
    {
        if (depths.TryGetValue((comparison, write), out DepthStencilState? state))
        {
            return state;
        }
        state = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = write,
            DepthBufferFunction = comparison switch
            {
                TriangleDepthComparison.Always => CompareFunction.Always,
                TriangleDepthComparison.Less => CompareFunction.Less,
                TriangleDepthComparison.LessEqual => CompareFunction.LessEqual,
                TriangleDepthComparison.Greater => CompareFunction.Greater,
                TriangleDepthComparison.GreaterEqual => CompareFunction.GreaterEqual,
                _ => throw new NotSupportedException("Unsupported triangle depth comparison.")
            }
        };
        depths.Add((comparison, write), state);
        return state;
    }

    public void Abort()
    {
        if (!active)
        {
            return;
        }
        vertexCount = 0;
        RestoreDevice();
    }

    private void RestoreDevice()
    {
        try
        {
            device.SetRenderTargets(previousTargets);
            device.Viewport = previousViewport;
        }
        finally
        {
            active = false;
        }
    }

    public void Dispose()
    {
        Abort();
        target?.Dispose();
        workingTarget?.Dispose();
        effect.Dispose();
        DisposeTerrainBuffers();
        foreach (Texture2D texture in textures.Values)
        {
            texture.Dispose();
        }
        foreach (BlendState blend in blends.Values)
        {
            blend.Dispose();
        }
        foreach (DepthStencilState depth in depths.Values)
        {
            depth.Dispose();
        }
        textures.Clear();
        active = false;
    }

    private readonly struct GpuVertex : IVertexType
    {
        private static readonly VertexDeclaration Declaration = new(new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0), new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.Color, 0), new VertexElement(28, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0), new VertexElement(40, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2), new VertexElement(56, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3));
        private readonly Vector3 position;
        private readonly Vector4 color;
        private readonly Vector3 uv;
        private readonly Vector4 textureScreenRectangle;
        private readonly Vector4 textureTexelRectangle;
        public VertexDeclaration VertexDeclaration => Declaration;
        public GpuVertex(TriangleVertex vertex)
        {
            position = new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Depth);
            color = new Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W);
            textureScreenRectangle = new Vector4(vertex.TextureScreenRectangle.X, vertex.TextureScreenRectangle.Y, vertex.TextureScreenRectangle.Z, vertex.TextureScreenRectangle.W);
            textureTexelRectangle = new Vector4(vertex.TextureTexelRectangle.X, vertex.TextureTexelRectangle.Y, vertex.TextureTexelRectangle.Z, vertex.TextureTexelRectangle.W);
            uv = new Vector3(vertex.Uv.X * vertex.ReciprocalW, vertex.Uv.Y * vertex.ReciprocalW, vertex.ReciprocalW);
        }
    }
}
