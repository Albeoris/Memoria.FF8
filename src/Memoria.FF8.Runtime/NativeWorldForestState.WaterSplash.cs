namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    public NativeWorldForestState EmitWaterSplash(byte groundType, int animation, int animationCounter, int count, NativeWorldPathPosition position, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (animationCounter is < 0 or > ushort.MaxValue || count is not (8 or 16) || random.Length != 256)
        {
            throw new InvalidDataException("Native water splash requires its actual model phase, body burst count and complete random table.");
        }
        if (animation != 1 || groundType is not (>= 30 and <= 34 or 10 or 11) || Math.Abs(animationCounter - 304) >= 4 && Math.Abs(animationCounter - 144) >= 4)
        {
            return this;
        }
        NativeWorldForestEffect effect = effects.Single(item => item.Type == 2);
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        short yaw = 0;
        short roll = 0;
        for (int index = 0; index < count; index++)
        {
            yaw = unchecked((short)(yaw + (index << 9) + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
            roll = unchecked((short)(roll + 4 * NextRandom(random, ref cursor, ref offset) - 512));
            EmitParticle(particles, effect, position, 0, yaw, roll, 3, random, ref cursor, ref offset);
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = particles.OrderBy(particle => particle.Slot).ToArray() };
    }
}
