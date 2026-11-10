namespace Memoria.FF7.Runtime;

public sealed record Ff7NativePartVisibilityState
{
    public byte[] Visibility { get; init; } = [];
    public byte[]? PendingPayload { get; init; }
    public bool Completed => PendingPayload is null;

    public void Validate()
    {
        if (Visibility is not { Length: > 0 and <= 100 } || Visibility.Any(value => value > 1) || PendingPayload is { Length: 0 } || PendingPayload is not null && PendingPayload.Length < 1 + PendingPayload[0] * 2)
        {
            throw new InvalidDataException("The native part-visibility continuation has invalid topology, flags or payload.");
        }
    }
}

public static class Ff7NativePartVisibility
{
    public static Ff7NativePartVisibilityState Request(Ff7NativePartVisibilityState? current, byte[] payload, int partCount)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (partCount is < 1 or > 100)
        {
            throw new NotSupportedException("Native part visibility requires the original bounded rigid-part table.");
        }
        current?.Validate();
        if (current is not null && current.Visibility.Length != partCount)
        {
            throw new InvalidDataException("The saved native part visibility differs from the selected model topology.");
        }
        Ff7NativePartVisibilityState result = new() { Visibility = current?.Visibility ?? Enumerable.Repeat((byte)1, partCount).ToArray(), PendingPayload = (byte[])payload.Clone() };
        result.Validate();
        return result;
    }

    public static Ff7NativePartVisibilityState Advance(Ff7NativePartVisibilityState state, bool eligible, int field)
    {
        state.Validate();
        if (!eligible || state.PendingPayload is null)
        {
            return state;
        }
        // NG-025: the native field profile selects the initial rigid-part mask.
        byte[] values = Enumerable.Repeat(field == 461 ? (byte)0 : (byte)1, state.Visibility.Length).ToArray();
        byte[] payload = state.PendingPayload;
        for (int index = 0; index < payload[0]; index++)
        {
            int part = payload[1 + index * 2];
            if (part < values.Length)
            {
                values[part] = payload[2 + index * 2] != 0 ? (byte)1 : (byte)0;
            }
        }
        return state with { Visibility = values, PendingPayload = null };
    }

    public static bool PartVisible(Ff7NativePartVisibilityState state, int part)
    {
        state.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(part);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(part, state.Visibility.Length);
        return state.Visibility[part] != 0;
    }
}
