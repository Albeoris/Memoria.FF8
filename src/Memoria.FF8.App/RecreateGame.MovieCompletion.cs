using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private bool movieCompletionPresented;

    private void PreserveCompletedMovieFrame()
    {
        if (session.IsMoviePlaying)
        {
            movieCompletionPresented = false;
            return;
        }
        if (movieCompletionPresented || session.State.MoviePlayback is not { Completed: true } playback)
        {
            return;
        }
        movieCompletionPresented = true;
        MovieCompletionFrame? skipped = playback.Skipped ? movieClock.ReadSkippedCompletionFrame() : null;
        if (skipped is not null)
        {
            RetainMovieSurface(skipped.Pixels, skipped.Width, skipped.Height);
        }
        else if (movie?.HasFrame == true)
        {
            RetainMovieSurface(movie.DecodedPixels, movie.Texture.Width, movie.Texture.Height);
        }
    }

    private void RetainMovieSurface(ReadOnlySpan<byte> pixels, int width, int height)
    {
        // MOVIESYNC retains the pure decoded video surface across field allocation.
        // Ordinary field actors and fade are submitted by their next real draw callback.
        Texture2D retained = new(GraphicsDevice, width, height);
        retained.SetData(pixels.ToArray());
        fieldTexture.Dispose();
        fieldTexture = retained;
        fieldGpuPresented = false;
    }
}
