namespace Memoria.FF8.Runtime;

public static class NativeWorldEntityTypes
{
    public static bool IsOrdinaryFoot(int entityType)
    {
        return entityType is >= 0 and <= 9 or 128;
    }

    public static bool IsSupportedFoot(int entityType)
    {
        //546F90's ordinary character shapes have distinct54A2A0 animation identities.
        return entityType is 0 or 1 or 2 or 6;
    }
}
