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
            (logicalOwner.nativeCrtRandom is null) != (logicalOwner.definition.NativeCrtRandom is null) ||
            definition.NativeBattleUiRandom != logicalOwner.definition.NativeBattleUiRandom ||
            (nativeBattleUiRandom is null) != (definition.NativeBattleUiRandom is null) ||
            (logicalOwner.nativeBattleUiRandom is null) != (logicalOwner.definition.NativeBattleUiRandom is null) ||
            nativeBattleUiRandom?.State >= 0x8000 || logicalOwner.nativeBattleUiRandom?.State >= 0x8000)
        {
            throw new InvalidDataException("Hosted presentation and the logical battle require matching original global, CRT and UI RNG ownership.");
        }

        NativeGlobalRandomChange!.Validate();
        logicalOwner.NativeGlobalRandomChange!.Validate();
    }

    internal void ImportHostedPresentationRandom(BattleSession logicalOwner)
    {
        ValidateHostedPresentationRandomOwner(logicalOwner);
        CopyHostedPresentationRandomFrom(logicalOwner);
    }

    internal void CopyHostedPresentationRandomFrom(BattleSession logicalOwner)
    {
        nativeCrtRandom = logicalOwner.nativeCrtRandom;
        nativeBattleUiRandom = logicalOwner.nativeBattleUiRandom;
        nativeGlobalRandom = logicalOwner.nativeGlobalRandom;
        nativeGlobalRandomCalls = logicalOwner.nativeGlobalRandomCalls;
    }

    internal void ExportHostedPresentationRandom(BattleSession logicalOwner)
    {
        // A failed native callback can already have consumed process RNG. Export must not revalidate or replace that failure.
        logicalOwner.CopyHostedPresentationRandomFrom(this);
    }
}
