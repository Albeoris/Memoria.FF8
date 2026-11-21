using Memoria.FF8.Media;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed class MovieSubtitleRenderer(GraphicsDevice device, MovieSubtitleTrack track) : IDisposable
{
    private readonly MovieSubtitleCue[] cues = track.Cues.OrderBy(cue => cue.Layer).ToArray();
    private readonly Dictionary<MovieSubtitleCue, MovieSubtitleRaster> rasters = new(ReferenceEqualityComparer.Instance);
    private int rasterHeight;

    public void Draw(SpriteBatch batch, Rectangle movieBounds, double seconds)
    {
        if (movieBounds.Width <= 0 || movieBounds.Height <= 0)
        {
            return;
        }
        if (rasterHeight != movieBounds.Height)
        {
            ClearRasters();
            rasterHeight = movieBounds.Height;
        }
        foreach (MovieSubtitleCue cue in cues)
        {
            if (seconds >= cue.StartSeconds && seconds < cue.EndSeconds)
            {
                DrawCue(batch, movieBounds, cue, seconds);
            }
            else if (rasters.Remove(cue, out MovieSubtitleRaster? expired))
            {
                expired.Dispose();
            }
        }
    }

    private void DrawCue(SpriteBatch batch, Rectangle bounds, MovieSubtitleCue cue, double seconds)
    {
        double elapsed = seconds - cue.StartSeconds;
        MovieSubtitleAnimation animation = MovieSubtitleAnimation.At(cue.Keyframes, elapsed);
        if (animation.Opacity <= 0 || animation.Reveal <= 0 || animation.ScaleX <= 0 || animation.ScaleY <= 0)
        {
            return;
        }
        if (!rasters.TryGetValue(cue, out MovieSubtitleRaster? raster))
        {
            raster = new MovieSubtitleRaster(device, cue, bounds.Height);
            rasters.Add(cue, raster);
        }
        if (raster.Ink.IsEmpty)
        {
            return;
        }

        Vector2 scale = new Vector2(animation.ScaleX, animation.ScaleY) / raster.PixelsPerScreenPixel;
        Rectangle ink = raster.Ink;
        if (ink.Width * scale.X < 0.25f || ink.Height * scale.Y < 0.25f)
        {
            return;
        }
        Vector2 anchor = new(bounds.X + (cue.Style.X + animation.OffsetX) * bounds.Width, bounds.Y + (cue.Style.Y + animation.OffsetY) * bounds.Height);
        Vector2 origin = anchor - new Vector2(ink.X + ink.Width / 2f, ink.Y + ink.Height / 2f) * scale;
        Texture2D texture = raster.GetTexture(animation.Blur, out int padding);
        Rectangle source = ink;
        source.Inflate(padding, padding);
        source = Rectangle.Intersect(texture.Bounds, source);
        if (animation.Reveal < 1)
        {
            int edge = ink.Left + (int)MathF.Ceiling(ink.Width * animation.Reveal);
            source.Width = edge - source.X;
        }

        Vector2 split = new(animation.SplitX * bounds.Width, animation.SplitY * bounds.Height);
        if (split == Vector2.Zero)
        {
            DrawCopy(batch, texture, bounds, raster, animation, source, origin, scale, elapsed);
            return;
        }
        float colorAlpha = raster.Color.A / 255f;
        if (colorAlpha == 0)
        {
            return;
        }
        // Two overlapping copies should begin at roughly the opacity of the original
        // line. As they separate, each carries half of the authored opacity.
        float coincidentAlpha = 1 - MathF.Sqrt(1 - animation.Opacity * colorAlpha);
        float separatedAlpha = animation.Opacity * colorAlpha * 0.5f;
        float separation = Math.Min(1, split.Length() / (cue.Style.FontSize * bounds.Height));
        MovieSubtitleAnimation copy = animation with { Opacity = MathHelper.Lerp(coincidentAlpha, separatedAlpha, separation) / colorAlpha };
        DrawCopy(batch, texture, bounds, raster, copy, source, origin - split, scale, elapsed);
        DrawCopy(batch, texture, bounds, raster, copy, source, origin + split, scale, elapsed);
    }

    private static void DrawCopy(SpriteBatch batch, Texture2D texture, Rectangle bounds, MovieSubtitleRaster raster, MovieSubtitleAnimation animation, Rectangle piece, Vector2 origin, Vector2 scale, double elapsed)
    {
        bool fragmented = animation.Scatter > 0 || animation.Noise > 0;
        if (!fragmented && animation.Ripple == 0)
        {
            DrawPiece(batch, texture, bounds, piece, origin, scale, raster.Color, animation.Brightness, animation.Opacity, animation.Blur > 0);
            return;
        }

        float em = raster.EmPixels / raster.PixelsPerScreenPixel;
        int noiseFrame = (int)Math.Floor(elapsed * 24);
        int tileWidth = Math.Max(4, (int)MathF.Round(raster.EmPixels * (animation.Noise > 0 || !fragmented ? 0.17f : 0.55f)));
        int tileHeight = fragmented ? tileWidth : piece.Height;
        for (int y = piece.Top; y < piece.Bottom; y += tileHeight)
        {
            for (int x = piece.Left; x < piece.Right; x += tileWidth)
            {
                Rectangle tile = new(x, y, Math.Min(tileWidth, piece.Right - x), Math.Min(tileHeight, piece.Bottom - y));
                Vector2 offset = Vector2.Zero;
                float brightness = animation.Brightness;
                float opacity = animation.Opacity;
                if (fragmented)
                {
                    float scatter = animation.Scatter * em;
                    offset = (new Vector2(Noise(x, y, 0, 0), Noise(x, y, 0, 1)) - new Vector2(0.5f)) * (2 * scatter);
                    offset += (new Vector2(Noise(x, y, noiseFrame, 2), Noise(x, y, noiseFrame, 3)) - new Vector2(0.5f)) * (animation.Noise * em * 0.3f);
                    brightness *= 1 + animation.Noise * (Noise(x, y, noiseFrame, 4) - 0.5f);
                    opacity *= 1 - animation.Noise * Noise(x, y, noiseFrame, 5) * 0.85f;
                }
                if (animation.Ripple > 0)
                {
                    float phase = (tile.Center.X - raster.Ink.Left) / raster.EmPixels * MathHelper.TwoPi - (float)elapsed * 7;
                    offset.Y += MathF.Sin(phase) * animation.Ripple * em;
                }
                DrawPiece(batch, texture, bounds, tile, origin + offset, scale, raster.Color, brightness, opacity, animation.Blur > 0);
            }
        }
    }

    private static void DrawPiece(SpriteBatch batch, Texture2D texture, Rectangle bounds, Rectangle source, Vector2 origin, Vector2 scale, Color color, float brightness, float opacity, bool blurred)
    {
        Vector2 position = origin + new Vector2(source.X, source.Y) * scale;
        if (!SpriteRegionDrawing.TryClip(source, position, scale, bounds, out Rectangle croppedSource, out Vector2 croppedPosition))
        {
            return;
        }
        Color tint = new(Math.Min(1, color.R / 255f * brightness), Math.Min(1, color.G / 255f * brightness), Math.Min(1, color.B / 255f * brightness));
        float alpha = color.A / 255f * opacity;
        batch.Draw(texture, croppedPosition, croppedSource, tint * alpha, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        if (blurred && brightness > 1)
        {
            // Extra coverage lets the blur halo brighten even when the tint has reached white.
            float bloom = alpha * (1 - 1 / brightness);
            batch.Draw(texture, croppedPosition, croppedSource, tint * bloom, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }

    private static float Noise(int x, int y, int frame, int component)
    {
        // Hash the source tile and movie time so a seek reproduces the same effects.
        uint bits = unchecked((uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ (uint)frame * 0xC2B2AE35u ^ (uint)(component + 1) * 0x27D4EB2Fu);
        bits = unchecked((bits ^ (bits >> 16)) * 0x7FEB352Du);
        bits = unchecked((bits ^ (bits >> 15)) * 0x846CA68Bu);
        return (bits >> 8) / 16777215f;
    }

    private void ClearRasters()
    {
        foreach (MovieSubtitleRaster raster in rasters.Values)
        {
            raster.Dispose();
        }
        rasters.Clear();
    }

    public void Dispose()
    {
        ClearRasters();
    }
}
