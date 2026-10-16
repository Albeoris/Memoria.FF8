using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MOVIE : JsmInstruction
    {
        public MOVIE()
        {
        }

        public MOVIE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVIE)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IMovieService))
                .Method(nameof(IMovieService.Play))
                .Comment(nameof(MOVIE));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Movie[services].Play();
            return DummyAwaitable.Instance;
        }
    }
}