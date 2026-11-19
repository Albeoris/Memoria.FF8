using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeCustomLightingSample(byte[] Colors, short[] Directions)
{
    public void Validate()
    {
        if (Colors is not { Length: 12 } || Directions is not { Length: 9 })
        {
            throw new InvalidDataException("Native custom lighting requires four RGB triples and nine signed direction words.");
        }
    }
}

public sealed record Ff7NativeCustomLightingState
{
    public int ModelSlot { get; init; }
    public byte[] RequestedPayload { get; init; } = [];
    public bool Pending { get; init; }
    public bool Enabled { get; init; }
    public Ff7NativeCustomLightingSample? Cached { get; init; }
    public Ff7NativeCustomLightingSample? Displayed { get; init; }

    public void Validate()
    {
        if (ModelSlot is < 0 or >= byte.MaxValue || RequestedPayload is not { Length: > 0 } || RequestedPayload[0] == 0 && RequestedPayload.Length < 32 || Enabled && Cached is null || Displayed is not null && Cached is null)
        {
            throw new InvalidDataException("Native custom lighting has an invalid physical owner, payload or cache.");
        }
        Cached?.Validate();
        Displayed?.Validate();
    }
}

public static class Ff7NativeCustomLighting
{
    public static Ff7NativeCustomLightingState Request(Ff7NativeCustomLightingState? current, ReadOnlySpan<byte> payload, int modelSlot)
    {
        if (modelSlot is < 0 or >= byte.MaxValue || payload.Length == 0 || payload[0] == 0 && payload.Length < 32)
        {
            throw new InvalidDataException("Native custom lighting requires its original payload and physical model owner.");
        }
        current?.Validate();
        if (current is not null && current.ModelSlot != modelSlot)
        {
            throw new InvalidDataException("Native custom lighting belongs to another physical model.");
        }
        return (current ?? new()) with { ModelSlot = modelSlot, RequestedPayload = payload.ToArray(), Pending = true };
    }

    public static Ff7NativeCustomLightingState Advance(Ff7NativeCustomLightingState state, bool eligible)
    {
        state.Validate();
        if (!eligible || !state.Pending)
        {
            return state;
        }
        if (state.RequestedPayload[0] == 0)
        {
            byte[] colors = state.RequestedPayload.AsSpan(2, 12).ToArray();
            short[] directions = new short[9];
            for (int index = 0; index < directions.Length; index++)
            {
                directions[index] = BinaryPrimitives.ReadInt16LittleEndian(state.RequestedPayload.AsSpan(14 + index * 2));
            }
            return state with { Cached = new(colors, directions), Enabled = false, Pending = false };
        }
        if (state.Enabled || state.RequestedPayload[0] == 1)
        {
            Ff7NativeCustomLightingSample cached = state.Cached ?? throw new NotSupportedException("Native custom lighting application requires its initialized physical-model cache.");
            return state with { Enabled = true, Displayed = new(cached.Colors.ToArray(), cached.Directions.ToArray()) };
        }
        return state with { Pending = false };
    }
}
