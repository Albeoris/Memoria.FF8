using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleElectrocute275Ribbons
{
    public static uint CaptureComposedBoneDirectionWord(int boneCount)
    {
        if (boneCount is < 1 or > byte.MaxValue)
        {
            throw new NotSupportedException("Electrocute's incoming bone direction requires an actual completed nonempty bone composition.");
        }
        return checked((uint)(boneCount - 1));
    }

    public static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) DrawProjected(NativeBattleElectrocute275Frame frame, uint headAddress, short count, int textureVOffset, NativeBattleElectrocute275Resources resources, uint? incomingDirectionWord = null)
    {
        uint v = unchecked((uint)((textureVOffset + 0xC0) << 8));
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 3, 17, v | 0x3D140008, v | 0x00B70038, unchecked((ushort)(v | 8)), unchecked((ushort)(v | 0x38)));
        return Draw(frame, headAddress, count, resources, WorldPointsOffset, policy, incomingDirectionWord);
    }

    public static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) DrawBoneProjected(NativeBattleElectrocute275Frame frame, uint headAddress, short count, NativeBattleElectrocute275Resources resources)
    {
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Anchored, 4, 17, 0x3D14C008, 0x00B7C038, 0xC008, 0xC038);
        return Draw(frame, headAddress, count, resources, BonePointsOffset, policy, frame.BoneDirectionWord);
    }

    private static (NativeBattleElectrocute275Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleElectrocute275Frame frame, uint headAddress, short count, NativeBattleElectrocute275Resources resources, int pointsOffset, NativeBattleRibbonPacketPolicy policy, uint? incomingDirectionWord = null)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        int arenaOffset = checked((int)(working - ArenaAddress) + pointsOffset);
        NativeBattleRibbonBank bank = new(checked(working + (uint)pointsOffset), arenaOffset, PointCount);
        NativeBattleRibbonPacketStep result = NativeBattleRibbonPackets.DrawWithDirection(frame.Memory.Arena ?? throw new NotSupportedException("Electrocute ribbons require their physical point arena."), headAddress, count, bank, resources.DistanceTable, policy, incomingDirectionWord);
        NativeBattleElectrocute275Frame updated = frame with { Memory = frame.Memory with { Arena = result.Arena } };
        if (policy.Width == NativeBattleRibbonWidthPolicy.Anchored)
        {
            updated = updated with { BoneDirectionWord = result.DirectionWord };
        }
        return (updated, result.Batch);
    }
}
