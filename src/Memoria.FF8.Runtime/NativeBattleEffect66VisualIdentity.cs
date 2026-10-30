namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect66VisualIdentity
{
    internal static NativeBattleEffectArena Storage(NativeBattleEffect66Frame frame, uint address) => address is >= NativeBattleEffect66Profile.FirstPointAddress and < NativeBattleEffect66Profile.CapturedWorldAddress ? frame.State.Storage : frame.Memory.Arena ?? throw new NotSupportedException("Effect66 presentation requires its actual arena.");

    internal static void Allocated(NativeBattleEffect66Frame frame, uint address) => NativeBattlePilotVisualLifetimes.Allocated(Storage(frame, address), address, 0);

    internal static NativeBattlePilotVisualIdentity Point(NativeBattleEffect66Frame frame, uint address, uint source, int primitive = 0) => NativeBattlePilotVisualLifetimes.Identity(66, Storage(frame, address), address, 0, primitive) with { ResourceGeneration = source };

}
