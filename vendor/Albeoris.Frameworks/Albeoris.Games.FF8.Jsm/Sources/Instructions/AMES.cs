using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Pop up a message window until WINCLOSE or MESSYNC is called.
    /// </summary>
    public sealed class AMES : JsmInstruction, IPositionedMessageInstruction
    {
        private readonly Jsm.Expression.PSHN_L _channel;
        private readonly Jsm.Expression.PSHN_L _messageId;
        private readonly Jsm.Expression.PSHN_L _posX;
        private readonly Jsm.Expression.PSHN_L _posY;

        public Int32 Channel
        {
            get => _channel.Value;
            set => _channel.Value = value;
        }

        public Int32 MessageId
        {
            get => _messageId.Value;
            set => _messageId.Value = value;
        }

        public Int32 PosX
        {
            get => _posX.Value;
            set => _posX.Value = value;
        }

        public Int32 PosY
        {
            get => _posY.Value;
            set => _posY.Value = value;
        }

        IJsmExpression IMessageInstruction.MessageIdExpression => _messageId;

        IJsmExpression IPositionedMessageInstruction.PosXExpression => _posX;

        IJsmExpression IPositionedMessageInstruction.PosYExpression => _posY;

        public AMES(Int32 channel, Int32 messageId, Int32 posX, Int32 posY)
            : this(
                new Jsm.Expression.PSHN_L(channel),
                new Jsm.Expression.PSHN_L(messageId),
                new Jsm.Expression.PSHN_L(posX),
                new Jsm.Expression.PSHN_L(posY))
        {
        }

        private AMES(
            Jsm.Expression.PSHN_L channel,
            Jsm.Expression.PSHN_L messageId,
            Jsm.Expression.PSHN_L posX,
            Jsm.Expression.PSHN_L posY)
        {
            _channel = channel;
            _messageId = messageId;
            _posX = posX;
            _posY = posY;
        }

        public AMES(Int32 parameter, IExpressionStack stack)
            : this(
                posY: PopConstant(stack, nameof(PosY)),
                posX: PopConstant(stack, nameof(PosX)),
                messageId: PopConstant(stack, nameof(MessageId)),
                channel: PopConstant(stack, nameof(Channel)))
        {
        }

        private static Jsm.Expression.PSHN_L PopConstant(IExpressionStack stack, String operandName)
        {
            return stack.Pop() as Jsm.Expression.PSHN_L
                ?? throw new InvalidProgramException($"{nameof(AMES)} operand {operandName} must be PSHN_L.");
        }

        public override String ToString()
        {
            return $"{nameof(AMES)}({nameof(Channel)}: {Channel}, {nameof(MessageId)}: {MessageId}, {nameof(PosX)}: {PosX}, {nameof(PosY)}: {PosY})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            FormatHelper.FormatMonologue(sw, formatterContext.GetMessage(MessageId));

            sw.Format(formatterContext, services)
                .StaticType(nameof(IMessageService))
                .Method(nameof(IMessageService.Show))
                .Argument("channel", Channel)
                .Argument("messageId", MessageId)
                .Argument("posX", PosX)
                .Argument("posY", PosY)
                .Comment(nameof(AMES));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Message[services].Show(Channel, MessageId, PosX, PosY);
            return DummyAwaitable.Instance;
        }
    }
}
