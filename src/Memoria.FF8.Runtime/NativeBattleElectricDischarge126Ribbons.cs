using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleElectricDischarge126Ribbons
{
    public static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) DrawProjected(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, int textureVOffset, NativeBattleElectricDischarge126Resources resources, uint? incomingDirectionWord = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        uint v = unchecked((uint)((textureVOffset + 0xC0) << 8));
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 5, 17, v | 0x3D140008, v | 0x00B70038, unchecked((ushort)(v | 8)), unchecked((ushort)(v | 0x38)));
        return Draw(frame, headAddress, count, resources, WorldPointsOffset, policy, incomingDirectionWord, pilotIdentity, pilotWorld);
    }

    public static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) DrawBoneProjected(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, NativeBattleElectricDischarge126Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Anchored, 5, 17, 0x3D14C008, 0x00B7C038, 0xC008, 0xC038);
        return Draw(frame, headAddress, count, resources, BonePointsOffset, policy, frame.BoneDirectionWord, pilotIdentity, pilotWorld);
    }

    private static (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleElectricDischarge126Frame frame, uint headAddress, short count, NativeBattleElectricDischarge126Resources resources, int pointsOffset, NativeBattleRibbonPacketPolicy policy, uint? incomingDirectionWord = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        int arenaOffset = checked((int)(working - ArenaAddress) + pointsOffset);
        NativeBattleRibbonBank bank = new(checked(working + (uint)pointsOffset), arenaOffset, PointCount);
        var result = NativeBattleRibbonPackets.Draw(frame.Memory.Arena ?? throw new NotSupportedException("Electric Discharge ribbons require their physical point arena."), headAddress, count, bank, resources.DistanceTable, policy, incomingDirectionWord, pilotIdentity, pilotWorld is null ? null : address => pilotWorld[address], pilotIdentity.HasValue ? Identify : null);
        NativeBattlePilotVisualIdentity Identify(uint first, uint second)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena!;
            int a = checked((int)((first - bank.Address) / PointStride));
            int b = checked((int)((second - bank.Address) / PointStride));
            NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(126, arena, bank.Address, a, b);
            return identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(126, arena, bank.Address, b).Generation };
        }
        return (frame with { Memory = frame.Memory with { Arena = result.Arena } }, result.Batch);
    }
}
