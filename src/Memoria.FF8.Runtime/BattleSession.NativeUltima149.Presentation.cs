namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleUltima149ParticleVisualLifetimes
{
    private readonly long[] generations = new long[200];
    private long nextGeneration;

    internal void Allocated(bool bankA, int slot)
    {
        generations[Row(bankA, slot)] = ++nextGeneration;
    }

    internal NativeBattlePilotVisualIdentity Identity(NativeBattlePilotVisualIdentity task, bool bankA, int slot)
    {
        int row = Row(bankA, slot);
        return task with { Primitive = row, ResourceGeneration = generations[row] };
    }

    private static int Row(bool bankA, int slot)
    {
        if ((uint)slot >= 100)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return (bankA ? 100 : 0) + slot;
    }
}
