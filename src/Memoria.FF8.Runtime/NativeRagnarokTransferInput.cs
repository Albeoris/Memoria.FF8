namespace Memoria.FF8.Runtime;

/// <summary>54A7F0 admission and54B860 boarding prerequisites, without manufacturing terrain or53EAF0 placement results.</summary>
public static class NativeRagnarokTransferInput
{
    public static NativeGardenTransferAdmission Evaluate(NativeGardenTransferInput input)
    {
        return NativeGardenTransferState.EvaluateInput(input);
    }

    public static bool PassesBoardingGate(int sourceEntity, uint polygonFlags, int groundHeight, int ragnarokHeight, bool collisionEnabled, bool placementClear)
    {
        return NativeGardenTransferState.PassesBoardingGate(sourceEntity, 50, polygonFlags, groundHeight, ragnarokHeight, collisionEnabled) && placementClear;
    }
}
