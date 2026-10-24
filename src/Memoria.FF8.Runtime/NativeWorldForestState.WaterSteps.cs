namespace Memoria.FF8.Runtime;

public readonly record struct NativeWorldWaterStepResult(NativeWorldForestState Forest, byte? Edges);

public sealed partial record NativeWorldForestState
{
    public NativeWorldWaterStepResult EmitFootWaterStep(byte groundType, int animation, int animationCounter, byte? previousEdges, NativeWorldPathPosition position, short pitch, short yaw, short roll, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (animationCounter is < 0 or > ushort.MaxValue || previousEdges > 3)
        {
            throw new InvalidDataException("Native water footsteps require the executed model phase and two edge flags.");
        }
        if (animation != 1)
        {
            return new(this, previousEdges);
        }
        if (previousEdges is null && animationCounter >= 112 && groundType is 9 or 11 or 17)
        {
            throw new NotSupportedException("Native water footsteps require their retained process edge flags; migrate this save explicitly.");
        }
        byte edges = previousEdges ?? 0;
        int side = -1;
        for (int index = 0; index < 2; index++)
        {
            byte bit = (byte)(1 << index);
            if (animationCounter < (index == 0 ? 272 : 112))
            {
                edges &= (byte)~bit;
            }
            else if ((edges & bit) == 0)
            {
                edges |= bit;
                side = index;
            }
        }
        if (side < 0 || groundType is not (9 or 11 or 17))
        {
            return new(this, edges);
        }
        NativeWorldForestEffect effect = effects.Single(item => item.Type == 16);
        if (effect.Lifetime != 32 || effect.DrawMode != 3 || effect.VelocityX != 0 || effect.VelocityY != 0 || effect.VelocityZ != 0)
        {
            throw new InvalidDataException("Native water footsteps require their original stationary plane record.");
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        EmitParticle(particles, effect, position, pitch, unchecked((short)(yaw + (side == 0 ? -128 : 128))), roll, 0, [], ref cursor, ref offset);
        return new(this with { Particles = particles.OrderBy(particle => particle.Slot).ToArray() }, edges);
    }
}
