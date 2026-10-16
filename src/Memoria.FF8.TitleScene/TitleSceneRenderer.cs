using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.TitleScene;

/// <summary>Draws the animated, texture-free title backdrop. Use on the graphics thread before beginning the host SpriteBatch.</summary>
public sealed class TitleSceneRenderer : IDisposable
{
    private const int MaximumSurfaceWidth = 1440;
    private const int MaximumSurfaceHeight = 1080;
    private readonly GraphicsDevice graphicsDevice;
    private readonly SpriteBatch spriteBatch;
    private readonly Texture2D whitePixel;
    private readonly Effect effect;
    private RenderTarget2D? surface;
    private bool disposed;

    public TitleSceneRenderer(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (graphicsDevice.GraphicsProfile != GraphicsProfile.HiDef)
        {
            throw new NotSupportedException("The title scene requires MonoGame GraphicsProfile.HiDef for its DirectX 11 shaders.");
        }

        this.graphicsDevice = graphicsDevice;
        using var stream = typeof(TitleSceneRenderer).Assembly.GetManifestResourceStream("Memoria.FF8.TitleScene.TitleScene.mgfxo") ?? throw new InvalidOperationException("The embedded title scene effect is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        effect = new Effect(graphicsDevice, buffer.ToArray());
        spriteBatch = new SpriteBatch(graphicsDevice);
        whitePixel = new Texture2D(graphicsDevice, 1, 1);
        whitePixel.SetData(new[] { Color.White });
    }

    /// <summary>Draws into the current render target. Bounds use the current viewport's local coordinates; time is supplied by the host.</summary>
    public void Draw(Rectangle bounds, double seconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!double.IsFinite(seconds))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        EnsureSurface(bounds.Width, bounds.Height);
        var previousTargets = graphicsDevice.GetRenderTargets();
        var previousViewport = graphicsDevice.Viewport;
        var previousScissor = graphicsDevice.ScissorRectangle;
        var previousBlend = graphicsDevice.BlendState;
        var previousDepth = graphicsDevice.DepthStencilState;
        var previousRasterizer = graphicsDevice.RasterizerState;
        var previousSampler = graphicsDevice.SamplerStates[0];
        var previousTexture = graphicsDevice.Textures[0];
        var targetRestored = false;

        try
        {
            effect.Parameters["Time"].SetValue((float)seconds);
            effect.Parameters["Aspect"].SetValue((float)bounds.Width / bounds.Height);
            effect.Parameters["InverseHeight"].SetValue(1.0f / bounds.Height);
            graphicsDevice.SetRenderTarget(surface);
            effect.CurrentTechnique = effect.Techniques["Surface"];
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            spriteBatch.Draw(whitePixel, new Rectangle(0, 0, surface!.Width, surface.Height), Color.White);
            spriteBatch.End();

            graphicsDevice.SetRenderTargets(previousTargets);
            targetRestored = true;
            graphicsDevice.Viewport = previousViewport;
            effect.CurrentTechnique = effect.Techniques["Composite"];
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, effect);
            spriteBatch.Draw(surface, bounds, Color.White);
            spriteBatch.End();
        }
        finally
        {
            if (!targetRestored)
            {
                graphicsDevice.SetRenderTargets(previousTargets);
            }

            graphicsDevice.Viewport = previousViewport;
            graphicsDevice.ScissorRectangle = previousScissor;
            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.DepthStencilState = previousDepth;
            graphicsDevice.RasterizerState = previousRasterizer;
            graphicsDevice.SamplerStates[0] = previousSampler;
            graphicsDevice.Textures[0] = previousTexture;
        }
    }

    private void EnsureSurface(int width, int height)
    {
        var scale = Math.Min(1.0, Math.Min((double)MaximumSurfaceWidth / width, (double)MaximumSurfaceHeight / height));
        var targetWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
        var targetHeight = Math.Max(1, (int)Math.Ceiling(height * scale));
        if (surface?.Width == targetWidth && surface.Height == targetHeight)
        {
            return;
        }

        surface?.Dispose();
        surface = new RenderTarget2D(graphicsDevice, targetWidth, targetHeight, false, SurfaceFormat.HalfVector4, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        surface?.Dispose();
        whitePixel.Dispose();
        spriteBatch.Dispose();
        effect.Dispose();
    }
}
