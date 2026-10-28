using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShot188Resources(NativeBattleEffectHeapAllocation ActorOverride, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleVector> ReferenceAxes, ImmutableArray<uint> EchoColors, uint ShotSound);
public sealed record NativeBattleShot188Anchor(NativeBattleVector Position, NativeBattleActionMatrix Rotation);

/// <summary>Normal Shot's retained module globals; task links remain in their physical508300 pools.</summary>
public sealed record NativeBattleShot188State([property: JsonRequired] byte Actor, [property: JsonRequired] int Control, [property: JsonRequired] int SequenceCursor, [property: JsonRequired] int ShotSerial, [property: JsonRequired] int ImpactSerial, [property: JsonRequired] int Clock, [property: JsonRequired] int StopClock, [property: JsonRequired] ImmutableArray<NativeBattleShot188Anchor?> Anchors, [property: JsonRequired] ImmutableArray<byte> ImpactParticles, [property: JsonRequired] ImmutableArray<NativeBattleBodyBoneMatrices?> Echoes, [property: JsonRequired] ImmutableArray<bool> EchoActive)
{
    public static NativeBattleShot188State FromColdProcess()
    {
        return new(0, 0, 0, 0, 0, 0, 0, Enumerable.Repeat<NativeBattleShot188Anchor?>(null, 100).ToImmutableArray(), new byte[2 * 150 * 24].ToImmutableArray(), Enumerable.Repeat<NativeBattleBodyBoneMatrices?>(null, 5).ToImmutableArray(), Enumerable.Repeat(false, 5).ToImmutableArray());
    }

    public NativeBattleShot188State Enter(byte actor)
    {
        Validate();
        byte[] particles = ImpactParticles.ToArray();
        for (int offset = 0; offset < particles.Length; offset += 24)
        {
            BinaryPrimitives.WriteInt32LittleEndian(particles.AsSpan(offset), 0);
        }
        return this with { Actor = actor, Control = 0, SequenceCursor = 0, ShotSerial = 0, ImpactSerial = 0, Clock = 0, ImpactParticles = particles.ToImmutableArray() };
    }

    public void Validate()
    {
        if (Actor >= 7 || SequenceCursor is < 0 or > 2 || Anchors.IsDefault || Anchors.Length != 100 || ImpactParticles.IsDefault || ImpactParticles.Length != 7200 || Echoes.IsDefault || Echoes.Length != 5 || EchoActive.IsDefault || EchoActive.Length != 5 || Enumerable.Range(0, 5).Any(index => EchoActive[index] && Echoes[index] is null))
        {
            throw new InvalidDataException("Native Normal Shot requires its actual actor, anchor, impact and echo storage.");
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
    public NativeBattleShot188State? Shot188
    {
        get => effectState.Shot188;
        init => effectState = effectState with { Shot188 = value };
    }
}
