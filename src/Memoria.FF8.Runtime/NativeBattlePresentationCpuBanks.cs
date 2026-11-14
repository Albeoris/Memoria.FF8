namespace Memoria.FF8.Runtime;

internal static class NativeBattlePresentationCpuBanks
{
    private const int ActorRowOffset = 2;

    public static bool IsExtension(int bankId)
    {
        return bankId is >= NativeBattleSlots.AllyCount + ActorRowOffset and < NativeBattleGraphicsModelAddress.MaximumRowCount + ActorRowOffset;
    }

    public static int FromActorRow(int row)
    {
        return row < NativeBattleSlots.AllyCount ? row : row + ActorRowOffset;
    }

    public static int ToActorRow(int bankId)
    {
        return bankId - ActorRowOffset;
    }
}
