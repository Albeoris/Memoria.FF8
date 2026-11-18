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
    private readonly TitleSceneStyle style;
    private readonly Vector2[] meteorTailOffsets = new Vector2[MeteorMotion.TailSampleCount];
    private RenderTarget2D? surface;
    private bool disposed;

    public TitleSceneRenderer(GraphicsDevice graphicsDevice, TitleSceneStyle style = TitleSceneStyle.FinalFantasyVIII)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        if (graphicsDevice.GraphicsProfile != GraphicsProfile.HiDef)
        {
            throw new NotSupportedException("The title scene requires MonoGame GraphicsProfile.HiDef for its DirectX 11 shaders.");
        }

        this.graphicsDevice = graphicsDevice;
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style));
        }

        this.style = style;
        string resource = style == TitleSceneStyle.FinalFantasyVII ? "MeteorScene" : "TitleScene";
        using var stream = typeof(TitleSceneRenderer).Assembly.GetManifestResourceStream($"Memoria.FF8.TitleScene.{resource}.mgfxo") ?? throw new InvalidOperationException("The embedded title scene effect is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        effect = new Effect(graphicsDevice, buffer.ToArray());
        spriteBatch = new SpriteBatch(graphicsDevice);
        whitePixel = new Texture2D(graphicsDevice, 1, 1);
        whitePixel.SetData(new[] { Color.White });
    }

    /// <summary>Selects an authoring view for the FFVII meteor. Ignored by the FFVIII scene.</summary>
    public MeteorLightingPass MeteorLighting { get; set; } = MeteorLightingPass.Full;

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

        Vector4 meteorLightWeights = Vector4.One;
        if (style == TitleSceneStyle.FinalFantasyVII)
        {
            meteorLightWeights = MeteorLighting switch
            {
                MeteorLightingPass.Full => Vector4.One,
                MeteorLightingPass.Key => new Vector4(1f, 0f, 0f, 0f),
                MeteorLightingPass.Rim => new Vector4(0f, 1f, 0f, 0f),
                MeteorLightingPass.Fill => new Vector4(0f, 0f, 1f, 0f),
                MeteorLightingPass.KeyAndRim => new Vector4(1f, 1f, 0f, 0f),
                _ => throw new ArgumentOutOfRangeException(nameof(MeteorLighting), MeteorLighting, "Unknown meteor lighting pass.")
            };
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
            effect.Parameters["Time"].SetValue((float)(style == TitleSceneStyle.FinalFantasyVII ? seconds : TitleSceneAnimation.MotionSeconds(seconds)));
            if (style == TitleSceneStyle.FinalFantasyVIII)
            {
                effect.Parameters["LightIntensity"].SetValue(TitleSceneAnimation.LightIntensity(seconds));
                effect.Parameters["VortexRotation"].SetValue(TitleSceneAnimation.VortexRotation(seconds));
                effect.Parameters["VortexTravel"].SetValue(TitleSceneAnimation.VortexTravel(seconds));
            }
            if (style == TitleSceneStyle.FinalFantasyVII)
            {
                effect.Parameters["MeteorLightWeights"].SetValue(meteorLightWeights);
                MeteorMotionSample motion = MeteorMotion.Sample(seconds);
                effect.Parameters["MeteorHeadOffset"].SetValue(ToFlightOffset(motion.ScreenOffset));
                effect.Parameters["MeteorFlameIntensity"].SetValue(MeteorMotion.SampleFlameIntensity(seconds));
                effect.Parameters["MeteorObjectAxisX"].SetValue(new Vector3(motion.AxisX.X, motion.AxisX.Y, motion.AxisX.Z));
                effect.Parameters["MeteorObjectAxisY"].SetValue(new Vector3(motion.AxisY.X, motion.AxisY.Y, motion.AxisY.Z));
                effect.Parameters["MeteorObjectAxisZ"].SetValue(new Vector3(motion.AxisZ.X, motion.AxisZ.Y, motion.AxisZ.Z));
                Span<System.Numerics.Vector2> tail = stackalloc System.Numerics.Vector2[MeteorMotion.TailSampleCount];
                MeteorMotion.FillTailScreenOffsets(seconds, tail);
                for (int index = 0; index < meteorTailOffsets.Length; index++)
                {
                    meteorTailOffsets[index] = ToFlightOffset(tail[index]);
                }
                effect.Parameters["MeteorTailOffsets"].SetValue(meteorTailOffsets);
            }
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

    private static Vector2 ToFlightOffset(System.Numerics.Vector2 screenOffset)
    {
        return new(0.94f * screenOffset.X - 0.341f * screenOffset.Y, 0.341f * screenOffset.X + 0.94f * screenOffset.Y);
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
