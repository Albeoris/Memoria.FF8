using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF8.Audio;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private enum Ff7EndingPhase { Inactive, EndingMovie, Credits, CreditsRollOut, Epilogue, Completed }

    private const int EndingCreditMusicTrack = 0x62;
    private const int EndingCreditRollOutUpdates = 112;

    private Ff7EndingPhase endingPhase;
    private Ff7EndingCredits? endingCredits;
    private long endingMenuRequestId;
    private int endingMovieFrame;
    private int endingCreditRow;
    private int endingCreditOffset;
    private double endingCreditClock;
    private double endingRollOutClock;
    private readonly Dictionary<(int Font, int Palette), Texture2D> endingFonts = [];
    private Texture2D? endingAfterCredits;
    private RasterizerState? endingRasterizer;

    internal object? EndingStatus => endingPhase == Ff7EndingPhase.Inactive ? null : new { Phase = endingPhase.ToString(), MovieFrame = endingMovieFrame, CreditRow = endingCreditRow };
    private bool IsEndingPresentationActive => error is null && endingPhase is not (Ff7EndingPhase.Inactive or Ff7EndingPhase.Completed);

    private bool UpdateEndingPresentation(TimeSpan elapsed)
    {
        Ff7Session session = Session!;
        if (endingPhase == Ff7EndingPhase.Inactive)
        {
            Ff7FieldProcess process = session.Host.Process;
            if (session.World is not null || session.Battle is not null || process.Menu is not { Mode: 5, Parameter: 0, Completed: false } menu || process.GlobalCommand != Ff7GlobalCommand.Menu || process.GlobalRequestId != menu.RequestId)
            {
                return false;
            }
            endingCredits = Ff7EndingCredits.Load(installation);
            endingMenuRequestId = menu.RequestId;
            StopAudio();
            StartEndingMovie(Ff7EndingPhase.EndingMovie, 29);
        }
        if (endingPhase == Ff7EndingPhase.Credits)
        {
            AdvanceEndingCredits(elapsed);
            UpdateEndingCreditsMusic(127);
            return true;
        }
        if (endingPhase == Ff7EndingPhase.CreditsRollOut)
        {
            endingRollOutClock += elapsed.TotalSeconds * Ff7EndingCredits.RollOutUpdatesPerSecond;
            int volume = Math.Max(0, (int)(127 * (1 - endingRollOutClock / Ff7EndingCredits.RollOutUpdatesPerSecond)));
            UpdateEndingCreditsMusic(volume);
            if (endingRollOutClock >= EndingCreditRollOutUpdates)
            {
                StopMusicAudio();
                DisposeEndingTextures();
                StartEndingMovie(Ff7EndingPhase.Epilogue, 26);
            }
            return true;
        }

        MoviePresentation playback = movie ?? throw new InvalidOperationException("The FFVII ending has no active movie decoder.");
        playback.Update((frame, _) =>
        {
            endingMovieFrame = frame.FrameIndex;
            return true;
        });
        if (playback.Error is string failure)
        {
            throw new InvalidDataException($"FFVII ending movie decoder failed: {failure}");
        }
        if (!playback.Finished)
        {
            return true;
        }
        StopMovie();
        if (endingPhase == Ff7EndingPhase.EndingMovie)
        {
            StartEndingCreditsMusic(session);
            endingPhase = Ff7EndingPhase.Credits;
            return true;
        }

        session.Host.CompleteMenu(endingMenuRequestId);
        ResetAutoPlay();
        ResetMixedCombat();
        StopAudio();
        session.Dispose();
        Session = null;
        tickRemainder = 0;
        titleSeconds = 0;
        titleSelection = 1;
        sharedInput.SuppressHeldUiInput();
        DisposeEndingTextures();
        endingCredits = null;
        endingPhase = Ff7EndingPhase.Completed;
        return true;
    }

    private void StartEndingMovie(Ff7EndingPhase phase, int id)
    {
        StopMovie();
        Ff7MovieSource source = resolveMovie?.Invoke(id) ?? ResolveMovie(Session!.ReadMovieResources(id), mediaToolsDirectory);
        movie = new MoviePresentation(device, audioOutput, mediaToolsDirectory, source.Path, 0, source.Information, audioEnabled: audioEnabled);
        endingMovieFrame = 0;
        endingPhase = phase;
    }

    private void AdvanceEndingCredits(TimeSpan elapsed)
    {
        Ff7EndingCredits credits = endingCredits!;
        endingCreditClock += elapsed.TotalSeconds * Ff7EndingCredits.ScrollUpdatesPerSecond;
        while (endingCreditClock >= 1)
        {
            endingCreditClock--;
            endingCreditOffset += Ff7EndingCredits.ScrollPixelsPerUpdate;
            if (endingCreditOffset < credits.Rows[endingCreditRow].VerticalAdvance)
            {
                continue;
            }
            endingCreditOffset = 0;
            endingCreditRow++;
            if (endingCreditRow > Ff7EndingCredits.LastScrollingRow)
            {
                endingPhase = Ff7EndingPhase.CreditsRollOut;
                return;
            }
        }
    }

    private void StartEndingCreditsMusic(Ff7Session session)
    {
        if (!audioEnabled)
        {
            return;
        }
        audioNative ??= Ff7PcExecutableData.Load(installation);
        Ff7NativeMusicTimeline timeline = session.Host.DescribeNativeMusic(EndingCreditMusicTrack);
        PcmMusicRepeat repeat = timeline.Loops ? PcmMusicRepeat.SeparateLoop : PcmMusicRepeat.None;
        BeginMusicDecode(audioNative.MusicNames[EndingCreditMusicTrack], repeat, (prepared, _) =>
        {
            musicPlayback = prepared.Start(audioOutput, 0, false, EndingMusicGain(127), 100, audioPaused);
        });
    }

    private int EndingMusicGain(int volume) => NativeGain(volume, dialoguePreferences?.Invoke().MusicVolume ?? 100);

    private void UpdateEndingCreditsMusic(int volume)
    {
        musicDecoder?.Pump();
        musicPlayback?.SetGain(EndingMusicGain(volume));
        musicPlayback?.Update();
    }

    private void DrawEndingPresentation(SpriteBatch batch, Rectangle bounds)
    {
        Rectangle previousScissor = device.ScissorRectangle;
        device.ScissorRectangle = bounds;
        endingRasterizer ??= new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, rasterizerState: endingRasterizer);
        try
        {
            batch.Draw(pixel, bounds, Color.Black);
            if (endingPhase == Ff7EndingPhase.Credits)
            {
                DrawEndingCredits(batch, bounds);
            }
            else if (endingPhase == Ff7EndingPhase.CreditsRollOut)
            {
                DrawEndingCreditRollOut(batch, bounds);
            }
            else if (movie is { HasFrame: true })
            {
                movie.Draw(batch, bounds);
            }
        }
        finally
        {
            batch.End();
            device.ScissorRectangle = previousScissor;
        }
    }

    private void DrawEndingCredits(SpriteBatch batch, Rectangle bounds)
    {
        Ff7EndingCredits credits = endingCredits!;
        (Vector2 origin, Vector2 scale) = EndingCreditViewport(bounds);
        device.ScissorRectangle = Rectangle.Intersect(bounds, new Rectangle((int)origin.X, (int)(origin.Y + 62 * scale.Y), (int)(640 * scale.X), (int)(356 * scale.Y)));
        int y = 62 - endingCreditOffset;
        int end = Math.Min(credits.Rows.Count, endingCreditRow + 11);
        for (int index = endingCreditRow; index < end && y < 418; index++)
        {
            Ff7EndingCreditRow row = credits.Rows[index];
            int width = row.Style < 3 ? 32 : 16;
            int height = row.Style < 3 ? 40 : 20;
            int x = row.CenteredX;
            foreach (byte glyph in row.Glyphs.Span)
            {
                if (glyph == Ff7EndingCredits.CenterCursor)
                {
                    x = Ff7EndingCredits.CanvasWidth / 2;
                    continue;
                }
                Rectangle glyphBounds = new((glyph & 7) * width + (row.Style == 5 ? 128 : 0), (glyph >> 3) * height + (row.Style == 4 ? 128 : 0), width, height);
                batch.Draw(EndingFont(credits, row), origin + new Vector2(x, y) * scale, glyphBounds, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                x += credits.GlyphAdvance(row.Style, glyph);
            }
            y += row.VerticalAdvance;
        }
    }

    private void DrawEndingCreditRollOut(SpriteBatch batch, Rectangle bounds)
    {
        (Vector2 origin, Vector2 scale) = EndingCreditViewport(bounds);
        endingAfterCredits ??= CreateTexture(endingCredits!.AfterCreditsTexture);
        batch.Draw(endingAfterCredits, origin + new Vector2(131, 224) * scale, new Rectangle(0, 0, 200, 32), Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.Draw(endingAfterCredits, origin + new Vector2(324, 224) * scale, new Rectangle(0, 32, 187, 32), Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);

        int update = (int)endingRollOutClock;
        for (int index = 0; index < 76; index++)
        {
            int opacity = Math.Max(0, 255 + update * ((index - 80) >> 1));
            batch.Draw(pixel, origin + new Vector2(131 + 5 * index, 224) * scale, null, Color.Black * (opacity / 255f), 0, Vector2.Zero, new Vector2(5, 32) * scale, SpriteEffects.None, 0);
        }
        int fade = Math.Clamp((update - 85) * 10, 0, 255);
        batch.Draw(pixel, bounds, Color.Black * (fade / 255f));
    }

    private static (Vector2 Origin, Vector2 Scale) EndingCreditViewport(Rectangle bounds)
    {
        PixelBounds fit = ViewportLayout.Fit(Ff7EndingCredits.CanvasWidth, Ff7EndingCredits.CanvasHeight, bounds.Width, bounds.Height);
        return (new Vector2(bounds.X + fit.X, bounds.Y + fit.Y), new Vector2((float)fit.Width / Ff7EndingCredits.CanvasWidth, (float)fit.Height / Ff7EndingCredits.CanvasHeight));
    }

    private Texture2D EndingFont(Ff7EndingCredits credits, Ff7EndingCreditRow row)
    {
        var key = (Math.Min((int)row.Style, 3), row.DrawArgument);
        if (!endingFonts.TryGetValue(key, out Texture2D? texture))
        {
            var image = Ff7FieldRenderer.DecodeTexture(credits.Font(row.Style), useColorKey: true, paletteOverride: row.DrawArgument);
            Color[] colors = image.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray();
            texture = UploadTexture(image.Width, image.Height, colors);
            endingFonts.Add(key, texture);
        }
        return texture;
    }

    private void DisposeEndingTextures()
    {
        foreach (Texture2D texture in endingFonts.Values)
        {
            texture.Dispose();
        }
        endingFonts.Clear();
        endingAfterCredits?.Dispose();
        endingAfterCredits = null;
        endingRasterizer?.Dispose();
        endingRasterizer = null;
    }

    private void ResetEndingPresentation()
    {
        DisposeEndingTextures();
        endingPhase = Ff7EndingPhase.Inactive;
        endingCredits = null;
        endingMenuRequestId = 0;
        endingMovieFrame = 0;
        endingCreditRow = 0;
        endingCreditOffset = 0;
        endingCreditClock = 0;
        endingRollOutClock = 0;
    }
}
