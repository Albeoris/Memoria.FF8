namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeTransparencyState
{
    public byte Mode { get; init; }
    public byte[]? Request { get; init; }
    public bool Pending => Request is not null;

    public void Validate()
    {
        if (Request is { Length: 0 })
        {
            throw new InvalidDataException("Native model transparency requires its original mode byte.");
        }
    }
}

public static class Ff7NativeTransparency
{
    public static Ff7NativeTransparencyState Request(Ff7NativeTransparencyState? current, ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0)
        {
            throw new InvalidDataException("Native model transparency requires its original mode byte.");
        }
        current?.Validate();
        return (current ?? new()) with { Request = payload.ToArray() };
    }

    public static Ff7NativeTransparencyState Advance(Ff7NativeTransparencyState state, bool eligible)
    {
        state.Validate();
        return eligible && state.Request is { } request ? state with { Mode = request[0], Request = null } : state;
    }
}
