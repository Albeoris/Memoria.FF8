using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldForestEffect(byte Type, ushort Shrink, byte Lifetime, byte Frames, byte FrameDuration, short HalfSize);
public sealed record NativeWorldForestParticle([property: JsonRequired] int Slot, [property: JsonRequired] byte Type, [property: JsonRequired] int X, [property: JsonRequired] int NativeY, [property: JsonRequired] int GraphicsZ, [property: JsonRequired] short Yaw, [property: JsonRequired] short Roll, [property: JsonRequired] ushort Scale, [property: JsonRequired] byte Age, [property: JsonRequired] byte Lifetime, [property: JsonRequired] bool Visible);

/// <summary>Normal-foot forest branch of550070, first-free64-slot5562B0 pool, and555B80 update.</summary>
public sealed record NativeWorldForestState
{
    [JsonRequired]
    public required uint SeedCounter { get; init; }
    [JsonRequired]
    public required byte RandomCursor { get; init; }
    [JsonRequired]
    public required byte RandomOffset { get; init; }
    [JsonRequired]
    public required short CameraPitch { get; init; }
    [JsonRequired]
    public required int CameraCooldown { get; init; }
    [JsonRequired]
    public required NativeWorldForestParticle[] Particles { get; init; }

    public static NativeWorldForestState Create(uint counter, int cooldown)
    {
        //53F310 samples4022C0 for cursor/offset;
        //557250 sets pitch-112;555B20 clears all particle lifetimes/ages.
        return new NativeWorldForestState { SeedCounter = counter, RandomCursor = unchecked((byte)counter), RandomOffset = unchecked((byte)counter), CameraPitch = -112, CameraCooldown = cooldown, Particles = [] };
    }

    public void Validate()
    {
        if (CameraPitch is < -512 or > 0 || Particles is null || Particles.Length > 64 || Particles.Select(particle => particle.Slot).Distinct().Count() != Particles.Length || Particles.Any(particle => particle.Slot is < 0 or >= 64 || particle.Type is < 4 or > 9 || particle.Lifetime is < 4 or > 11 || particle.Age > particle.Lifetime))
        {
            throw new InvalidDataException("Invalid executed native forest presentation state.");
        }
    }

    public NativeWorldForestState Advance(byte groundType, int x, int nativeHeight, int rawZ, int bodyYaw, int priorGraphicsSpeed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (random.Length != 256 || effects.Count != 6 || priorGraphicsSpeed is < 0 or > 46340)
        {
            throw new InvalidDataException("Native forest update requires its complete original random/effect tables.");
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        if (groundType <= 5 && 8 * priorGraphicsSpeed > NextRandom(random, ref cursor, ref offset))
        {
            short yaw = unchecked((short)(bodyYaw - 2048 + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
            short roll = unchecked((short)(4 * NextRandom(random, ref cursor, ref offset) - 512));
            int slot = Enumerable.Range(0, 64).FirstOrDefault(candidate => !particles.Any(particle => particle.Slot == candidate && particle.Age < particle.Lifetime), -1);
            if (slot >= 0)
            {
                NativeWorldForestEffect effect = effects[groundType];
                ushort scale = unchecked((ushort)(4096 + NextRandom(random, ref cursor, ref offset) - 128));
                byte lifetime = unchecked((byte)(effect.Lifetime + (NextRandom(random, ref cursor, ref offset) & 7) - 4));
                particles.RemoveAll(particle => particle.Slot == slot);
                particles.Add(new(slot, effect.Type, x, nativeHeight - 320, -rawZ, yaw, roll, scale, 0, lifetime, true));
            }
        }
        NativeWorldForestParticle[] advanced = particles.OrderBy(particle => particle.Slot).Select(particle => particle.Age < particle.Lifetime ? particle with { Scale = unchecked((ushort)(particle.Scale * effects[particle.Type - 4].Shrink / 4096)), Age = unchecked((byte)(particle.Age + 1)), Visible = true } : particle with { Visible = false }).ToArray();
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = advanced };
    }

    private static byte NextRandom(ReadOnlySpan<byte> table, ref byte cursor, ref byte offset)
    {
        //541F10 increments BEFORE indexing and increases offset13 on byte wrap.
        cursor = unchecked((byte)(cursor + 1));
        if (cursor == 0)
        {
            offset = unchecked((byte)(offset + 13));
        }
        return unchecked((byte)(table[cursor] - offset));
    }
}
