namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    public NativeWorldForestState AdvanceScriptedSteam(byte entityType, NativeWorldPathPosition position, NativeWorldPathAngles angles, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        NativeWorldForestEffect effect = effects.Single(effect => effect.Type == 0);
        if (entityType is not (65 or 66) || random.Length != 256)
        {
            throw new InvalidDataException("Scripted steam requires its native controlled carriage and complete random table.");
        }
        byte cursor = RandomCursor;
        byte offset = RandomOffset;
        List<NativeWorldForestParticle> particles = [.. Particles];
        for (int side = 0; side < 2; side++)
        {
            short yaw = unchecked((short)(angles.Yaw + (side == 0 ? 4 : -4) * NextRandom(random, ref cursor, ref offset)));
            EmitParticle(particles, effect, position, angles.Pitch, yaw, angles.Roll, 3, random, ref cursor, ref offset);
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = AdvanceParticles(particles, effects) };
    }
}
