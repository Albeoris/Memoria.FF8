namespace Albeoris.Games.FF8.Jsm.Instructions;

/// <summary>Identifies a message instruction that targets a message window channel.</summary>
public interface IChannelMessageInstruction : IMessageInstruction
{
    IJsmExpression ChannelExpression { get; }
}
