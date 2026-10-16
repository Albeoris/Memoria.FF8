namespace Albeoris.Games.FF8.Jsm.Messages;

/// <summary>Describes the changes made while applying a field message context.</summary>
public sealed record FieldMessageContextApplyResult(Int32 UpdatedInstructionCount, Int32 UpdatedOperandCount);
