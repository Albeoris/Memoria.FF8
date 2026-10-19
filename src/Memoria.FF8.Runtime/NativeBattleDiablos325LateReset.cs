using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325LateReset
{
    public static NativeBattleDiablos325TailStep Reset65659F(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera)
    {
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        frame = NativeBattleDiablos325Puffs.Reset6545B0(frame);
        NativeBattleCameraPose pose = new(new(375, -8170, unchecked((short)(z - 289))), new(-175, -8111, unchecked((short)(z - 240))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(pose));
    }

    public static NativeBattleDiablos325Frame ClearParticleLinks655567(NativeBattleDiablos325Frame frame)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == NativeBattleDiablos325Particles.Descriptor);
        NativeBattleEffectTaskPool cleared = pool with { Head = null, Tail = null };
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(item => item.DescriptorAddress == cleared.DescriptorAddress ? cleared : item).ToImmutableArray() } };
    }

    public static NativeBattleDiablos325TailStep Advance6565FA(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera, ushort counter)
    {
        if (counter is < 217 or > 236)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        int angle = (102 + (counter - 217) * 4096 / 20) / 2;
        int ratio = 2048 - NativeBattleCameraMath.Cosine(angle) / 2;
        NativeBattleCameraPose first = new(new(-471, -9765, unchecked((short)(z + 184))), new(-176, -8109, unchecked((short)(z - 241))), camera.FocalLength, camera.Roll);
        NativeBattleCameraPose second = new(new(459, -9677, unchecked((short)(z + 124))), new(-176, -8109, unchecked((short)(z - 241))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio)));
    }

    public static NativeBattleDiablos325TailStep Reset6566B7(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera)
    {
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        NativeBattleCameraPose pose = new(new(-838, -8389, unchecked((short)(z - 1164))), new(39, -8496, unchecked((short)(z - 235))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(pose));
    }
}
