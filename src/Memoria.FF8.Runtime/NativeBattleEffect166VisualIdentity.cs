namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect166VisualIdentity
{
    internal static NativeBattlePilotVisualIdentity Segment(NativeBattleEffect166Frame frame, NativeBattleModulePoolTask task, uint path, int first, int second, int primitive)
    {
        long a = NativeBattlePilotVisualLifetimes.Identity(166, frame.State.Storage, path + 56, first).Generation;
        long b = NativeBattlePilotVisualLifetimes.Identity(166, frame.State.Storage, path + 56, second).Generation;
        return NativeBattlePilotVisualLifetimes.Identity(166, task, primitive) with { ResourceGeneration = unchecked(((long)path * 397 ^ a) * 397 ^ b) };
    }

    internal static NativeBattlePilotVisualIdentity History(NativeBattleEffect166Frame frame, NativeBattleModulePoolTask task, uint path, int count, int primitive)
    {
        long source = path;
        int ring = unchecked((short)task.ReadWord(12)) % 3;
        for (int index = 0; index < count; index++)
        {
            source = unchecked(source * 397 ^ NativeBattlePilotVisualLifetimes.Identity(166, frame.State.Storage, path + 440, ring).Generation);
            ring = ring == 0 ? 2 : ring - 1;
        }
        return NativeBattlePilotVisualLifetimes.Identity(166, task, primitive) with { ResourceGeneration = unchecked(source * 397 ^ count) };
    }
}
