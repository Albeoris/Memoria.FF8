using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBombGlow179Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Spark, uint Sound, ImmutableArray<uint> Colors);
public sealed record NativeBattleBombGlow179State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] NativeBattleEffectArena Packets)
{
    public static NativeBattleBombGlow179State FromColdProcess() => new(new byte[2400].ToImmutableArray(), NativeBattleEffectArena.Clear());
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBombGlow179State? BombGlow179
    {
        get => effectState.BombGlow179;
        init => effectState = effectState with { BombGlow179 = value };
    }
}
