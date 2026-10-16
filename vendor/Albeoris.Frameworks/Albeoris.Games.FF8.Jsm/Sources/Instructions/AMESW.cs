using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Pop up a message window and pauses script execution until the player dismisses the window.
    /// </summary>
    public sealed class AMESW : JsmInstruction, IPositionedMessageInstruction
    {
        public IJsmExpression Channel { get; }
        public IJsmExpression MessageId { get; }
        public IJsmExpression PosX { get; }
        public IJsmExpression PosY { get; }

        IJsmExpression IMessageInstruction.MessageIdExpression => MessageId;

        IJsmExpression IPositionedMessageInstruction.PosXExpression => PosX;

        IJsmExpression IPositionedMessageInstruction.PosYExpression => PosY;

        public AMESW(IJsmExpression channel, IJsmExpression messageId, IJsmExpression posX, IJsmExpression posY)
        {
            Channel = channel;
            MessageId = messageId;
            PosX = posX;
            PosY = posY;
        }

        public AMESW(Int32 parameter, IExpressionStack stack)
            : this(
                posY: stack.Pop(),
                posX: stack.Pop(),
                messageId: stack.Pop(),
                channel: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(AMESW)}({nameof(Channel)}: {Channel}, {nameof(MessageId)}: {MessageId}, {nameof(PosX)}: {PosX}, {nameof(PosY)}: {PosY})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            if (MessageId is IConstExpression message)
                FormatHelper.FormatMonologue(sw, formatterContext.GetMessage(message.Int32()));

            sw.Format(formatterContext, services)
                .Await()
                .StaticType(nameof(IMessageService))
                .Method(nameof(IMessageService.ShowDialog))
                .Argument("channel", Channel)
                .Argument("messageId", MessageId)
                .Argument("posX", PosX)
                .Argument("posY", PosY)
                .Comment(nameof(AMESW));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            return ServiceId.Message[services].ShowDialog(
                Channel.Int32(services),
                MessageId.Int32(services),
                PosX.Int32(services),
                PosY.Int32(services));
        }
    }
}
