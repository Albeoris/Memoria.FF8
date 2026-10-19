using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot198Resources(NativeBattleEffectHeapAllocation ActorOverride, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleVector> ReferenceAxes, ImmutableArray<uint> EchoColors, uint ShotSound);
public sealed record NativeBattleShot198Anchor(NativeBattleVector Position, NativeBattleVector Direction, NativeBattleActionMatrix Rotation);

/// <summary>Hyper Shot's retained module globals; task links remain in their physical508300 pools.</summary>
public sealed record NativeBattleShot198State([property: JsonRequired] byte Actor, [property: JsonRequired] int Control, [property: JsonRequired] int SequenceCursor, [property: JsonRequired] int ShotSerial, [property: JsonRequired] int ImpactSerial, [property: JsonRequired] int Clock, [property: JsonRequired] int StopClock, [property: JsonRequired] ImmutableArray<NativeBattleShot198Anchor?> Anchors, [property: JsonRequired] ImmutableArray<byte> ParticleStorage, [property: JsonRequired] ImmutableArray<NativeBattleBodyBoneMatrices?> Echoes, [property: JsonRequired] ImmutableArray<bool> EchoActive)
{
    public static NativeBattleShot198State FromColdProcess()
    {
        return new(0, 0, 0, 0, 0, 0, 0, Enumerable.Repeat<NativeBattleShot198Anchor?>(null, 100).ToImmutableArray(), new byte[9760].ToImmutableArray(), Enumerable.Repeat<NativeBattleBodyBoneMatrices?>(null, 5).ToImmutableArray(), Enumerable.Repeat(false, 5).ToImmutableArray());
    }

    public NativeBattleShot198State Enter(byte actor)
    {
        Validate();
        byte[] particles = ParticleStorage.ToArray();
        foreach ((int first, int count, int stride) in new[] { (0, 200, 24), (4832, 200, 24) })
        {
            for (int slot = 0; slot < count; slot++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(first + slot * stride), 0);
            }
        }
        return this with { Actor = actor, Control = 0, SequenceCursor = 0, ShotSerial = 0, ImpactSerial = 0, Clock = 0, ParticleStorage = particles.ToImmutableArray() };
    }

    public void Validate()
    {
        if (Actor >= 7 || SequenceCursor is < 0 or > 2 || Anchors.IsDefault || Anchors.Length != 100 || ParticleStorage.IsDefault || ParticleStorage.Length != 9760 || Echoes.IsDefault || Echoes.Length != 5 || EchoActive.IsDefault || EchoActive.Length != 5 || Enumerable.Range(0, 5).Any(index => EchoActive[index] && Echoes[index] is null))
        {
            throw new InvalidDataException("Native Hyper Shot requires its actual actor, anchor, impact and echo storage.");
        }
        foreach (NativeBattleBodyBoneMatrices? echo in Echoes)
        {
            echo?.Validate();
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShot198State? Shot198 { get; init; }
}
