namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    public NativeWorldWaterStepResult EmitChocoboParticles(byte type, byte groundType, int animation, ushort counter, byte edges, NativeWorldPathPosition position, short yaw, int priorGraphicsSpeed, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        NativeWorldWaterStepResult step = EmitWaterStep(type, groundType, animation, counter, edges, position, 0, yaw, 0, effects);
        NativeWorldForestState forest = step.Forest.EmitWaterSplash(groundType, animation, counter, type == 17 ? 16 : 8, position, random, effects);
        if (groundType > 5)
        {
            return new(forest, step.Edges);
        }

        byte cursor = forest.RandomCursor;
        byte offset = forest.RandomOffset;
        List<NativeWorldForestParticle> particles = [.. forest.Particles];
        EmitForestDust(particles, groundType, position, yaw, priorGraphicsSpeed, random, effects, ref cursor, ref offset);
        return new(forest with { RandomCursor = cursor, RandomOffset = offset, Particles = particles.OrderBy(particle => particle.Slot).ToArray() }, step.Edges);
    }
}
