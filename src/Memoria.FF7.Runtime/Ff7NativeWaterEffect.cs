using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeWaterEffectState
{
    public byte[] RequestedPayload { get; init; } = [];
    public int ModelSlot { get; init; }
    public bool Active { get; init; }
    public bool Initialized { get; init; }
    public short Parameter0 { get; init; }
    public short Parameter1 { get; init; }
    public short Parameter2 { get; init; }
    public short Parameter3 { get; init; }
    public byte Flags { get; init; }
    public short CycleIndex { get; init; }
    public short SubcycleIndex { get; init; }

    public void Validate()
    {
        if (RequestedPayload is not { Length: >= 11 } || RequestedPayload[0] != 0 || ModelSlot is < 0 or >= byte.MaxValue || CycleIndex != 0 || SubcycleIndex != (Initialized ? 1 : 0))
        {
            throw new InvalidDataException("FFVII mode0 water-effect continuation has invalid parameters, model ownership or counters.");
        }
    }
}

public static class Ff7NativeWaterEffect
{
    public static Ff7NativeWaterEffectState Request(Ff7NativeWaterEffectState? current, ReadOnlySpan<byte> payload, int modelSlot)
    {
        if (payload.Length == 0)
        {
            throw new InvalidDataException("FFVII water effects require their native payload.");
        }
        if (payload[0] != 0)
        {
            throw new NotSupportedException("FFVII water-effect modes beyond cache initialization require their native model/material consumers.");
        }
        if (payload.Length < 11 || modelSlot is < 0 or >= byte.MaxValue)
        {
            throw new InvalidDataException("FFVII mode0 water effects require four parameter words, the flag byte and a physical model slot.");
        }
        current?.Validate();
        if (current is not null && current.ModelSlot != modelSlot)
        {
            throw new InvalidDataException("The FFVII water-effect cache belongs to another physical model.");
        }
        return (current ?? new()) with { RequestedPayload = payload.ToArray(), ModelSlot = modelSlot, Active = true };
    }

    public static Ff7NativeWaterEffectState Advance(Ff7NativeWaterEffectState state, bool eligible)
    {
        state.Validate();
        if (!state.Active || !eligible)
        {
            return state;
        }
        ReadOnlySpan<byte> payload = state.RequestedPayload;
        return state with
        {
            Initialized = true,
            Parameter0 = BinaryPrimitives.ReadInt16LittleEndian(payload[2..]),
            Parameter1 = BinaryPrimitives.ReadInt16LittleEndian(payload[4..]),
            Parameter2 = BinaryPrimitives.ReadInt16LittleEndian(payload[6..]),
            Parameter3 = BinaryPrimitives.ReadInt16LittleEndian(payload[8..]),
            Flags = payload[10],
            CycleIndex = 0,
            SubcycleIndex = 1
        };
    }
}
