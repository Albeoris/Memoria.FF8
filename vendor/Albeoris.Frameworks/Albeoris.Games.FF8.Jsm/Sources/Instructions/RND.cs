using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Pushes a random number into temp variable 0 in the range [0-255].
    /// </summary>
    internal sealed class RND : JsmInstruction
    {
        public static ScriptResultId ResultVariable { get; } = new ScriptResultId(0);

        public RND()
        {
        }

        public RND(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(RND)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.AppendLine($"R{ResultVariable.ResultId} = Random.Shared.Next(0, 256);");
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Interaction[services][ResultVariable] = Random.Shared.Next(0, 256);

            return DummyAwaitable.Instance;
        }
    }
}
