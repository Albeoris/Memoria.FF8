namespace Memoria.FF8.Runtime;

internal static class NativeBattleRayBomb307Visuals
{
    internal static void Born(NativeBattleRayBomb307Frame frame, int slot)
    {
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Particles, NativeBattleRayBomb307Profile.ParticlesAddress, slot);
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleRayBomb307Frame frame, int slot)
    {
        return NativeBattlePilotVisualLifetimes.Identity(307, frame.State.Particles, NativeBattleRayBomb307Profile.ParticlesAddress, slot) with { ResourceGeneration = 0xCE1760 };
    }
}
