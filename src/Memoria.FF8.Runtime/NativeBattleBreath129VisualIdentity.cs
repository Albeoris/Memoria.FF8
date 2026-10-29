namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129VisualIdentity
{
    public static NativeBattlePilotVisualIdentity Task(NativeBattleBreath129Frame frame, uint address, uint source, int primitive = 0) => NativeBattlePilotVisualLifetimes.Identity(129, NativeBattleBreath129Controller.Read(frame, address), primitive) with { ResourceGeneration = source };

    public static NativeBattlePilotVisualIdentity Workspace(NativeBattleBreath129Frame frame, uint address, uint source, int primitive)
    {
        var owner = Containing(frame, address) ?? throw new InvalidDataException("Breath presentation workspace leaves its actual allocated task.");
        return NativeBattlePilotVisualLifetimes.Identity(129, owner.Task, primitive) with { ResourceGeneration = ((long)source << 32) | (uint)owner.Offset };
    }

    public static NativeBattlePilotVisualIdentity Segment(NativeBattleBreath129Frame frame, uint bank, uint first, uint second)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(129, arena, bank, checked((int)((first - bank) / 28)), checked((int)((second - bank) / 28)));
        return identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(129, arena, bank, checked((int)((second - bank) / 28))).Generation };
    }

    public static NativeBattlePilotVisualIdentity Fibre(NativeBattleBreath129Frame frame, uint address, uint geometry)
    {
        NativeBattleModulePoolTask task = NativeBattleBreath129Controller.Read(frame, address);
        uint triangles = task.ReadDword(24);
        long capturedGeneration = Containing(frame, triangles) is { } capture ? NativeBattlePilotVisualLifetimes.Identity(129, capture.Task).Generation : triangles == NativeBattleBreath129Profile.ControlTrianglesAddress ? 0 : throw new InvalidDataException("Breath fibre presentation triangle source leaves its actual captured task or immutable control source.");
        long resource = unchecked((((long)geometry << 32) | task.ReadDword(20)) * 397 ^ triangles);
        return NativeBattlePilotVisualLifetimes.Identity(129, task) with { ResourceGeneration = unchecked(resource * 397 ^ capturedGeneration) };
    }

    private static (NativeBattleModulePoolTask Task, int Offset)? Containing(NativeBattleBreath129Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length))
            {
                return (pool.Slots[checked((int)(offset / pool.Stride))], checked((int)(offset % pool.Stride)));
            }
        }
        return null;
    }
}
