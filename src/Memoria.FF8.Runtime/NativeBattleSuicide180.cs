using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSuicide180Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleStaticMeshResource Explosion, NativeBattleStaticMeshResource Ring, NativeBattleStaticMeshResource SecondRing, NativeBattleModuleResource Spark, NativeBattleModuleResource Smoke, uint Sound);
public sealed record NativeBattleSuicide180State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] int BufferOffset)
{
    public static NativeBattleSuicide180State FromColdProcess() => new(new byte[2400].ToImmutableArray(), 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSuicide180State? Suicide180 { get; init; }
}
public static class NativeBattleSuicide180
{
    public static int GlowLevel(int counter) => counter < 16 ? unchecked(counter << 8) : 4096;

    public static NativeBattleModulePoolTask AdvanceRing(NativeBattleModulePoolTask task, bool second)
    {
        short scaleSpeed = unchecked((short)task.ReadWord(30));
        if (!second)
        {
            short spinSpeed = unchecked((short)task.ReadWord(26));
            task = task.WriteWord(24, unchecked((ushort)(task.ReadWord(24) + spinSpeed))).WriteWord(26, unchecked((ushort)(spinSpeed - (spinSpeed >> 4))));
        }
        return task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + scaleSpeed))).WriteWord(30, unchecked((ushort)(scaleSpeed - scaleSpeed / 7))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
    }
}
