using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAcid317Trail
{
    public static (NativeBattleAcid317Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleAcid317Frame frame, uint headAddress, short count, NativeBattleAcid317Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        uint working = frame.State.ReadDword(WorkingGlobal);
        int arenaOffset = checked((int)(working - ArenaAddress) + PointsOffset);
        NativeBattleRibbonBank bank = new(checked(working + PointsOffset), arenaOffset, PointCount);
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 3, 17, 0x3D14E048, 0x00BAE078, 0xE048, 0xE078);
        var result = NativeBattleRibbonPackets.Draw(frame.Memory.Arena ?? throw new NotSupportedException("Acid trail requires its physical point arena."), headAddress, count, bank, resources.DistanceTable, policy, pilotIdentity: pilotIdentity, readWorldCenter: pilotWorld is not null ? point => pilotWorld[point] : null, identifySegment: pilotIdentity.HasValue ? Identify : null);
        return (frame with { Memory = frame.Memory with { Arena = result.Arena } }, result.Batch);

        NativeBattlePilotVisualIdentity Identify(uint first, uint second)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Acid segment identity requires its actual point arena.");
            NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(317, arena, bank.Address, checked((int)((first - bank.Address) / PointStride)), checked((int)((second - bank.Address) / PointStride)));
            return identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(317, arena, bank.Address, checked((int)((second - bank.Address) / PointStride))).Generation };
        }
    }
}
