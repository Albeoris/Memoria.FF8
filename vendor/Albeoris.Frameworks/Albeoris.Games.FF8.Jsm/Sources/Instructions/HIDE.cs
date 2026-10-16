using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Hides this entity's model on the field. See also SHOW.
    /// </summary>
    internal sealed class HIDE : JsmInstruction
    {
        public HIDE()
        {
        }

        public HIDE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(HIDE)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .Property(nameof(FieldObject.Model))
                .Method(nameof(FieldObjectModel.Hide))
                .Comment(nameof(HIDE));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            FieldObject currentObject = ServiceId.Field[services].Engine.CurrentObject;
            currentObject.Model.Hide();
            return DummyAwaitable.Instance;
        }
    }
}