namespace Memoria.FF8.Runtime;

/// <summary>Selects whether projected outlines consume native caller scratch or independent host geometry runs.</summary>
public enum NativeBattleRibbonProjectionPolicy
{
    NativeCallerScratch,
    IndependentGeometricRuns
}
