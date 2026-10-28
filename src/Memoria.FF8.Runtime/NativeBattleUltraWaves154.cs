using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWaves154Resources(NativeBattleStaticMeshResource Ring, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<uint> WarpColors, ImmutableArray<uint> RingColors, ImmutableArray<NativeBattleVector> EmitterVectors, ImmutableArray<int> StageDraw, ImmutableArray<byte> CapturePackets);
public sealed record NativeBattleUltraWaves154State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] ImmutableArray<byte> Grid, [property: JsonRequired] NativeBattleEffectArena Quads, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena CapturePackets)
{
    public bool? ColdCaptureSourcesPending { get; init; }
    public NativeBattleEffectArena? CaptureOffsets { get; init; }
    public static NativeBattleUltraWaves154State FromColdProcess() => new(new byte[2400].ToImmutableArray(), new byte[28 * 41 * 12].ToImmutableArray(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(0, 48)) { ColdCaptureSourcesPending = true, CaptureOffsets = NativeBattleEffectArena.Clear() };

    public NativeBattleUltraWaves154State SeedColdCapture(NativeBattleUltraWaves154Resources resources)
    {
        if (ColdCaptureSourcesPending is null)
        {
            throw new NotSupportedException("Ultra Waves requires an explicitly observed mutable capture packet source.");
        }
        if (ColdCaptureSourcesPending == false)
        {
            return this;
        }
        if (resources.CapturePackets.IsDefault || resources.CapturePackets.Length != 48)
        {
            throw new InvalidDataException("Ultra Waves requires its original two capture packets.");
        }
        return this with { CapturePackets = CapturePackets.Write(0, resources.CapturePackets.AsSpan()), ColdCaptureSourcesPending = false };
    }
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltraWaves154State? UltraWaves154
    {
        get => effectState.UltraWaves154;
        init => effectState = effectState with { UltraWaves154 = value };
    }
}
