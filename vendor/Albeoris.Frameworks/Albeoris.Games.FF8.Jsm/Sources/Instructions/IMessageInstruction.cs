namespace Albeoris.Games.FF8.Jsm.Instructions;

/// <summary>
/// Identifies an instruction whose arguments include a message identifier.
/// </summary>
public interface IMessageInstruction : IJsmInstruction
{
    IJsmExpression MessageIdExpression { get; }
}
