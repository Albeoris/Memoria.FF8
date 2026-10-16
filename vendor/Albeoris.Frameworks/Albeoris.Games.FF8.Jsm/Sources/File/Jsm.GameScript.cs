using System;
using System.Collections.Generic;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;
using Albeoris.Games.FF8.Jsm.Instructions;

namespace Albeoris.Games.FF8.Jsm
{
    public static partial class Jsm
    {
        public sealed class GameScript
        {
            public Int32 ScriptId { get; }
            public Jsm.ExecutableSegment Segment { get; }
            public IReadOnlyList<IJsmInstruction> Instructions { get; }

            public GameScript(Int32 scriptId, Jsm.ExecutableSegment segment)
                : this(scriptId, segment, Array.Empty<IJsmInstruction>())
            {
            }

            public GameScript(
                Int32 scriptId,
                Jsm.ExecutableSegment segment,
                IReadOnlyList<IJsmInstruction> instructions)
            {
                ScriptId = scriptId;
                Segment = segment;
                Instructions = instructions;
            }

            public void FormatMethod(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices executionContext)
            {
                String methodName = GetMethodName(formatterContext);

                sw.AppendLine($"public void {methodName}()");
                {
                    sw.AppendLine("{");
                    sw.Indent++;

                    FormatMethodBody(sw, formatterContext, executionContext);

                    sw.Indent--;
                    sw.AppendLine("}");
                }
            }

            public void FormatMethodBody(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices executionContext)
            {
                Segment.Format(sw, formatterContext, executionContext);
            }

            private String GetMethodName(IScriptFormatterContext formatterContext)
            {
                formatterContext.GetObjectScriptNamesById(ScriptId, out _, out String methodName);
                if (Char.IsLower(methodName[0]))
                    methodName = Char.ToUpperInvariant(methodName[0]) + methodName.Substring(1);
                return methodName;
            }
        }
    }
}
