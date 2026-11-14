using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeColorTransitionState
{
    public short[] Current { get; init; } = [0, 0, 0];
    public short[] Target { get; init; } = [0, 0, 0];
    public short[] Step { get; init; } = [0, 0, 0];
    public bool Active { get; init; }
    public bool Finished { get; init; }
    public byte[]? Request { get; init; }
    public bool Pending => Request is not null;

    public void Validate()
    {
        if (Current is not { Length: 3 } || Target is not { Length: 3 } || Step is not { Length: 3 } || Request is { Length: 0 } || Request is { Length: < 21 } && Request[0] == 0)
        {
            throw new InvalidDataException("The native model color transition is invalid.");
        }
    }
}

public static class Ff7NativeColorTransition
{
    public static Ff7NativeColorTransitionState Request(Ff7NativeColorTransitionState? current, ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0 || payload[0] == 0 && payload.Length < 21)
        {
            throw new InvalidDataException("A native model color transition requires its complete original payload.");
        }
        current?.Validate();
        return (current ?? new()) with { Request = payload.ToArray() };
    }

    public static (Ff7NativeColorTransitionState State, short[]? Sample) Advance(Ff7NativeColorTransitionState state, bool eligible)
    {
        state.Validate();
        if (!eligible || state.Request is not byte[] request)
        {
            return (state, null);
        }
        if (request[0] == 0)
        {
            short[] current = new short[3];
            short[] target = new short[3];
            short[] step = new short[3];
            for (int channel = 0; channel < 3; channel++)
            {
                current[channel] = BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(2 + channel * 2));
                target[channel] = BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(8 + channel * 2));
                short delta = BinaryPrimitives.ReadInt16LittleEndian(request.AsSpan(14 + channel * 2));
                step[channel] = target[channel] < 0 && delta > 0 ? unchecked((short)-delta) : delta;
            }
            return (state with { Current = current, Target = target, Step = step, Active = false, Finished = false, Request = null }, null);
        }
        if (!state.Active && request[0] != 1)
        {
            return (state with { Request = null }, null);
        }
        short[] sample = state.Current.ToArray();
        if (state.Finished)
        {
            return (state with { Active = true, Request = null }, sample);
        }
        short[] next = new short[3];
        bool finished = true;
        for (int channel = 0; channel < 3; channel++)
        {
            short value = unchecked((short)(state.Current[channel] + state.Step[channel]));
            bool reached = state.Step[channel] >= 0 ? value >= state.Target[channel] : value <= state.Target[channel];
            next[channel] = reached ? state.Target[channel] : value;
            finished &= reached;
        }
        return (state with { Current = next, Active = true, Finished = finished }, sample);
    }
}
