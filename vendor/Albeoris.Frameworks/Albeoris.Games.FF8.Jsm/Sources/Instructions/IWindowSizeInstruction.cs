namespace Albeoris.Games.FF8.Jsm.Instructions;

/// <summary>Identifies an instruction that configures the location and size of a message window.</summary>
public interface IWindowSizeInstruction : IJsmInstruction
{
    IJsmExpression ChannelExpression { get; }

    IJsmExpression XExpression { get; }

    IJsmExpression YExpression { get; }

    IJsmExpression WidthExpression { get; }

    IJsmExpression HeightExpression { get; }
}
