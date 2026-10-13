namespace Memoria.FF8.Runtime;

/// <summary>Original simulation may continue while a newly loaded world has not executed its first drawable callback.</summary>
public sealed class NativeWorldPresentationDeferredException(string message) : NotSupportedException(message);
