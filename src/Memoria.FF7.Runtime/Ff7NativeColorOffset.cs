using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeColorOffsetState
{
    public short Red { get; init; }
    public short Green { get; init; }
    public short Blue { get; init; }
    public short RequestedRed { get; init; }
    public short RequestedGreen { get; init; }
    public short RequestedBlue { get; init; }
    public bool Pending { get; init; }

    public void Validate()
    {
        if (Red is < -255 or > 255 || Green is < -255 or > 255 || Blue is < -255 or > 255)
        {
            throw new InvalidDataException("The native model color offset is invalid.");
        }
    }
}

public static class Ff7NativeColorOffset
{
    public static Ff7NativeColorOffsetState Request(Ff7NativeColorOffsetState? current, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 6)
        {
            throw new InvalidDataException("Native model color offsets require three signed little-endian words.");
        }
        current?.Validate();
        return (current ?? new()) with
        {
            RequestedRed = BinaryPrimitives.ReadInt16LittleEndian(payload),
            RequestedGreen = BinaryPrimitives.ReadInt16LittleEndian(payload[2..]),
            RequestedBlue = BinaryPrimitives.ReadInt16LittleEndian(payload[4..]),
            Pending = true
        };
    }

    public static Ff7NativeColorOffsetState Advance(Ff7NativeColorOffsetState state, bool eligible)
    {
        state.Validate();
        if (!eligible || !state.Pending)
        {
            return state;
        }
        if (state.RequestedRed is < -255 or > 255 || state.RequestedGreen is < -255 or > 255 || state.RequestedBlue is < -255 or > 255)
        {
            return state with { Pending = false };
        }
        return state with { Red = state.RequestedRed, Green = state.RequestedGreen, Blue = state.RequestedBlue, Pending = false };
    }

    public static uint Shade(Ff7NativeColorOffsetState state, uint original)
    {
        state.Validate();
        uint red = (uint)Math.Clamp((int)((original >> 16) & 255) + state.Red, 0, 255);
        uint green = (uint)Math.Clamp((int)((original >> 8) & 255) + state.Green, 0, 255);
        uint blue = (uint)Math.Clamp((int)(original & 255) + state.Blue, 0, 255);
        return (original & 0xFF000000) | (red << 16) | (green << 8) | blue;
    }
}
