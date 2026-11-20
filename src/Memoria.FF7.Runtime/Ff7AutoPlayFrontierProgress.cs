namespace Memoria.FF7.Runtime;

/// <summary>Read-only diagnostic evidence; an attention interval does not establish failure or limit execution.</summary>
public sealed record Ff7AutoPlayFrontierProgress(string? Key, int IdleSteps, int AttentionInterval);
