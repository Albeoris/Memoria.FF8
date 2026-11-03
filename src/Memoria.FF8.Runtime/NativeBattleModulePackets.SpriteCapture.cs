namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    public static NativeBattleGfGeometryBatch CaptureSprite571DC0(NativeBattleModulePacketBatch batch)
    {
        return batch.CapturedWrites ?? throw new NotSupportedException("Native571DC0 requires its actual ordered physical stores; logical packets cannot reconstruct rejected writes or retained UV padding.");
    }
}
