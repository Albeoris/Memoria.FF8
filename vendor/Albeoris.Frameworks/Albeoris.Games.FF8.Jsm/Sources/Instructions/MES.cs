using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    /// <summary>
    /// Popup a message window.
    /// This is usually used on lines to popup text when the player crosses a certain point on a screen.
    /// The size of the message window can be set with WINSIZE.
    /// </summary>
    public sealed class MES : JsmInstruction, IChannelMessageInstruction
    {
        private readonly Jsm.Expression.PSHN_L _channel;
        private readonly Jsm.Expression.PSHN_L _messageId;

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

        IJsmExpression IMessageInstruction.MessageIdExpression => _messageId;

        IJsmExpression IChannelMessageInstruction.ChannelExpression => _channel;

        public MES(Int32 channel, Int32 messageId)
            : this(new Jsm.Expression.PSHN_L(channel), new Jsm.Expression.PSHN_L(messageId))
        {
        }

        private MES(Jsm.Expression.PSHN_L channel, Jsm.Expression.PSHN_L messageId)
        {
            _channel = channel;
            _messageId = messageId;
        }

        public MES(Int32 parameter, IExpressionStack stack)
            : this(
                messageId: PopConstant(stack, nameof(MessageId)),
                channel: PopConstant(stack, nameof(Channel)))
        {
        }

        private static Jsm.Expression.PSHN_L PopConstant(IExpressionStack stack, String operandName)
        {
            return stack.Pop() as Jsm.Expression.PSHN_L
                ?? throw new InvalidProgramException($"{nameof(MES)} operand {operandName} must be PSHN_L.");
        }

        public override String ToString()
        {
            return $"{nameof(MES)}({nameof(Channel)}: {Channel}, {nameof(MessageId)}: {MessageId})";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            FormatHelper.FormatMonologue(sw, formatterContext.GetMessage(MessageId));

            sw.Format(formatterContext, services)
                .StaticType(nameof(IMessageService))
                .Method(nameof(IMessageService.Show))
                .Argument("channel", Channel)
                .Argument("messageId", MessageId)
                .Comment(nameof(MES));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Message[services].Show(Channel, MessageId);
            return DummyAwaitable.Instance;
        }
    }
}
