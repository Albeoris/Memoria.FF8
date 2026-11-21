namespace Memoria.FF7.Formats;

public sealed record BattleActorUvAnimationState(byte Generation, sbyte Cursor = 0, ushort Delay = 0);
public sealed record BattleActorUvAnimationUpdate(BattleActorUvAnimationState? State, ushort? PackedUv = null);

public static class BattleActorUvAnimation
{
    public static BattleActorUvAnimationUpdate Advance(BattleActorUvAnimationState state, byte currentGeneration, ReadOnlyMemory<byte> allocation, int startOffset)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Generation != currentGeneration)
        {
            return new(null);
        }
        if (state.Delay > 0)
        {
            return new(state with { Delay = (ushort)(state.Delay - 1) });
        }
        HashSet<sbyte> visited = [];
        sbyte cursor = state.Cursor;
        byte Read()
        {
            long address = (long)startOffset + cursor;
            if (address < 0 || address >= allocation.Length)
            {
                throw new InvalidDataException("The native actor UV animation cursor lies outside its allocation.");
            }
            byte value = allocation.Span[(int)address];
            cursor = unchecked((sbyte)(cursor + 1));
            return value;
        }
        while (visited.Add(cursor))
        {
            byte command = Read();
            if (command == 0xFD)
            {
                byte u = Read();
                byte v = Read();
                byte delay = Read();
                return new(state with { Cursor = cursor, Delay = delay }, (ushort)(u | v << 8));
            }
            if (command == 0xFE)
            {
                cursor = 0;
                continue;
            }
            if (command == 0xFF)
            {
                return new(null);
            }
            return new(state with { Cursor = cursor });
        }
        throw new InvalidDataException("The native actor UV animation repeats without yielding.");
    }
}
