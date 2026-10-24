namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldForestState
{
    public NativeWorldForestState AdvanceRagnarok(byte groundType, NativeRagnarokWorldObject body, int groundHeight, int priorGraphicsSpeed, NativeWorldPathPosition follow, byte scriptMode, IReadOnlyList<NativeWorldPathPosition> thrustOffsets, ReadOnlySpan<byte> random, IReadOnlyList<NativeWorldForestEffect> effects)
    {
        Validate();
        if (body.ShapeId != 1 || random.Length != 256 || effects.Count != 18 || priorGraphicsSpeed is < 0 or > 46340 || thrustOffsets.Count != 2)
        {
            throw new InvalidDataException("Ragnarok effects require their actual body, prior movement, thrust offsets and complete original records.");
        }
        NativeWorldForestState emitted = groundType == 8 ? EmitVehicleDust(body.Position, body.Pitch, body.Yaw, body.Roll, groundHeight, priorGraphicsSpeed, random, effects) : this;
        byte cursor = emitted.RandomCursor;
        byte offset = emitted.RandomOffset;
        List<NativeWorldForestParticle> particles = [.. emitted.Particles];
        byte? sprayType = null;
        bool spray = false;
        if (groundType is >= 30 and <= 34 or 10 or 11)
        {
            short difference = unchecked((short)(groundHeight - body.Position.Y));
            spray = priorGraphicsSpeed > (difference >> 2) + NextRandom(random, ref cursor, ref offset) && Math.Abs((int)difference) < 420;
            sprayType = 15;
        }
        else if (groundType is 9 or 17)
        {
            spray = (priorGraphicsSpeed >> 1) > ((groundHeight - body.Position.Y) >> 4) + NextRandom(random, ref cursor, ref offset);
            sprayType = 20;
        }
        if (spray)
        {
            NativeWorldForestEffect effect = effects[sprayType!.Value - 4];
            for (int side = 0; side < 2; side++)
            {
                short yaw = unchecked((short)(body.Yaw + (side == 0 ? 800 : -800) + 8 * NextRandom(random, ref cursor, ref offset) - 1024));
                byte rotationRandom = NextRandom(random, ref cursor, ref offset);
                short roll = unchecked((short)(body.Roll + (sprayType == 15 ? rotationRandom - 128 : 4 * rotationRandom - 512)));
                EmitParticle(particles, effect, new(body.Position.X, groundHeight, body.Position.Z), body.Pitch, yaw, roll, 3, random, ref cursor, ref offset);
            }
        }
        if (scriptMode is not (5 or 6))
        {
            short roll = unchecked((short)-body.Roll);
            foreach (NativeWorldPathPosition thrust in thrustOffsets)
            {
                (short x, short y, short z) = RotateParticleVelocity(body.Pitch, body.Yaw, roll, checked((short)thrust.X), checked((short)thrust.Y), checked((short)thrust.Z));
                NativeWorldPathPosition position = new(body.Position.X + x, body.Position.Y - 146 + y, body.Position.Z + z);
                if (priorGraphicsSpeed != 0)
                {
                    EmitParticle(particles, effects[8], position, body.Pitch, body.Yaw, roll, 2, random, ref cursor, ref offset);
                }
                EmitParticle(particles, effects[9], position, body.Pitch, body.Yaw, roll, 2, random, ref cursor, ref offset);
            }
        }
        return this with { RandomCursor = cursor, RandomOffset = offset, Particles = AdvanceParticles(particles, effects, follow) };
    }
}
