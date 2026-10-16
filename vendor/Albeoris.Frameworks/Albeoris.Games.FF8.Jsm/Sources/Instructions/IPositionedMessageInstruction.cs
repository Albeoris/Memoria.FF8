namespace Albeoris.Games.FF8.Jsm.Instructions;

/// <summary>
/// Identifies a message instruction that controls the position of its message window.
/// </summary>
public interface IPositionedMessageInstruction : IMessageInstruction
{
    IJsmExpression PosXExpression { get; }

    IJsmExpression PosYExpression { get; }
}
