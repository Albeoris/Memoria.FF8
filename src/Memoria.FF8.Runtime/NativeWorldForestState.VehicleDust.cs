namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    private NativeWorldForestState EmitVehicleDust(NativeWorldPathPosition position, short pitch, short bodyYaw, short bodyRoll, int groundHeight, int speed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        if (effects.Count <= 15 || effects[15].Type != 19)
        {
            throw new InvalidDataException("Vehicle dust requires its complete original type19 record.");
        }
        short difference = unchecked((short)(groundHeight - position.Y));
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        if (speed > (difference >> 2) + NextRandom(random, ref cursor, ref offset) && Math.Abs((int)difference) < 600)
        {
            for (int side = 0; side < 2; side++)
            {
                short yaw = unchecked((short)(bodyYaw + (side == 0 ? 800 : -800) + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
                short roll = unchecked((short)(bodyRoll + 4 * NextRandom(random, ref cursor, ref offset) - 512));
                EmitParticle(particles, effects[15], new(position.X, groundHeight, position.Z), pitch, yaw, roll, 3, random, ref cursor, ref offset);
            }
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = particles.OrderBy(particle => particle.Slot).ToArray() };
    }

    private static void EmitParticle(List<NativeWorldForestParticle> particles, NativeWorldForestEffect effect, NativeWorldPathPosition position, short pitch, short yaw, short roll, byte flags, ReadOnlySpan<byte> random, ref byte cursor, ref byte offset)
    {
        int slot = Enumerable.Range(0, 64).FirstOrDefault(candidate => !particles.Any(particle => particle.Slot == candidate && particle.Age < particle.Lifetime), -1);
        if (slot < 0)
        {
            return;
        }
        ushort scale = (flags & 2) != 0 ? unchecked((ushort)(4096 + NextRandom(random, ref cursor, ref offset) - 128)) : (ushort)4096;
        byte lifetime = (flags & 1) != 0 ? unchecked((byte)(effect.Lifetime + (NextRandom(random, ref cursor, ref offset) & 7) - 4)) : effect.Lifetime;
        (short vx, short vy, short vz) = RotateParticleVelocity(pitch, yaw, roll, effect.VelocityX, effect.VelocityY, effect.VelocityZ);
        particles.RemoveAll(particle => particle.Slot == slot);
        particles.Add(new(slot, effect.Type, position.X, position.Y, position.Z, yaw, roll, scale, 0, lifetime, true) { Motion = new(pitch, vx, vy, vz) });
    }
}
