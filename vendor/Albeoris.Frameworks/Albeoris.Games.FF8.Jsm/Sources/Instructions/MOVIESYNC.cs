using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Pauses execution of this script until the current FMV movie is finished playing.
    /// </summary>
    internal sealed class MOVIESYNC : JsmInstruction
    {
        public MOVIESYNC()
        {
        }

        public MOVIESYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVIESYNC)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IMovieService))
                .Method(nameof(IMovieService.Wait))
                .Comment(nameof(MOVIE));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Movie[services].Wait();
            return DummyAwaitable.Instance;
        }
    }
}