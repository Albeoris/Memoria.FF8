namespace Memoria.FF8.Runtime;

internal static class NativeBattlePresentationCpuBanks
{
    internal const int FirstResourceBank = 5;

    public static bool IsExtension(int bankId) => bankId >= FirstResourceBank;
}
