using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeWorldForestEffect(byte Type, ushort Shrink, byte Lifetime, byte Frames, byte FrameDuration, short HalfSize)
{
    public ushort TextureX { get; init; }
    public ushort TextureY { get; init; }
    public ushort PaletteX { get; init; }
    public ushort PaletteY { get; init; }
    public byte Width { get; init; }
    public byte Height { get; init; }
    public byte DrawMode { get; init; }
    public byte BlendMode { get; init; }
    public byte Red { get; init; }
    public byte Green { get; init; }
    public byte Blue { get; init; }
    public short VelocityX { get; init; }
    public short VelocityY { get; init; }
    public short VelocityZ { get; init; }
    public short AccelerationX { get; init; }
    public short AccelerationY { get; init; }
    public short AccelerationZ { get; init; }
}

public sealed record NativeWorldForestParticleMotion([property: JsonRequired] short RotationX, [property: JsonRequired] short VelocityX, [property: JsonRequired] short VelocityY, [property: JsonRequired] short VelocityZ);

public sealed record NativeWorldForestParticle([property: JsonRequired] int Slot, [property: JsonRequired] byte Type, [property: JsonRequired] int X, [property: JsonRequired] int NativeY, [property: JsonRequired] int GraphicsZ, [property: JsonRequired] short Yaw, [property: JsonRequired] short Roll, [property: JsonRequired] ushort Scale, [property: JsonRequired] byte Age, [property: JsonRequired] byte Lifetime, [property: JsonRequired] bool Visible)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldForestParticleMotion? Motion { get; init; }
    [JsonIgnore]
    public short RotationX => Motion?.RotationX ?? 0;
    [JsonIgnore]
    public short VelocityX => Motion?.VelocityX ?? 0;
    [JsonIgnore]
    public short VelocityY => Motion?.VelocityY ?? 0;
    [JsonIgnore]
    public short VelocityZ => Motion?.VelocityZ ?? 0;
}

