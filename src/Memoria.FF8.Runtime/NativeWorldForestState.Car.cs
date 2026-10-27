namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    public NativeWorldForestState AdvanceCar(byte groundType, int entityType, NativeWorldPathPosition position, short pitch, short bodyYaw, short roll, int priorGraphicsSpeed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (entityType is not (>= 32 and <= 40 or 132) || random.Length != 256 || priorGraphicsSpeed is < 0 or > 46340)
        {
            throw new InvalidDataException("Car particles require the actual native entity, preceding movement and original random table.");
        }
        NativeWorldForestEffect Effect(byte type) => effects.Single(effect => effect.Type == type);
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        if (groundType is >= 30 and <= 34 or 10 or 11)
        {
            if (priorGraphicsSpeed != 0)
            {
                short yaw = 0;
                short particleRoll = 0;
                for (int index = 0; index < 8; index++)
                {
                    yaw = unchecked((short)(yaw + (index << 9) + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
                    particleRoll = unchecked((short)(particleRoll + 4 * NextRandom(random, ref cursor, ref offset) - 512));
                    EmitParticle(particles, Effect(2), position, 0, yaw, particleRoll, 3, random, ref cursor, ref offset);
                }
            }
        }
        else if (groundType == 8 || groundType is not (27 or 28) && entityType is not (39 or 40))
        {
            int multiplier = groundType == 8 ? 2 : 4;
            if (multiplier * priorGraphicsSpeed > NextRandom(random, ref cursor, ref offset))
            {
                NativeWorldForestEffect effect = Effect(groundType == 8 ? (byte)14 : (byte)3);
                for (int side = 0; side < 2; side++)
                {
                    short yaw = unchecked((short)(bodyYaw + (side == 0 ? 800 : -800) + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
                    short particleRoll = unchecked((short)(roll + 4 * NextRandom(random, ref cursor, ref offset) - 512));
                    EmitParticle(particles, effect, position, pitch, yaw, particleRoll, 3, random, ref cursor, ref offset);
                }
            }
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = AdvanceParticles(particles, effects) };
    }
}
