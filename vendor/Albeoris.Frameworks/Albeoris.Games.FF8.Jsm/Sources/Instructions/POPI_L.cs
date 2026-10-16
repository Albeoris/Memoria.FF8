using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;
using Jsm = Albeoris.Games.FF8.Jsm.Jsm;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Temp[index] = value;
    /// </summary>
    internal sealed class POPI_L : JsmInstruction
    {
        private ScriptResultId _index;
        private IJsmExpression _value;

        public POPI_L(ScriptResultId index, IJsmExpression value)
        {
            _index = index;
            _value = value;
        }

        public POPI_L(Int32 parameter, IExpressionStack stack)
            : this(new ScriptResultId(parameter),
                value: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(POPI_L)}({nameof(_index)}: {_index}, {nameof(_value)}: {_value})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Append($"R{_index.ResultId}");
            sw.Append(" = ");
            _value.Format(sw, formatterContext, services);
            sw.AppendLine(";");
        }

        public override IAwaitable TestExecute(IServices services)
        {
            Int32 value = _value.Int32(services);
            ServiceId.Interaction[services][_index] = value;
            return DummyAwaitable.Instance;
        }
    }
}