/// <summary>Shared first-free64-slot5562B0/555B80 pool, normal forest and Garden wake550070, and late Cactuar556DE0 emission.</summary>
public sealed partial record NativeWorldForestState
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

    public static bool ContentEquals(NativeWorldForestState? left, NativeWorldForestState? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        if (left.SeedCounter != right.SeedCounter || left.RandomCursor != right.RandomCursor || left.RandomOffset != right.RandomOffset || left.CameraPitch != right.CameraPitch || left.CameraCooldown != right.CameraCooldown)
        {
            return false;
        }
        if (left.Particles is null || right.Particles is null)
        {
            return left.Particles is null && right.Particles is null;
        }
        return left.Particles.SequenceEqual(right.Particles);
    }

    public static NativeWorldForestState Create(uint counter, int cooldown)
    {
        //53F310 samples4022C0 for cursor/offset;
        //557250 sets pitch-112;555B20 clears all particle lifetimes/ages.
        return new NativeWorldForestState { SeedCounter = counter, RandomCursor = unchecked((byte)counter), RandomOffset = unchecked((byte)counter), CameraPitch = -112, CameraCooldown = cooldown, Particles = [] };
    }

    public void Validate()
    {
        if (CameraPitch is < -512 or > 0 || Particles is null || Particles.Length > 64 || Particles.Select(particle => particle.Slot).Distinct().Count() != Particles.Length || Particles.Any(particle => particle.Slot is < 0 or >= 64 || particle.Type is not (0 or 2 or 3 or >= 4 and <= 10 or 12 or 13 or 14 or 15 or 16 or 19 or 20 or 21) || particle.Lifetime < MinimumLifetime(particle.Type) || particle.Lifetime > MaximumLifetime(particle.Type) || particle.Age > particle.Lifetime || (particle.Type is 0 or 2 or 3 or 10 or 12 or 13 or 14 or 15 or 16 or 19 or 20 or 21) != (particle.Motion is not null)))
        {
            throw new InvalidDataException("Invalid executed native forest presentation state.");
        }
    }

    private static int MinimumLifetime(byte type) => type switch
    {
        0 or 10 => 12,
        2 => 6,
        3 => 2,
        12 or 13 or 14 or 15 or 20 => 8,
        16 => 32,
        19 => 14,
        21 => 12,
        _ => 4
    };

    private static int MaximumLifetime(byte type) => type switch
    {
        0 or 10 => 19,
        2 => 13,
        3 => 9,
        12 or 13 => 8,
        14 or 15 or 20 => 15,
        16 => 32,
        19 => 21,
        21 => 12,
        _ => 11
    };

    public NativeWorldForestState Advance(byte groundType, int x, int nativeHeight, int rawZ, int bodyYaw, int priorGraphicsSpeed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects, NativeWorldContactSpark? contactSpark = null)
    {
        Validate();
        if (random.Length != 256 || effects.Count is not (6 or 7 or 11 or 18 or 21) || priorGraphicsSpeed is < 0 or > 46340)
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
        if (contactSpark is not null)
        {
            EmitContactSpark(particles, contactSpark, random, effects, ref cursor, ref offset);
        }
        NativeWorldForestParticle[] advanced = AdvanceParticles(particles, effects);
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = advanced };
    }

    public NativeWorldForestState AdvanceGarden(byte groundType, NativeGardenWorldObject garden, int groundHeight, int priorGraphicsSpeed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (garden.ShapeId != 64 || random.Length != 256 || effects.Count is not (7 or 11 or 18) || effects[6].Type != 10 || priorGraphicsSpeed is < 0 or > 46340)
        {
            throw new InvalidDataException("Garden wake update requires its physical shape64, canonical prior speed and authenticated complete effect tables.");
        }
        if (groundType == 8)
        {
            NativeWorldForestState emitted = EmitVehicleDust(garden.Position, garden.RotationX, garden.RotationY, garden.RotationZ, groundHeight, priorGraphicsSpeed, random, effects);
            return emitted with { Particles = AdvanceParticles(emitted.Particles, effects) };
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        if (groundType is >= 30 and <= 34 or 10 or 11)
        {
            int difference = unchecked(garden.Position.Y - groundHeight);
            int threshold = unchecked(Math.Min(difference, 0) + 2 * priorGraphicsSpeed + 128);
            if (threshold > NextRandom(random, ref cursor, ref offset))
            {
                short yaw = unchecked((short)(garden.RotationY + 16 * (NextRandom(random, ref cursor, ref offset) - 128)));
                int slot = Enumerable.Range(0, 64).FirstOrDefault(candidate => !particles.Any(particle => particle.Slot == candidate && particle.Age < particle.Lifetime), -1);
                if (slot >= 0)
                {
                    NativeWorldForestEffect effect = effects[6];
                    ushort scale = unchecked((ushort)(4096 + NextRandom(random, ref cursor, ref offset) - 128));
                    byte lifetime = unchecked((byte)(effect.Lifetime + (NextRandom(random, ref cursor, ref offset) & 7) - 4));
                    (short vx, short vy, short vz) = RotateParticleVelocity(garden.RotationX, yaw, garden.RotationZ, effect.VelocityX, effect.VelocityY, effect.VelocityZ);
                    particles.RemoveAll(particle => particle.Slot == slot);
                    particles.Add(new(slot, 10, garden.Position.X, groundHeight, garden.Position.Z, yaw, garden.RotationZ, scale, 0, lifetime, true) { Motion = new(garden.RotationX, vx, vy, vz) });
                }
                //550070 draws this byte even when5562B0 found no available pool slot.
                _ = NextRandom(random, ref cursor, ref offset);
            }
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = AdvanceParticles(particles, effects) };
    }

    public NativeWorldForestState EmitCactuarBurst(NativeWorldPathPosition position, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (random.Length != 256 || effects.Count is not (11 or 18) || effects[10].Type != 14)
        {
            throw new InvalidDataException("Cactuar556DE0 requires its actual position and complete original random/effect tables.");
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        if (NextRandom(random, ref cursor, ref offset) < 128)
        {
            int slot = Enumerable.Range(0, 64).FirstOrDefault(candidate => !particles.Any(particle => particle.Slot == candidate && particle.Age < particle.Lifetime), -1);
            if (slot >= 0)
            {
                NativeWorldForestEffect effect = effects[10];
                ushort scale = unchecked((ushort)(4096 + NextRandom(random, ref cursor, ref offset) - 128));
                byte lifetime = unchecked((byte)(effect.Lifetime + (NextRandom(random, ref cursor, ref offset) & 7) - 4));
                particles.RemoveAll(particle => particle.Slot == slot);
                // 556DE0 runs after555B80: this newly emitted slot is first
                // advanced and drawn by the following ordinary pool callback.
                particles.Add(new(slot, 14, position.X, position.Y, position.Z, 0, 0, scale, 0, lifetime, false) { Motion = new(0, effect.VelocityX, effect.VelocityY, effect.VelocityZ) });
            }
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = particles.OrderBy(particle => particle.Slot).ToArray() };
    }

    private static NativeWorldForestParticle[] AdvanceParticles(IEnumerable<NativeWorldForestParticle> particles, IReadOnlyList<NativeWorldForestEffect> effects, NativeWorldPathPosition? follow = null)
    {
        return particles.OrderBy(particle => particle.Slot).Select(particle =>
        {
            if (particle.Age >= particle.Lifetime)
            {
                return particle with { Visible = false };
            }
            NativeWorldForestEffect? effect = effects.FirstOrDefault(effect => effect.Type == particle.Type);
            if (effect is null)
            {
                throw new InvalidDataException("Live world particle lost its original effect record.");
            }
            NativeWorldPathPosition displacement = new(0, 0, 0);
            if (particle.Type is 12 or 13 && particle.Age != 0)
            {
                displacement = follow ?? throw new NotSupportedException("Live thrust particles require the actual current movement and preceding native height owner.");
            }
            return particle with
            {
                Scale = unchecked((ushort)(particle.Scale * effect.Shrink / 4096)),
                X = unchecked(particle.X + particle.VelocityX + 5 * (short)displacement.X / 6),
                NativeY = unchecked(particle.NativeY + particle.VelocityY + 5 * displacement.Y / 6),
                GraphicsZ = unchecked(particle.GraphicsZ + particle.VelocityZ + 5 * (short)displacement.Z / 6),
                Motion = particle.Motion is null ? null : new(particle.RotationX, unchecked((short)(particle.VelocityX + effect.AccelerationX)), unchecked((short)(particle.VelocityY + effect.AccelerationY)), unchecked((short)(particle.VelocityZ + effect.AccelerationZ))),
                Age = unchecked((byte)(particle.Age + 1)),
                Visible = true
            };
        }).ToArray();
    }

    private static (short X, short Y, short Z) RotateParticleVelocity(short pitch, short yaw, short roll, short x, short y, short z)
    {
        (int sx, int cx) = NativeGardenTransform.Offset(4096, pitch);
        (int sy, int cy) = NativeGardenTransform.Offset(4096, yaw);
        (int sz, int cz) = NativeGardenTransform.Offset(4096, roll);
        short[] rotateX = [4096, 0, 0, 0, (short)cx, (short)-sx, 0, (short)sx, (short)cx];
        short[] rotateY = [(short)cy, 0, (short)sy, 0, 4096, 0, (short)-sy, 0, (short)cy];
        short[] rotateZ = [(short)cz, (short)-sz, 0, (short)sz, (short)cz, 0, 0, 0, 4096];
        short[] matrix = MultiplyWakeRotation(rotateX, MultiplyWakeRotation(rotateY, rotateZ));
        return (Component(0), Component(3), Component(6));

        short Component(int row)
        {
            int value = checked((int)Math.Round(((long)matrix[row] * x + (long)matrix[row + 1] * y + (long)matrix[row + 2] * z) / 4096d, MidpointRounding.ToEven));
            return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }

    private static short[] MultiplyWakeRotation(short[] left, short[] right)
    {
        short[] matrix = new short[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                long sum = (long)left[3 * row] * right[column] + (long)left[3 * row + 1] * right[column + 3] + (long)left[3 * row + 2] * right[column + 6];
                matrix[3 * row + column] = unchecked((short)Math.Round(sum / 4096d, MidpointRounding.ToEven));
            }
        }
        return matrix;
    }

    public NativeWorldForestState DrawRandom(ReadOnlySpan<byte> table, out byte value)
    {
        Validate();
        if (table.Length != 256)
        {
            throw new InvalidDataException("Native541F10 requires its original256-byte table.");
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        value = NextRandom(table, ref cursor, ref offset);
        return this with { RandomCursor = cursor, RandomOffset = offset };
    }

    public NativeWorldForestState AdvanceParticlesOnly(IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        //555B80 runs after550070 even when controlled50 has no emitter branch.
        return this with { Particles = AdvanceParticles(Particles, effects) };
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
