using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Opens a field message window and lets player choose a single line. AASK saves the chosen line index (first option is always 0) into a temp variable which you can retrieve with PSHI_L 0.
    /// </summary>
    public sealed class AASK : JsmInstruction, IPositionedMessageInstruction, IChoiceMessageInstruction
    {
        public IJsmExpression Channel { get; }
        public IJsmExpression MessageId { get; }
        public IJsmExpression FirstLine { get; }
        public IJsmExpression LastLine { get; }
        public IJsmExpression BeginLine { get; }
        public IJsmExpression CancelLine { get; }
        public IJsmExpression PosX { get; }
        public IJsmExpression PosY { get; }

        IJsmExpression IMessageInstruction.MessageIdExpression => MessageId;

        IJsmExpression IPositionedMessageInstruction.PosXExpression => PosX;

        IJsmExpression IPositionedMessageInstruction.PosYExpression => PosY;

        IJsmExpression IChoiceMessageInstruction.FirstLineExpression => FirstLine;

        IJsmExpression IChoiceMessageInstruction.LastLineExpression => LastLine;

        IJsmExpression IChoiceMessageInstruction.BeginLineExpression => BeginLine;

        IJsmExpression IChoiceMessageInstruction.CancelLineExpression => CancelLine;

        public AASK(IJsmExpression channel, IJsmExpression messageId, IJsmExpression firstLine, IJsmExpression lastLine, IJsmExpression beginLine, IJsmExpression cancelLine, IJsmExpression posX, IJsmExpression posY)
        {
            Channel = channel;
            MessageId = messageId;
            FirstLine = firstLine;
            LastLine = lastLine;
            BeginLine = beginLine;
            CancelLine = cancelLine;
            PosX = posX;
            PosY = posY;
        }

        public AASK(Int32 parameter, IExpressionStack stack)
            : this(
                posY: stack.Pop(),
                posX: stack.Pop(),
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
            return $"{nameof(AASK)}({nameof(Channel)}: {Channel}, {nameof(MessageId)}: {MessageId}, {nameof(FirstLine)}: {FirstLine}, {nameof(LastLine)}: {LastLine}, {nameof(BeginLine)}: {BeginLine}, {nameof(CancelLine)}: {CancelLine}, {nameof(PosX)}: {PosX}, {nameof(PosY)}: {PosY})";
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
                .Argument("posX", PosX)
                .Argument("posY", PosY)
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
                CancelLine.Int32(services),
                PosX.Int32(services),
                PosY.Int32(services));
        }
    }
}
