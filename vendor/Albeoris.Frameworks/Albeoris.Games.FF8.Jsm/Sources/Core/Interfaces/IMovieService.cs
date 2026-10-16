using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IMovieService
    {
        Boolean IsSupported { get; }

        void PrepareToPlay(Int32 movieId, Boolean flag);
        void Play();
        void Wait();
    }
}