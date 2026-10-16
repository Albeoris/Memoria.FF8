using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Turns off the display of salary alerts.
    /// </summary>
    internal sealed class SARALYDISPOFF : JsmInstruction
    {
        public SARALYDISPOFF()
        {
        }

        public SARALYDISPOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SARALYDISPOFF)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(ISalaryService))
                .Property(nameof(ISalaryService.IsSalaryAlertEnabled))
                .Assign(false)
                .Comment(nameof(SARALYDISPOFF));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Salary[services].IsSalaryAlertEnabled = false;
            return DummyAwaitable.Instance;
        }
    }
}