namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal void ValidateHostedPresentationRandomOwner(BattleSession logicalOwner)
    {
        if (!hostedBattlePresentation || logicalOwner.hostedBattlePresentation || !logicalOwner.UsesExpandedRoster || ReferenceEquals(this, logicalOwner))
        {
            throw new InvalidOperationException("Hosted random exchange requires the presentation child and its logical battle.");
        }

        if (definition.NativeGlobalRandom is not { } initial || initial != logicalOwner.definition.NativeGlobalRandom ||
            nativeGlobalRandom is null || logicalOwner.nativeGlobalRandom is null ||
            (definition.NativeCrtRandom is null) != (logicalOwner.definition.NativeCrtRandom is null) ||
            (nativeCrtRandom is null) != (definition.NativeCrtRandom is null) ||
            (logicalOwner.nativeCrtRandom is null) != (logicalOwner.definition.NativeCrtRandom is null))
        {
            throw new InvalidDataException("Hosted presentation and the logical battle require matching original global RNG and CRT ownership.");
        }

        NativeGlobalRandomChange!.Validate();
        logicalOwner.NativeGlobalRandomChange!.Validate();
    }

    internal void ImportHostedPresentationRandom(BattleSession logicalOwner)
    {
        ValidateHostedPresentationRandomOwner(logicalOwner);
        nativeCrtRandom = logicalOwner.nativeCrtRandom;
        nativeGlobalRandom = logicalOwner.nativeGlobalRandom;
        nativeGlobalRandomCalls = logicalOwner.nativeGlobalRandomCalls;
    }

    internal void ExportHostedPresentationRandom(BattleSession logicalOwner)
    {
        // A failed native callback can already have consumed process RNG. Export must not revalidate or replace that failure.
        logicalOwner.nativeCrtRandom = nativeCrtRandom;
        logicalOwner.nativeGlobalRandom = nativeGlobalRandom;
        logicalOwner.nativeGlobalRandomCalls = nativeGlobalRandomCalls;
    }
}
