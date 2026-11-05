using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A logical GJMP call site and its live operand boundary. Target/return instructions derive from the matching installed program.</summary>
public sealed record GameplaySaveGlobalCall([property: JsonRequired] int CallOperation, [property: JsonRequired] int OperandBase, [property: JsonRequired] bool Entered);
