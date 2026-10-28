using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal readonly record struct VisualFrameComparison(long FrameOrdinal, TimeSpan Timestamp, bool? Changed, bool Dropped = false);

/// <summary>Compares every displayed RGB pixel before drawing the FPS overlay. Only completed GPU query results cross to the CPU.</summary>
internal sealed class VisualFrameRate : IDisposable
{
    private const int MaximumPendingPresentations = 8;
    private readonly GraphicsDevice device;
    private readonly SpriteBatch batch;
    private readonly Effect difference;
    private readonly BlendState queryBlend = new() { ColorWriteChannels = ColorWriteChannels.None };
    private readonly PresentationFrameRate counter = new();
    private readonly PresentationFrameRate presentationCounter = new();
    private readonly Queue<PendingPresentation> pending = new();
    private readonly Stack<OcclusionQuery> availableQueries = new();
    private RenderTarget2D? current;
    private RenderTarget2D? previous;
    private RenderTargetBinding[] destination = [];
    private Viewport destinationViewport;
    private Rectangle destinationScissor;
    private PendingPresentation? awaitingPresentation;
    private bool hasPrevious;
    private bool frameActive;

    public VisualFrameRate(GraphicsDevice device)
    {
        this.device = device;
        batch = new SpriteBatch(device);
        using Stream stream = typeof(VisualFrameRate).Assembly.GetManifestResourceStream("Memoria.FF8.Shaders.FrameDifference.mgfxo") ?? throw new InvalidOperationException("The visual frame comparison effect resource is missing.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        difference = new Effect(device, bytes.ToArray());
        device.DeviceReset += OnDeviceReset;
    }

    public double? FramesPerSecond => counter.FramesPerSecond;
    public double? PresentationsPerSecond => presentationCounter.PresentationsPerSecond;
    public event Action<VisualFrameComparison>? ComparisonResolved;
    public long DroppedComparisons { get; private set; }
    public int PendingComparisons => pending.Count;
    public bool HasAwaitingPresentation => awaitingPresentation is not null;

    /// <summary>Compares an already rendered image. Owns only its previous color image and never reads image pixels onto the CPU.</summary>
    public void ObserveTexture(Texture2D source, long frameId = 0)
    {
        if (frameActive || awaitingPresentation is not null)
        {
            throw new InvalidOperationException("The preceding visual image must be presented before observing another.");
        }
        ResolveCompletedPresentations();
        if (pending.Count >= MaximumPendingPresentations)
        {
            ResetMeasurement();
        }
        if (current is not null || previous is null || previous.Width != source.Width || previous.Height != source.Height || previous.Format != SurfaceFormat.Color)
        {
            ResetResources();
            previous = new RenderTarget2D(device, source.Width, source.Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
        RenderTargetBinding[] savedTargets = device.GetRenderTargets();
        Viewport savedViewport = device.Viewport;
        Rectangle savedScissor = device.ScissorRectangle;
        Rectangle bounds = new(0, 0, source.Width, source.Height);
        OcclusionQuery? query = null;
        try
        {
            // Color writes are disabled during comparison; the existing backbuffer is only a query destination.
            device.SetRenderTarget(null);
            device.Viewport = new(0, 0, source.Width, source.Height);
            if (hasPrevious)
            {
                query = CompareImages(source, previous!, bounds, Vector2.Zero);
            }
            UnbindComparisonTextures();
            device.SetRenderTarget(previous);
            device.Viewport = new(0, 0, source.Width, source.Height);
            batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp, depthStencilState: DepthStencilState.None, rasterizerState: RasterizerState.CullNone);
            batch.Draw(source, bounds, Color.White);
            batch.End();
            device.Textures[0] = null;
            hasPrevious = true;
            awaitingPresentation = new(query) { FrameOrdinal = frameId };
        }
        catch
        {
            query?.Dispose();
            hasPrevious = false;
            throw;
        }
        finally
        {
            device.SetRenderTargets(savedTargets);
            device.Viewport = savedViewport;
            device.ScissorRectangle = savedScissor;
        }
    }

    public void BeginFrame()
    {
        if (frameActive || awaitingPresentation is not null)
        {
            throw new InvalidOperationException("The preceding visual frame must be completed and presented first.");
        }
        ResolveCompletedPresentations();
        if (pending.Count >= MaximumPendingPresentations)
        {
            // Results that remain unavailable cannot establish a frame rate. Start a new baseline without waiting for the GPU.
            ResetMeasurement();
        }
        PresentationParameters presentation = device.PresentationParameters;
        Viewport viewport = device.Viewport;
        if (current is null || current.Width != viewport.Width || current.Height != viewport.Height || current.MultiSampleCount != presentation.MultiSampleCount || current.Format != presentation.BackBufferFormat || current.DepthStencilFormat != presentation.DepthStencilFormat)
        {
            ResetResources();
            current = CreateSurface(viewport, presentation);
            previous = CreateSurface(viewport, presentation);
        }
        destination = device.GetRenderTargets();
        destinationViewport = viewport;
        destinationScissor = device.ScissorRectangle;
        device.SetRenderTarget(current);
        frameActive = true;
    }

    public void CompleteFrame()
    {
        if (!frameActive)
        {
            throw new InvalidOperationException("BeginFrame must precede CompleteFrame.");
        }
        device.SetRenderTargets(destination);
        device.Viewport = destinationViewport;
        device.ScissorRectangle = destinationScissor;
        Rectangle bounds = new(0, 0, destinationViewport.Width, destinationViewport.Height);
        OcclusionQuery? query = null;
        if (hasPrevious)
        {
            query = CompareImages(current!, previous!, bounds, new(destinationViewport.X, destinationViewport.Y));
        }
        batch.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
        batch.Draw(current!, bounds, Color.White);
        batch.End();
        // The next current surface was sampled as the previous image. Unbind it before using it as a render target.
        UnbindComparisonTextures();
        (current, previous) = (previous, current);
        hasPrevious = true;
        frameActive = false;
        awaitingPresentation = new PendingPresentation(query);
    }

    public void Presented(TimeSpan timestamp)
    {
        if (awaitingPresentation is null)
        {
            throw new InvalidOperationException("CompleteFrame must precede Presented.");
        }
        presentationCounter.Presented(timestamp, true);
        awaitingPresentation.Timestamp = timestamp;
        pending.Enqueue(awaitingPresentation);
        awaitingPresentation = null;
        ResolveCompletedPresentations();
    }

    internal void ResolveCompletedPresentations()
    {
        while (pending.TryPeek(out PendingPresentation? frame))
        {
            if (frame.Query is { IsComplete: false })
            {
                return;
            }
            pending.Dequeue();
            counter.Presented(frame.Timestamp, frame.Query is { PixelCount: > 0 });
            ComparisonResolved?.Invoke(new(frame.FrameOrdinal, frame.Timestamp, frame.Query is null ? null : frame.Query.PixelCount > 0));
            if (frame.Query is not null)
            {
                availableQueries.Push(frame.Query);
            }
        }
    }

    private OcclusionQuery CompareImages(Texture2D image, Texture2D previousImage, Rectangle bounds, Vector2 viewportOrigin)
    {
        OcclusionQuery query = availableQueries.TryPop(out OcclusionQuery? reusable) ? reusable : new OcclusionQuery(device);
        difference.Parameters["CurrentTexture"].SetValue(image);
        difference.Parameters["PreviousTexture"].SetValue(previousImage);
        difference.Parameters["InverseSize"].SetValue(new Vector2(1f / image.Width, 1f / image.Height));
        difference.Parameters["ViewportOrigin"].SetValue(viewportOrigin);
        query.Begin();
        batch.Begin(SpriteSortMode.Immediate, queryBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, difference);
        batch.Draw(image, bounds, Color.White);
        batch.End();
        query.End();
        return query;
    }

    private void UnbindComparisonTextures()
    {
        device.Textures[0] = null;
        device.Textures[1] = null;
        difference.Parameters["CurrentTexture"].SetValue((Texture2D?)null);
        difference.Parameters["PreviousTexture"].SetValue((Texture2D?)null);
    }

    private RenderTarget2D CreateSurface(Viewport viewport, PresentationParameters presentation)
    {
        return new RenderTarget2D(device, viewport.Width, viewport.Height, false, presentation.BackBufferFormat, presentation.DepthStencilFormat, presentation.MultiSampleCount, RenderTargetUsage.PreserveContents);
    }

    private void OnDeviceReset(object? sender, EventArgs args)
    {
        ResetResources();
    }

    private void ResetMeasurement()
    {
        foreach (PendingPresentation frame in pending)
        {
            DroppedComparisons++;
            ComparisonResolved?.Invoke(new(frame.FrameOrdinal, frame.Timestamp, null, true));
            frame.Query?.Dispose();
        }
        pending.Clear();
        awaitingPresentation?.Query?.Dispose();
        awaitingPresentation = null;
        hasPrevious = false;
        counter.Reset();
    }

    private void ResetResources()
    {
        ResetMeasurement();
        presentationCounter.Reset();
        while (availableQueries.TryPop(out OcclusionQuery? query))
        {
            query.Dispose();
        }
        current?.Dispose();
        previous?.Dispose();
        current = null;
        previous = null;
        frameActive = false;
    }

    public void Dispose()
    {
        device.DeviceReset -= OnDeviceReset;
        ResetResources();
        difference.Dispose();
        batch.Dispose();
        queryBlend.Dispose();
    }

    private sealed class PendingPresentation(OcclusionQuery? query)
    {
        public OcclusionQuery? Query { get; } = query;
        public TimeSpan Timestamp { get; set; }
        public long FrameOrdinal { get; init; }
    }
}
