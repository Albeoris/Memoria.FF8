using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleElectricDischarge126Ribbons
{
    public static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) DrawProjected(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, int textureVOffset, NativeBattleElectricDischarge126Resources resources, uint? incomingDirectionWord = null)
    {
        uint v = unchecked((uint)((textureVOffset + 0xC0) << 8));
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 5, 17, v | 0x3D140008, v | 0x00B70038, unchecked((ushort)(v | 8)), unchecked((ushort)(v | 0x38)));
        return Draw(frame, headAddress, count, resources, WorldPointsOffset, policy, incomingDirectionWord);
    }

    public static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) DrawBoneProjected(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, NativeBattleElectricDischarge126Resources resources)
    {
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Anchored, 5, 17, 0x3D14C008, 0x00B7C038, 0xC008, 0xC038);
        return Draw(frame, headAddress, count, resources, BonePointsOffset, policy, frame.BoneDirectionWord);
    }

    private static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, NativeBattleElectricDischarge126Resources resources, int pointsOffset, NativeBattleRibbonPacketPolicy policy, uint? incomingDirectionWord = null)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        int arenaOffset = checked((int)(working - ArenaAddress) + pointsOffset);
        NativeBattleRibbonBank bank = new(checked(working + (uint)pointsOffset), arenaOffset, PointCount);
        var result = NativeBattleRibbonPackets.Draw(frame.Memory.Arena ?? throw new NotSupportedException("Electric Discharge ribbons require their physical point arena."), headAddress, count, bank, resources.DistanceTable, policy, incomingDirectionWord);
        return (frame with { Memory = frame.Memory with { Arena = result.Arena } }, result.Batch);
    }
}
