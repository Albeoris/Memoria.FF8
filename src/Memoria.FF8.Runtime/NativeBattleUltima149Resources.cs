using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltima149Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Orbit, NativeBattleModuleResource Dust, NativeBattleModuleResource Spark, NativeBattleStaticMeshResource Streak, NativeBattleStaticMeshResource Shard, NativeBattleStaticMeshResource Energy, NativeBattleStaticMeshResource Ring, NativeBattleStaticMeshResource Dome, NativeBattleStaticMeshResource Core, ImmutableArray<byte> CoreShape1, ImmutableArray<int> RingFades, NativeVibrationBank Vibration, NativeBattleVector ReferencePoint, uint StartSound, uint BlastSound);

public sealed record NativeBattleUltima149State([property: JsonRequired] ImmutableArray<byte> ParticlesA, [property: JsonRequired] ImmutableArray<byte> ParticlesB, [property: JsonRequired] ImmutableArray<uint> SideFlags, [property: JsonRequired] ImmutableArray<byte> CoreVertices, [property: JsonRequired] int RingScale, [property: JsonRequired] int VibrationHandle)
{
    public static NativeBattleUltima149State FromColdProcess() => new(new byte[2400].ToImmutableArray(), new byte[2400].ToImmutableArray(), new uint[7].ToImmutableArray(), new byte[1920].ToImmutableArray(), 0, 0);

    public NativeBattleUltima149State Initialize()
    {
        Validate();
        byte[] a = ParticlesA.ToArray();
        byte[] b = ParticlesB.ToArray();
        for (int index = 0; index < 100; index++)
        {
            a.AsSpan(index * 24, 4).Clear();
            b.AsSpan(index * 24, 4).Clear();
        }
        return this with { ParticlesA = a.ToImmutableArray(), ParticlesB = b.ToImmutableArray() };
    }

    public void Validate()
    {
        if (ParticlesA.IsDefault || ParticlesA.Length != 2400 || ParticlesB.IsDefault || ParticlesB.Length != 2400 || SideFlags.IsDefault || SideFlags.Length != 7 || CoreVertices.IsDefault || CoreVertices.Length != 1920)
        {
            throw new InvalidDataException("Native Ultima requires its complete retained particle, side and vertex owners.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltima149State? Ultima149
    {
        get => effectState.Ultima149;
        init => effectState = effectState with { Ultima149 = value };
    }
}
