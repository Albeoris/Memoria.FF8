using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector4N = System.Numerics.Vector4;

namespace Memoria.FF8;

internal readonly record struct GpuTriangleBatchMetrics(long Draws, long Vertices, long VertexUploads, long TextureChanges, long MaterialChanges, long PrimitiveKindChanges, long Capacity, long Capture, long End, long Other)
{
    internal GpuTriangleBatchMetrics Since(GpuTriangleBatchMetrics start)
    {
        return new(Draws - start.Draws, Vertices - start.Vertices, VertexUploads - start.VertexUploads, TextureChanges - start.TextureChanges, MaterialChanges - start.MaterialChanges, PrimitiveKindChanges - start.PrimitiveKindChanges, Capacity - start.Capacity, Capture - start.Capture, End - start.End, Other - start.Other);
    }
}

/// <summary>Ordered hardware triangle rasterizer; scene decoding remains in the shared rendering library.</summary>
public sealed partial class GpuTriangleRenderer : ITriangleRenderer, ILineRenderer, IFramebufferCaptureRenderer, IWorldTerrainRenderer, IFieldLightingRenderer, IDisposable
{
    internal enum FlushReason { Other, TextureChange, MaterialChange, PrimitiveKindChange, Capacity, Capture, End }

    internal readonly record struct GpuTriangleBatch(int First, int Count, PrimitiveType Kind, TriangleMaterial Material, FlushReason Reason);

    private readonly GraphicsDevice device;
    private readonly Effect effect;
    private readonly ConditionalWeakTable<PixelImage, LinkedListNode<CachedTexture>> textures = new();
    private readonly LinkedList<CachedTexture> recentTextures = new();
    private readonly int maximumCachedTextures;
    private readonly long maximumCachedTextureBytes;
    private long cachedTextureBytes;
    private Texture2D? transientTexture;
    private Color[] captureColors = [];
    private RenderTarget2D? centreCaptureTarget;
    private Effect? centreCaptureEffect;
    private SpriteBatch? centreCaptureBatch;
    private readonly record struct CachedTexture(WeakReference<PixelImage> Image, Texture2D Texture, long Bytes);
    private readonly Dictionary<(TriangleBlend, bool, bool), BlendState> blends = [];
    private readonly Dictionary<(TriangleDepthComparison, bool), DepthStencilState> depths = [];
    private const int MaximumStreamVertices = 32768;
    private const int MaximumStreamBatches = 2048;
    private readonly GpuVertex[] vertices = new GpuVertex[MaximumStreamVertices];
    private readonly GpuTriangleBatch[] batches = new GpuTriangleBatch[MaximumStreamBatches];
    private DynamicVertexBuffer? streamBuffer;
    private readonly HashSet<PixelImage> dynamicUploads = new(ReferenceEqualityComparer.Instance);
    private TriangleMaterial pendingMaterial;
    private PrimitiveType pendingPrimitiveType;
    private int vertexCount;
    private int firstPendingVertex;
    private int batchCount;
    private RenderTargetBinding[] previousTargets = [];
    private Viewport previousViewport;
    private RenderTarget2D? target;
    private RenderTarget2D? workingTarget;
    private bool active;
    private int width;
    private int height;
    private const int MaximumCachedTextures = 512;
    private const long MaximumCachedTextureBytes = 256L * 1024 * 1024;

    public GpuTriangleRenderer(GraphicsDevice device) : this(device, MaximumCachedTextures, MaximumCachedTextureBytes)
    {
    }

