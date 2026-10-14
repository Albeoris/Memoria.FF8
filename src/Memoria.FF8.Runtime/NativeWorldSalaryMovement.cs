namespace Memoria.FF8.Runtime;

public static class NativeWorldSalaryMovement
{
    public static uint AcceptedVectorUnits(int x, int y, int z)
    {
        return (uint)(NativeWorldVectorMagnitude.Truncate(x, y, z) / 8);
    }
}
