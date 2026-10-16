using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Enables the payment of salaries.
    /// </summary>
    internal sealed class SARALYON : JsmInstruction
    {
        public SARALYON()
        {
        }

        public SARALYON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SARALYON)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(ISalaryService))
                .Property(nameof(ISalaryService.IsSalaryEnabled))
                .Assign(true)
                .Comment(nameof(SARALYON));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Salary[services].IsSalaryEnabled = true;
            return DummyAwaitable.Instance;
        }
    }
}