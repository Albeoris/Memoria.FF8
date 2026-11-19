namespace Memoria.FF7.Runtime;

/// <summary>Actual search work, separate from live gameplay callbacks and scheduling attempts.</summary>
public sealed record Ff7AutoPlayPlanningProgress(bool Pending, long PhysicalNodes, long DetachedNativeCallbacks, int NativeCandidates);
