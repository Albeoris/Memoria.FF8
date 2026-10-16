using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Turns on the display of salary alerts.
    /// </summary>
    internal sealed class SARALYDISPON : JsmInstruction
    {
        public SARALYDISPON()
        {
        }

        public SARALYDISPON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SARALYDISPON)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(ISalaryService))
                .Property(nameof(ISalaryService.IsSalaryAlertEnabled))
                .Assign(true)
                .Comment(nameof(SARALYDISPON));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Salary[services].IsSalaryAlertEnabled = true;
            return DummyAwaitable.Instance;
        }
    }
}