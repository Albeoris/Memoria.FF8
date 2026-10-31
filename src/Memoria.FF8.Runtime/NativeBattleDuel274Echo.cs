namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Echo
{
    public static NativeBattleDuel274Frame Draw68A7B0(NativeBattleDuel274Frame frame, NativeBattleGraphicsModel model, uint echoAddress, uint sectionAddress, Func<NativeBattleDuel274Frame, int, int, byte[]> readSection, ushort scratchMaximum, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<NativeBattleDuel274Frame, NativeBattleGfGeometryBatch, NativeBattleDuel274Frame> submitCommon, short zsf3, short zsf4)
    {
        return NativeBattleDuelEchoDrawing.Draw6A2760(frame, model, echoAddress, 12, current => NativeBattleDuel274ArenaEcho.Read(current, echoAddress), (current, task, writes) => NativeBattleDuel274ArenaEcho.StoreRanges(current, echoAddress, task, writes), sectionAddress, readSection, scratchMaximum, view, projection, (current, address, bytes) => current with { Staging = current.Staging.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging.ReadSnapshot(address, length), submitCommon, zsf3, zsf4, current => current.Staging, (current, staging) => current with { Staging = staging }, frame.PilotIdentity is { } identity ? identity with { ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ sectionAddress) } : null, frame.CapturePresentation ? bank => unchecked(((long)sectionAddress * 397 ^ echoAddress) * 397 ^ NativeBattlePilotVisualLifetimes.Identity(274, frame.Memory.Arena!, echoAddress, (bank - 148) >> 9).Generation) : null);
    }
}
