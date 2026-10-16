namespace Albeoris.Games.FF8.Jsm.Instructions;

/// <summary>
/// Identifies a message instruction that presents a range of selectable lines.
/// </summary>
public interface IChoiceMessageInstruction : IMessageInstruction
{
    IJsmExpression FirstLineExpression { get; }

    IJsmExpression LastLineExpression { get; }

    IJsmExpression BeginLineExpression { get; }

    IJsmExpression CancelLineExpression { get; }
}
