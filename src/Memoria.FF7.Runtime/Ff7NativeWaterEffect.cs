using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeWaterEffectPose(int ModelIndex, int AnimationIndex, int AnimationFrame16, byte Direction, float X, float Y, float Z);

public sealed record Ff7NativeWaterEffectSample(Ff7FieldWaterParameters Parameters, Ff7FieldWaterPartMask[] Parts, short CycleIndex, short SubcycleIndex)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7NativeWaterEffectPose? SamplingPose { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7FieldWaterColorRegion? ColorRegion { get; init; }
}

public sealed record Ff7NativeWaterSplash(int ModelSlot, int BoneIndex, int TextureIndex, float X, float Y, float Depth, float HalfSize);

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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7NativeWaterEffectSample? DisplayedSample { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OwnsColors { get; init; }

    public void Validate()
    {
        if (RequestedPayload is not { Length: > 0 } || RequestedPayload[0] > 1 || RequestedPayload[0] == 0 && RequestedPayload.Length < 11 || ModelSlot is < 0 or >= byte.MaxValue || CycleIndex is < 0 or > 3 || SubcycleIndex is < 0 or > 2 || !Initialized && (CycleIndex != 0 || SubcycleIndex != 0) || OwnsColors && DisplayedSample is null)
        {
            throw new InvalidDataException("FFVII water-effect continuation has invalid parameters, model ownership or counters.");
        }
        if (DisplayedSample is { } sample && (sample.Parameters is null || sample.Parts is null || sample.Parts.Any(part => part is null || part.Part < 0 || part.ModifiedVertices is null) || sample.Parts.Select(part => part.Part).Distinct().Count() != sample.Parts.Length || sample.CycleIndex is < 0 or > 3 || sample.SubcycleIndex is < 0 or > 2))
        {
            throw new InvalidDataException("FFVII water-effect color sample has invalid topology or counters.");
        }
        if (DisplayedSample?.SamplingPose is { } pose && (pose.ModelIndex < 0 || pose.AnimationIndex < 0 || pose.AnimationFrame16 < 0 || (pose.AnimationFrame16 & 15) != 0 || !float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Z)))
        {
            throw new InvalidDataException("FFVII water-effect pose metadata has invalid model, animation or coordinates.");
        }
        DisplayedSample?.ColorRegion?.Validate();
    }
}

public static class Ff7NativeWaterEffect
{
    public static int EnvironmentProfile(ushort field, Ff7Actor actor, PcFieldCamera camera)
    {
        Vector3 point = new(unchecked((short)(actor.X4096 >> 12)), unchecked((short)(actor.Y4096 >> 12)), unchecked((short)((actor.Z4096 >> 12) - 10)));
        Vector3 projected = PcFieldProjection.Project(point, camera, Vector2.Zero);
        short x = unchecked((short)(int)projected.X);
        short y = unchecked((short)(int)projected.Y);
        // NG-089: Preserve the original map-dependent water environment selection.
        return field switch
        {
            88 when x >= 30 => 2,
            429 when y >= -65 => 3,
            430 => 4,
            435 => 5,
            449 when y >= 16 => 1,
            641 => 6,
            _ => 0
        };
    }

    public static Ff7NativeWaterEffectState Request(Ff7NativeWaterEffectState? current, ReadOnlySpan<byte> payload, int modelSlot)
    {
        if (payload.Length == 0)
        {
            throw new InvalidDataException("FFVII water effects require their native payload.");
        }
        if (payload[0] > 1)
        {
            throw new NotSupportedException("FFVII water-effect modes beyond initialization and application require their native consumers.");
        }
        if (payload[0] == 0 && payload.Length < 11 || modelSlot is < 0 or >= byte.MaxValue)
        {
            throw new InvalidDataException("FFVII mode0 water effects require four parameter words, the flag byte and a physical model slot.");
        }
        current?.Validate();
        if (current is not null && current.ModelSlot != modelSlot)
        {
            throw new InvalidDataException("The FFVII water-effect cache belongs to another physical model.");
        }
        return (current ?? new()) with { RequestedPayload = payload.ToArray(), ModelSlot = modelSlot, Active = true, Initialized = payload[0] == 1 || current?.Initialized == true };
    }

    public static Ff7NativeWaterEffectState Advance(Ff7NativeWaterEffectState state, bool eligible, Ff7NativeWaterEffectSample? sample = null)
    {
        state.Validate();
        if (!state.Active || !eligible)
        {
            return state;
        }
        ReadOnlySpan<byte> payload = state.RequestedPayload;
        if (payload[0] == 1)
        {
            if (sample is null || sample.CycleIndex != state.CycleIndex || sample.SubcycleIndex != state.SubcycleIndex || sample.Parameters.Level != state.Parameter0 || sample.Parameters.Red != state.Parameter1 || sample.Parameters.Green != state.Parameter2 || sample.Parameters.Blue != state.Parameter3 || sample.Parameters.Flags != state.Flags)
            {
                throw new InvalidDataException("FFVII water-effect application requires its exact accepted-callback color sample.");
            }
            short subcycle = (short)(state.SubcycleIndex + 1);
            short cycle = state.CycleIndex;
            if (subcycle >= 3)
            {
                subcycle = 0;
                cycle = (short)((cycle + 1) % 4);
            }
            Ff7NativeWaterEffectState applied = state with { DisplayedSample = sample, OwnsColors = true, CycleIndex = cycle, SubcycleIndex = subcycle };
            applied.Validate();
            return applied;
        }
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
