using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly StartupSequence startup;
    private MoviePresentation? startupMovie;
    private Task<MovieInformation>? startupMovieInformation;
    private Task<NativeStartupArtwork>? startupArtwork;
    private readonly Dictionary<int, Texture2D> startupTextures = [];
    private bool startupMusicStarted;
    private bool startupFaulted;
    private BlendState? startupSubtract;
    private NativeStartupCreditsFrame? startupCreditFrame;

    private bool UpdateStartup(TimeSpan elapsed, InputFrame input)
    {
        if (!startup.Active)
        {
            return false;
        }
        if (!startup.IsConfigured)
        {
            // A watcher can invalidate preparation before the first catalog is committed.
            if (!startup.TryConfigure(resourceHost!.StartupPolicy, resourceHost.Mods.Catalog.Generation))
            {
                if (!resourceHost.Mods.IsReloadPending && !startupFaulted)
                {
                    startupFaulted = true;
                    error = "Initial mod publication failed before startup. " + string.Join(" ", resourceHost.Mods.Diagnostics.Select(diagnostic => diagnostic.Message));
                    Console.Error.WriteLine(error);
                }
                return true;
            }
            startupFaulted = false;
            error = null;
            if (!startup.Active)
            {
                return false;
            }
        }
        if (startup.HandleInput(input.Confirm, input.Cancel))
        {
            ReleaseStartupStage();
            return true;
        }
        if (startupFaulted)
        {
            return true;
        }
        try
        {
            if (startup.Stage is StartupStage.SquareEnixMovie or StartupStage.PublisherMovie)
            {
                string path = movieClock.ResolveMovieFile("movies/disc04_00h.avi");
                startupMovieInformation ??= Task.Run(() => FfmpegMovieDecoder.ProbePresentation(movieClock.ToolsDirectory, path));
                if (!startupMovieInformation.IsCompleted)
                {
                    return true;
                }
                double start = startup.Stage == StartupStage.PublisherMovie ? StartupSequence.PublisherStartSeconds : 0;
                startupMovie ??= new MoviePresentation(GraphicsDevice, movieClock.ToolsDirectory, path, start, startupMovieInformation.GetAwaiter().GetResult(), options.AudioEnabled);
                startupMovie.Update();
                if (startupMovie.Error is string movieError)
                {
                    throw new InvalidDataException(movieError);
                }
                if (startup.Stage == StartupStage.SquareEnixMovie && startupMovie.PositionSeconds >= StartupSequence.PublisherStartSeconds)
                {
                    startup.Next();
                }
                else if (startupMovie.Finished)
                {
                    startup.Next();
                    ReleaseStartupStage();
                }
                return true;
            }

            startupArtwork ??= Task.Run(() => NativeStartupArtwork.Load(options.GameDirectory));
            if (!startupArtwork.IsCompleted)
            {
                return true;
            }
            NativeStartupCreditsFrame frame = startup.Stage == StartupStage.SquareLogo ? NativeStartupCredits.SampleSquare(startup.Elapsed) : NativeStartupCredits.SampleCredits(startup.Elapsed, startup.Policy.SkipCreditsPause);
            if (frame.Finished)
            {
                if (startup.Stage == StartupStage.Credits)
                {
                    fieldAudio?.StopAll();
                    startupMusicStarted = false;
                    startup.RepeatCredits();
                }
                else
                {
                    startup.Next();
                }
                ReleaseStartupStage();
                return true;
            }
            if (!startupTextures.ContainsKey(frame.ResourceIndex))
            {
                PixelImage image = startupArtwork.GetAwaiter().GetResult().Read(frame.ResourceIndex);
                Texture2D texture = new(GraphicsDevice, image.Width, image.Height);
                texture.SetData(image.Pixels.Select(color => new Color(color.R, color.G, color.B, (byte)255)).ToArray());
                startupTextures.Add(frame.ResourceIndex, texture);
            }
            if (startup.Stage == StartupStage.Credits && !startupMusicStarted)
            {
                fieldAudio?.PlayStartupMusic();
                startupMusicStarted = true;
            }
            startupCreditFrame = frame;
            startup.Advance(elapsed);
            fieldAudio?.Update();
        }
        catch (Exception exception)
        {
            startupFaulted = true;
            error = exception.Message;
            Console.Error.WriteLine(exception);
        }
        return true;
    }

    private void DrawStartup(Rectangle bounds)
    {
        if (startup.Stage is StartupStage.SquareEnixMovie or StartupStage.PublisherMovie)
        {
            if (startupMovie?.HasFrame == true)
            {
                startupMovie.Draw(batch, bounds);
            }
            return;
        }
        if (startupCreditFrame is not { } frame || !startupTextures.TryGetValue(frame.ResourceIndex, out Texture2D? texture))
        {
            return;
        }
        PixelBounds fit = ViewportLayout.Fit(640, 480, bounds.Width, bounds.Height);
        Rectangle target = new(bounds.X + fit.X, bounds.Y + fit.Y + fit.Height / 12, fit.Width, fit.Height * 5 / 6);
        batch.Draw(texture, target, Color.White);
        // Native5302C0 adds white (mode1) or subtracts intensity (mode2) per color channel.
        startupSubtract ??= new BlendState { ColorBlendFunction = BlendFunction.ReverseSubtract, ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One };
        batch.End();
        batch.Begin(blendState: frame.OverlayIsWhite ? BlendState.Additive : startupSubtract, samplerState: SamplerState.LinearClamp);
        batch.Draw(pixel, target, new Color(frame.OverlayValue, frame.OverlayValue, frame.OverlayValue, (byte)255));
        batch.End();
        batch.Begin(samplerState: SamplerState.LinearClamp);
    }

    private void ReleaseStartupStage()
    {
        startupMovie?.Dispose();
        startupMovie = null;
        startupCreditFrame = null;
        startupFaulted = false;
        error = null;
        if (!startup.Active)
        {
            DisposeStartup();
        }
    }

    private void EndStartup()
    {
        if (startup.Active)
        {
            startup.Complete();
            ReleaseStartupStage();
        }
    }

    private void DisposeStartup()
    {
        startupMovie?.Dispose();
        startupMovie = null;
        if (startupMusicStarted)
        {
            fieldAudio?.StopAll();
            startupMusicStarted = false;
        }
        foreach (Texture2D texture in startupTextures.Values)
        {
            texture.Dispose();
        }
        startupTextures.Clear();
        startupSubtract?.Dispose();
        startupSubtract = null;
    }
}
