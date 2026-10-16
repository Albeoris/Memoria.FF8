using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Opens a field message window and lets player choose a single line.
    /// ASK saves the chosen line index into a temp variable which you can retrieve with PSHI_L 0.
    /// AASK is an upgrade that also lets you set the window position.
    /// </summary>
    public sealed class ASK : JsmInstruction, IChoiceMessageInstruction
    {
        public IJsmExpression Channel { get; }
        public IJsmExpression MessageId { get; }
        public IJsmExpression FirstLine { get; }
        public IJsmExpression LastLine { get; }
        public IJsmExpression BeginLine { get; }
        public IJsmExpression CancelLine { get; }

        IJsmExpression IMessageInstruction.MessageIdExpression => MessageId;

        IJsmExpression IChoiceMessageInstruction.FirstLineExpression => FirstLine;

        IJsmExpression IChoiceMessageInstruction.LastLineExpression => LastLine;

        IJsmExpression IChoiceMessageInstruction.BeginLineExpression => BeginLine;

        IJsmExpression IChoiceMessageInstruction.CancelLineExpression => CancelLine;

        public ASK(IJsmExpression channel, IJsmExpression messageId, IJsmExpression firstLine, IJsmExpression lastLine, IJsmExpression beginLine, IJsmExpression cancelLine)
        {
            Channel = channel;
            MessageId = messageId;
            FirstLine = firstLine;
            LastLine = lastLine;
            BeginLine = beginLine;
            CancelLine = cancelLine;
        }

        public ASK(Int32 parameter, IExpressionStack stack)
            : this(
                cancelLine: stack.Pop(),
                beginLine: stack.Pop(),
                lastLine: stack.Pop(),
                firstLine: stack.Pop(),
                messageId: stack.Pop(),
                channel: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(ASK)}({nameof(Channel)}: {Channel}, {nameof(MessageId)}: {MessageId}, {nameof(FirstLine)}: {FirstLine}, {nameof(LastLine)}: {LastLine}, {nameof(BeginLine)}: {BeginLine}, {nameof(CancelLine)}: {CancelLine})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            if (MessageId is IConstExpression message)
                FormatHelper.FormatAnswers(sw, formatterContext.GetMessage(message.Int32()), FirstLine, LastLine, BeginLine, CancelLine);

            sw.Format(formatterContext, services)
                .Await()
                .StaticType(nameof(IMessageService))
                .Method(nameof(IMessageService.ShowDialog))
                .Argument("channel", Channel)
                .Argument("messageId", MessageId)
                .Argument("firstLine", FirstLine)
                .Argument("lastLine", LastLine)
                .Argument("beginLine", BeginLine)
                .Argument("cancelLine", CancelLine)
                .Comment(nameof(AASK));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            return ServiceId.Message[services].ShowQuestion(
                Channel.Int32(services),
                MessageId.Int32(services),
                FirstLine.Int32(services),
                LastLine.Int32(services),
                BeginLine.Int32(services),
                CancelLine.Int32(services));
        }
    }
}
