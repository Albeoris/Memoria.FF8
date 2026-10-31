namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematic31Presentation
{
    internal static bool Owns(ushort effect) => effect is 265 or 266 or 267 or 268 or 269 or 270;

    internal static NativeBattlePilotVisualIdentity DissolveSource(NativeBattlePilotVisualIdentity identity, NativeBattleEffectArena arena, uint clone, uint row, uint geometry)
    {
        NativeBattlePilotVisualIdentity allocated = NativeBattlePilotVisualLifetimes.Identity(identity.Effect, arena, clone, 0);
        return identity with { ResourceGeneration = unchecked(((allocated.Generation * 397 ^ row) * 397 ^ geometry) * 397 ^ identity.ResourceGeneration) };
    }
}
