using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public NativeRagnarokWorldAllocation ReadParkedRagnarokAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals, NativeSavedWorldFootReturn? battleReload = null)
    {
        if (battleReload is not null && (battleReload.WorldMode != 3 || saved.FootReturn != battleReload))
        {
            throw new InvalidDataException("World mode2 requires its actual retained mode3 saved-foot owner.");
        }
        if ((battleReload?.EntityType105 ?? unchecked((byte)packet.Mode)) != 0)
        {
            throw new NotSupportedException("Parked Ragnarok allocation requires its original normal-foot packet.");
        }
        NativeSavedWorldPose foot;
        ushort cameraYaw;
        int spawn = unchecked((sbyte)packet.EntryIndex);
        if (battleReload is not null || spawn == 62)
        {
            NativeSavedWorldFootReturn receipt = battleReload ?? saved.FootReturn ?? throw new NotSupportedException("Parked Ragnarok entry62 requires the actual independent foot return.");
            receipt.RequireCompleteFootReload();
            if (receipt.EntityType105 != 0 || receipt.AnimationVariant108 != 0)
            {
                throw new NotSupportedException("Parked Ragnarok saved reload requires the exact normal-foot variant0 owner.");
            }
            foot = receipt.Pose0;
            cameraYaw = receipt.CameraYaw96;
        }
        else
        {
            if ((uint)spawn >= Spawns.Count)
            {
                throw new InvalidDataException("Parked Ragnarok normal-foot spawn is absent from original WMSET.");
            }
            WorldSpawn source = Spawns[spawn];
            foot = new(source.X, source.Z, source.NativeHeight, checked((ushort)((source.Unknown >> 8) * 16)));
            cameraYaw = checked((ushort)((source.Unknown & 255) * 16));
        }
        NativeSavedWorldPose parked = saved.RagnarokPose2 ?? throw new NotSupportedException("Parked Ragnarok reload has no independently saved shape1 pose2.");
        vitals.Validate();
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        return NativeRagnarokWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, foot, cameraYaw, saved.ReadMapDisplayModeForFreshFootLoader(), parked, battleReload: battleReload);
    }
}
