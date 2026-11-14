namespace Memoria.FF8.Runtime;

public static class NativeBattleEffectModelTransforms
{
    public static bool ActorRelative(NativeBattleEffectModelTransformPolicy policy, Func<(NativeBattleStackBytes Bytes, int Offset)> readCallerScratch)
    {
        switch (policy)
        {
            case NativeBattleEffectModelTransformPolicy.HostActorRelative:
                return true;
            case NativeBattleEffectModelTransformPolicy.NativeCallerScratch:
                (NativeBattleStackBytes bytes, int offset) = readCallerScratch();
                return bytes.IsNonzero(offset);
            default:
                throw new InvalidDataException("The model transform policy is unsupported.");
        }
    }
}
