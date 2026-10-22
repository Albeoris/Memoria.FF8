using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleElectrocute275Ribbons
{
    public static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) DrawProjected(NativeBattleElectrocute275Frame frame, uint headAddress, short count, int textureVOffset, NativeBattleElectrocute275Resources resources)
    {
        uint v = unchecked((uint)((textureVOffset + 0xC0) << 8));
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 3, 17, v | 0x3D140008, v | 0x00B70038, unchecked((ushort)(v | 8)), unchecked((ushort)(v | 0x38)));
        return Draw(frame, headAddress, count, resources, WorldPointsOffset, policy);
    }

    public static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) DrawBoneProjected(NativeBattleElectrocute275Frame frame, uint headAddress, short count, NativeBattleElectrocute275Resources resources)
    {
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Anchored, 4, 17, 0x3D14C008, 0x00B7C038, 0xC008, 0xC038);
        return Draw(frame, headAddress, count, resources, BonePointsOffset, policy);
    }

    private static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleElectrocute275Frame frame, uint headAddress, short count, NativeBattleElectrocute275Resources resources, int pointsOffset, NativeBattleRibbonPacketPolicy policy)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        int arenaOffset = checked((int)(working - ArenaAddress) + pointsOffset);
        NativeBattleRibbonBank bank = new(checked(working + (uint)pointsOffset), arenaOffset, PointCount);
        var result = NativeBattleRibbonPackets.Draw(frame.Memory.Arena ?? throw new NotSupportedException("Electrocute ribbons require their physical point arena."), headAddress, count, bank, resources.DistanceTable, policy);
        return (frame with { Memory = frame.Memory with { Arena = result.Arena } }, result.Batch);
    }
}
