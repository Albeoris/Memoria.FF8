using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWaves154Resources(NativeBattleStaticMeshResource Ring, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<uint> WarpColors, ImmutableArray<uint> RingColors, ImmutableArray<NativeBattleVector> EmitterVectors, ImmutableArray<int> StageDraw, ImmutableArray<byte> CapturePackets);
public sealed record NativeBattleUltraWaves154State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] ImmutableArray<byte> Grid, [property: JsonRequired] NativeBattleEffectArena Quads, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] NativeBattleEffectArena CapturePackets)
{
    public static NativeBattleUltraWaves154State FromColdProcess() => new(new byte[2400].ToImmutableArray(), new byte[28 * 41 * 12].ToImmutableArray(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltraWaves154State? UltraWaves154 { get; init; }
}
