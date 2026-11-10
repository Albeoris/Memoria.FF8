namespace Memoria.FF7.Runtime;

/// <summary>The first Tifa reel's native menu position and input latch.</summary>
public readonly record struct Ff7BeatRushReel(short Position, byte InputDelay, bool Stopped)
{
    public static Ff7BeatRushReel Start(short previousPosition = 0) => new(previousPosition, 10, false);

    public Ff7BeatRushReel Advance(bool confirmPressed, bool inputBlocked = false)
    {
        short position = Position;
        if (!Stopped || (position & 3) != 0)
        {
            position = unchecked((short)(position + 1));
        }

        byte delay = InputDelay;
        if (delay != 0)
        {
            delay--;
        }
        return new(position, delay, Stopped || (delay == 0 && confirmPressed && !inputBlocked));
    }

    public Ff7BeatRushSlot ReadVisibleSymbol(IReadOnlyList<byte> symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        if (symbols.Count != 16)
        {
            throw new InvalidDataException("FFVII Beat Rush requires the original sixteen-position reel.");
        }
        byte symbol = symbols[(2 - Position / 4) & 15];
        return symbol <= (byte)Ff7BeatRushSlot.Yeah
            ? (Ff7BeatRushSlot)symbol
            : throw new InvalidDataException($"Invalid FFVII Beat Rush symbol {symbol}.");
    }

    public Ff7BeatRushSlot ReadResult(IReadOnlyList<byte> symbols) => Stopped
        ? ReadVisibleSymbol(symbols)
        : throw new InvalidOperationException("FFVII Beat Rush reel has not stopped.");
}