    internal GpuTriangleRenderer(GraphicsDevice device, int maximumCachedTextures, long maximumCachedTextureBytes)
    {
        if (maximumCachedTextures < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCachedTextures));
        }
        if (maximumCachedTextureBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCachedTextureBytes));
        }
        this.device = device;
        this.maximumCachedTextures = maximumCachedTextures;
        this.maximumCachedTextureBytes = maximumCachedTextureBytes;
        using Stream stream = typeof(GpuTriangleRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.Shaders.Triangles.mgfxo") ?? throw new InvalidOperationException("The GPU triangle effect resource is missing.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        effect = new Effect(device, bytes.ToArray());
    }

    public RenderTarget2D Target => target ?? throw new InvalidOperationException("No scene has been rendered.");
    internal Guid TelemetryOwnerId { get; } = Guid.NewGuid();
    internal long PresentationVersion { get; private set; }
    internal int FrameDrawCalls { get; private set; }
    internal int FrameTextureUploads { get; private set; }
    internal bool ObserveTriangleBatches { get; set; }
    internal GpuTriangleBatchMetrics TriangleBatchMetrics { get; private set; }

    internal void InvalidatePresentation()
    {
        PresentationVersion++;
    }

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
        if (this.width != width || this.height != height)
        {
            captureColors = [];
        }
        this.width = width;
        this.height = height;
        dynamicUploads.Clear();
        ClearStream();
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
        PrepareBatch(PrimitiveType.TriangleList, material, 3);
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
        PrepareBatch(PrimitiveType.LineList, material, 2);
        vertices[vertexCount++] = new(a);
        vertices[vertexCount++] = new(b);
    }

    public bool TryCaptureArgb(out uint[] pixels)
    {
        if (!active || workingTarget is null)
        {
            throw new InvalidOperationException("Active framebuffer capture requires a current triangle scene.");
        }
        Flush(FlushReason.Capture);
        int pixelCount = checked(width * height);
        if (captureColors.Length != pixelCount)
        {
            captureColors = new Color[pixelCount];
        }
        Color[] colors = captureColors;
        Viewport viewport = device.Viewport;
        try
        {
            // Unbinding resolves multisampled color before readback. PreserveContents keeps the scene available for subsequent draws.
            device.SetRenderTarget(null);
            workingTarget.GetData(colors);
            NativeBattleTelemetry.Record(2003, NativeBattleTrafficKind.GpuReadback, checked((long)colors.Length * 4));
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
        pixels = ConvertCaptureArgb(colors);
        return true;
    }

    public bool TryCaptureArgb(int sourceWidth, int sourceHeight, PixelBounds sourceBounds, int destinationWidth, int destinationHeight, out uint[] pixels)
    {
        if (!active || workingTarget is null)
        {
            throw new InvalidOperationException("Active framebuffer capture requires a current triangle scene.");
        }
        IFramebufferCaptureRenderer.ValidateCaptureBounds(sourceWidth, sourceHeight, sourceBounds, destinationWidth, destinationHeight);
        if (sourceWidth != width || sourceHeight != height)
        {
            throw new InvalidDataException("Framebuffer centre capture dimensions must identify the active raster target.");
        }
        Flush(FlushReason.Capture);
        UnbindTerrain();
        PrepareCentreCapture(destinationWidth, destinationHeight);
        int pixelCount = checked(destinationWidth * destinationHeight);
        if (captureColors.Length != pixelCount)
        {
            captureColors = new Color[pixelCount];
        }
        Color[] colors = captureColors;
        RenderTargetBinding[] savedTargets = device.GetRenderTargets();
        Viewport savedViewport = device.Viewport;
        Rectangle savedScissor = device.ScissorRectangle;
        BlendState savedBlend = device.BlendState;
        DepthStencilState savedDepth = device.DepthStencilState;
        RasterizerState savedRasterizer = device.RasterizerState;
        SamplerState savedSampler0 = device.SamplerStates[0];
        SamplerState savedSampler1 = device.SamplerStates[1];
        Texture? savedTexture0 = device.Textures[0];
        Texture? savedTexture1 = device.Textures[1];
        IndexBuffer? savedIndices = device.Indices;
        try
        {
            // Unbinding resolves the same MSAA surface used by the full-size capture.
            device.SetRenderTarget(null);
            device.SetRenderTarget(centreCaptureTarget);
            device.Viewport = new(0, 0, destinationWidth, destinationHeight);
            device.ScissorRectangle = new(0, 0, destinationWidth, destinationHeight);
            centreCaptureEffect!.CurrentTechnique = centreCaptureEffect.Techniques["CentreCapture"];
            centreCaptureEffect.Parameters["CurrentTexture"].SetValue(workingTarget);
            centreCaptureEffect.Parameters["InverseSize"].SetValue(new Vector2(1f / sourceWidth, 1f / sourceHeight));
            centreCaptureEffect.Parameters["CentreBounds"].SetValue(new Vector4(sourceBounds.X, sourceBounds.Y, sourceBounds.Width, sourceBounds.Height));
            centreCaptureEffect.Parameters["CentreDestinationSize"].SetValue(new Vector2(destinationWidth, destinationHeight));
            centreCaptureBatch!.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, centreCaptureEffect);
            try
            {
                centreCaptureBatch.Draw(workingTarget, new Rectangle(0, 0, destinationWidth, destinationHeight), Color.White);
            }
            finally
            {
                centreCaptureBatch.End();
            }
            device.Textures[0] = null;
            centreCaptureEffect.Parameters["CurrentTexture"].SetValue((Texture2D?)null);
            device.SetRenderTarget(null);
            centreCaptureTarget!.GetData(colors);
            NativeBattleTelemetry.Record(2003, NativeBattleTrafficKind.GpuReadback, checked((long)colors.Length * 4));
        }
        catch (InvalidOperationException)
        {
            pixels = [];
            return false;
        }
        finally
        {
            device.Textures[0] = null;
            centreCaptureEffect!.Parameters["CurrentTexture"].SetValue((Texture2D?)null);
            device.SetRenderTargets(savedTargets);
            device.Viewport = savedViewport;
            device.ScissorRectangle = savedScissor;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
            }
            device.BlendState = savedBlend;
            device.DepthStencilState = savedDepth;
            device.RasterizerState = savedRasterizer;
            device.SamplerStates[0] = savedSampler0;
            device.SamplerStates[1] = savedSampler1;
            device.Textures[0] = savedTexture0;
            device.Textures[1] = savedTexture1;
            device.Indices = savedIndices;
            // SpriteBatch replaces the vertex stream. The next retained draw must bind it again.
            device.SetVertexBuffer(null);
        }
        pixels = ConvertCaptureArgb(colors);
        return true;
    }

    private void PrepareCentreCapture(int width, int height)
    {
        if (centreCaptureEffect is null)
        {
            using Stream stream = typeof(GpuTriangleRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.Shaders.FrameDifference.mgfxo") ?? throw new InvalidOperationException("The framebuffer centre capture effect resource is missing.");
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            centreCaptureEffect = new Effect(device, bytes.ToArray());
            centreCaptureEffect.CurrentTechnique = centreCaptureEffect.Techniques["CentreCapture"];
        }
        centreCaptureBatch ??= new SpriteBatch(device);
        if (centreCaptureTarget is null || centreCaptureTarget.Width != width || centreCaptureTarget.Height != height)
        {
            centreCaptureTarget?.Dispose();
            centreCaptureTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
    }

    private static uint[] ConvertCaptureArgb(Color[] colors)
    {
        uint[] pixels = new uint[colors.Length];
        NativeBattleTelemetry.Record(2004, NativeBattleTrafficKind.CpuCopyPayload, checked((long)colors.Length * 4));
        for (int index = 0; index < colors.Length; index++)
        {
            Color color = colors[index];
            pixels[index] = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
        }
        return pixels;
    }

    private void PrepareBatch(PrimitiveType kind, in TriangleMaterial material, int addedVertices)
    {
        bool full = vertexCount + addedVertices > vertices.Length;
        if (vertexCount > firstPendingVertex && (pendingPrimitiveType != kind || pendingMaterial != material || full))
        {
            FlushReason reason = ObserveTriangleBatches ? BatchBreakReason(kind, material) : FlushReason.Other;
            bool dynamicBoundary = pendingMaterial.DynamicTexture || material.DynamicTexture;
            CloseBatch(reason);
            if (full || dynamicBoundary || batchCount == batches.Length)
            {
                SubmitStream();
            }
        }
        pendingMaterial = material;
        pendingPrimitiveType = kind;
    }

    private FlushReason BatchBreakReason(PrimitiveType kind, in TriangleMaterial material)
    {
        if (pendingPrimitiveType != kind)
        {
            return FlushReason.PrimitiveKindChange;
        }
        if (pendingMaterial != material)
        {
            return ReferenceEquals(pendingMaterial.Texture, material.Texture) ? FlushReason.MaterialChange : FlushReason.TextureChange;
        }
        return FlushReason.Capacity;
    }

    private void Flush(FlushReason reason = FlushReason.Other)
    {
        CloseBatch(reason);
        SubmitStream();
    }

    private void CloseBatch(FlushReason reason)
    {
        if (vertexCount > firstPendingVertex)
        {
            batches[batchCount++] = new(firstPendingVertex, vertexCount - firstPendingVertex, pendingPrimitiveType, pendingMaterial, reason);
            firstPendingVertex = vertexCount;
        }
    }

    private void SubmitStream()
    {
        if (vertexCount == 0)
        {
            return;
        }
        try
        {
            UnbindTerrain();
            streamBuffer ??= new(device, vertices[0].VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
            streamBuffer.SetData(vertices, 0, vertexCount, SetDataOptions.Discard);
            NativeBattleTelemetry.Record(2001, NativeBattleTrafficKind.GpuUpload, checked((long)vertexCount * vertices[0].VertexDeclaration.VertexStride));
            if (ObserveTriangleBatches)
            {
                TriangleBatchMetrics = TriangleBatchMetrics with { VertexUploads = TriangleBatchMetrics.VertexUploads + 1 };
            }
            device.SetVertexBuffer(streamBuffer);
            for (int index = 0; index < batchCount; index++)
            {
                SubmitBatch(batches[index]);
            }
        }
        finally
        {
            ClearStream();
            device.SetVertexBuffer(null);
        }
    }

    private void SubmitBatch(in GpuTriangleBatch batch)
    {
        try
        {
            effect.CurrentTechnique = effect.Techniques[fieldShadowPass ? "FieldShadowTechnique" : batch.Material.FieldLighting != FieldLightingPass.None ? "FieldLightingTechnique" : "TriangleTechnique"];
            SamplerState sampler = ApplyMaterial(batch.Material);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.SamplerStates[0] = sampler;
                if (!fieldShadowPass && fieldShadowTarget is not null)
                {
                    device.SamplerStates[1] = SamplerState.PointClamp;
                }
                device.DrawPrimitives(batch.Kind, batch.First, batch.Count / (batch.Kind == PrimitiveType.LineList ? 2 : 3));
                FrameDrawCalls++;
                if (ObserveTriangleBatches)
                {
                    GpuTriangleBatchMetrics metrics = TriangleBatchMetrics;
                    TriangleBatchMetrics = metrics with
                    {
                        Draws = metrics.Draws + 1,
                        Vertices = metrics.Vertices + batch.Count,
                        TextureChanges = metrics.TextureChanges + (batch.Reason == FlushReason.TextureChange ? 1 : 0),
                        MaterialChanges = metrics.MaterialChanges + (batch.Reason == FlushReason.MaterialChange ? 1 : 0),
                        PrimitiveKindChanges = metrics.PrimitiveKindChanges + (batch.Reason == FlushReason.PrimitiveKindChange ? 1 : 0),
                        Capacity = metrics.Capacity + (batch.Reason == FlushReason.Capacity ? 1 : 0),
                        Capture = metrics.Capture + (batch.Reason == FlushReason.Capture ? 1 : 0),
                        End = metrics.End + (batch.Reason == FlushReason.End ? 1 : 0),
                        Other = metrics.Other + (batch.Reason == FlushReason.Other ? 1 : 0)
                    };
                }
            }
        }
        finally
        {
            // An image larger than the cache budget remains valid for its submitted batch.
            ReleaseTransientTexture();
        }
    }

    private void ClearStream()
    {
        Array.Clear(batches, 0, batchCount);
        batchCount = 0;
        vertexCount = 0;
        firstPendingVertex = 0;
        pendingMaterial = default;
    }

    private void ReleaseTransientTexture()
    {
        if (transientTexture is not { } oversized)
        {
            return;
        }
        transientTexture = null;
        try
        {
            UnbindTexture(oversized);
        }
        finally
        {
            oversized.Dispose();
        }
    }

    private void UnbindTexture(Texture2D texture)
    {
        if (ReferenceEquals(device.Textures[0], texture))
        {
            device.Textures[0] = null;
        }
        if (ReferenceEquals(effect.Parameters["SourceTexture"].GetValueTexture2D(), texture))
        {
            effect.Parameters["SourceTexture"].SetValue((Texture2D?)null);
        }
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
        effect.Parameters["ActorOpacityParameters"].SetValue(new Vector2(material.ActorOpacity, material.ActorOpacityReplacesAlpha ? 1 : 0));
        effect.Parameters["AlphaParameters"].SetValue(new Vector4(material.MinimumAlpha, material.MaximumAlpha, material.MinimumReciprocalW, multiplier));
        effect.Parameters["FogParameters"].SetValue(new Vector4(material.FogStart, material.FogEnd, material.PerspectiveDepth ? 1 : 0, material.Sampler == TriangleSampler.CutoutLinearClamp ? 1 : 0));
        effect.Parameters["DepthScale"].SetValue(material.DepthScale);
        effect.Parameters["CentroidTextureSampling"].SetValue(material.CentroidTextureSampling);
        effect.Parameters["FieldLightingPass"].SetValue((float)material.FieldLighting);
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
            Flush(FlushReason.End);
        }
        finally
        {
            ClearStream();
            dynamicUploads.Clear();
            RestoreDevice();
        }
        (target, workingTarget) = (workingTarget, target);
        PresentationVersion++;
        TrimTerrainBuffers();
        for (LinkedListNode<CachedTexture>? node = recentTextures.First; node is not null;)
        {
            LinkedListNode<CachedTexture>? next = node.Next;
            if (!node.Value.Image.TryGetTarget(out _))
            {
                RemoveCachedTexture(node);
            }
            node = next;
        }
    }

    /// <summary>Invalidate mutable source pixels explicitly before their next use.</summary>
    public void InvalidateTexture(PixelImage image)
    {
        Flush();
        PresentationVersion++;
        if (textures.TryGetValue(image, out LinkedListNode<CachedTexture>? node))
        {
            RemoveCachedTexture(node);
        }
    }

    private void RemoveCachedTexture(LinkedListNode<CachedTexture> node)
    {
        if (node.Value.Image.TryGetTarget(out PixelImage? image))
        {
            textures.Remove(image);
        }
        recentTextures.Remove(node);
        cachedTextureBytes -= node.Value.Bytes;
        try
        {
            UnbindTexture(node.Value.Texture);
        }
        finally
        {
            node.Value.Texture.Dispose();
        }
    }

    private Texture2D Texture(PixelImage image, bool refresh)
    {
        if (textures.TryGetValue(image, out LinkedListNode<CachedTexture>? node))
        {
            recentTextures.Remove(node);
            recentTextures.AddLast(node);
            if (refresh)
            {
                node.Value.Texture.SetData(image.Pixels);
                NativeBattleTelemetry.Record(2002, NativeBattleTrafficKind.GpuUpload, checked((long)image.Pixels.Length * 4));
                FrameTextureUploads++;
            }
            return node.Value.Texture;
        }
        long bytes = checked((long)image.Pixels.Length * 4);
        bool cacheable = bytes <= maximumCachedTextureBytes;
        while (cacheable && (recentTextures.Count >= maximumCachedTextures || cachedTextureBytes > maximumCachedTextureBytes - bytes))
        {
            LinkedListNode<CachedTexture> oldest = recentTextures.First!;
            RemoveCachedTexture(oldest);
        }
        Texture2D texture = new(device, image.Width, image.Height, false, SurfaceFormat.Color);
        try
        {
            texture.SetData(image.Pixels);
        }
        catch
        {
            texture.Dispose();
            throw;
        }
        NativeBattleTelemetry.Record(2002, NativeBattleTrafficKind.GpuUpload, checked((long)image.Pixels.Length * 4));
        FrameTextureUploads++;
        if (cacheable)
        {
            textures.Add(image, recentTextures.AddLast(new CachedTexture(new(image), texture, bytes)));
            cachedTextureBytes += bytes;
        }
        else
        {
            // Oversized images bypass retention instead of changing accepted rendering inputs.
            transientTexture = texture;
        }
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
        ClearStream();
        dynamicUploads.Clear();
        try
        {
            ReleaseTransientTexture();
        }
        finally
        {
            if (active)
            {
                RestoreDevice();
            }
        }
    }

    private void RestoreDevice()
    {
        try
        {
            UnbindTerrain();
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
        centreCaptureTarget?.Dispose();
        centreCaptureEffect?.Dispose();
        centreCaptureBatch?.Dispose();
        streamBuffer?.Dispose();
        DisposeTerrainBuffers();
        foreach (CachedTexture cached in recentTextures)
        {
            UnbindTexture(cached.Texture);
            cached.Texture.Dispose();
        }
        effect.Dispose();
        fieldShadowTarget?.Dispose();
        foreach (BlendState blend in blends.Values)
        {
            blend.Dispose();
        }
        foreach (DepthStencilState depth in depths.Values)
        {
            depth.Dispose();
        }
        textures.Clear();
        recentTextures.Clear();
        cachedTextureBytes = 0;
        captureColors = [];
        active = false;
    }

    private readonly struct GpuVertex : IVertexType
    {
        private static readonly VertexDeclaration Declaration = new(new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0), new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.Color, 0), new VertexElement(28, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0), new VertexElement(40, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2), new VertexElement(56, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3), new VertexElement(72, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 5), new VertexElement(84, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 6));
        private readonly Vector3 position;
        private readonly Vector4 color;
        private readonly Vector3 uv;
        private readonly Vector4 textureScreenRectangle;
        private readonly Vector4 textureTexelRectangle;
        private readonly Vector3 worldPosition;
        private readonly Vector3 worldNormal;
        public VertexDeclaration VertexDeclaration => Declaration;
        public GpuVertex(TriangleVertex vertex)
        {
            position = new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Depth);
            if (vertex.TextureScreenRectangle.Z > 0)
            {
                // Point-sampled rectangles cover whole destination pixels. Keep every
                // MSAA sample on the pixel-center-selected side of each tile edge.
                // The original rectangle below still owns source texel selection.
                position.X = MathF.Ceiling(position.X - 0.5f);
                position.Y = MathF.Ceiling(position.Y - 0.5f);
            }
            color = new Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W);
            textureScreenRectangle = new Vector4(vertex.TextureScreenRectangle.X, vertex.TextureScreenRectangle.Y, vertex.TextureScreenRectangle.Z, vertex.TextureScreenRectangle.W);
            textureTexelRectangle = new Vector4(vertex.TextureTexelRectangle.X, vertex.TextureTexelRectangle.Y, vertex.TextureTexelRectangle.Z, vertex.TextureTexelRectangle.W);
            uv = new Vector3(vertex.Uv.X * vertex.ReciprocalW, vertex.Uv.Y * vertex.ReciprocalW, vertex.ReciprocalW);
            worldPosition = new Vector3(vertex.WorldPosition.X, vertex.WorldPosition.Y, vertex.WorldPosition.Z) * vertex.ReciprocalW;
            worldNormal = new Vector3(vertex.WorldNormal.X, vertex.WorldNormal.Y, vertex.WorldNormal.Z) * vertex.ReciprocalW;
        }
    }
}
