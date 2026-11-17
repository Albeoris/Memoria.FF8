namespace Memoria.FF8.Runtime;

public readonly record struct BattleActionHeaderReference
{
    [System.Text.Json.Serialization.JsonConstructor]
    public BattleActionHeaderReference(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        Ordinal = ordinal;
    }

    public int Ordinal { get; }

    public BattleActionHeaderReference Advance(int offset) => new(checked(Ordinal + offset));
}

public readonly record struct BattleContactCursor
{
    public BattleContactCursor(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        Ordinal = ordinal;
    }

    internal int Ordinal { get; }

    public BattleContactCursor Advance(int offset) => new(checked(Ordinal + offset));
}

public readonly record struct BattleContactOccurrence
{
    [System.Text.Json.Serialization.JsonConstructor]
    public BattleContactOccurrence(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        Ordinal = ordinal;
    }

    public int Ordinal { get; }

    public BattleContactCursor AsCursor() => new(Ordinal);
}
