using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    public NativeRagnarokWorldAllocation ReadParkedRagnarokAllocation(WorldTransition packet, NativeWorldEntryMemory entry, NativeEventMemory memory, NativeSavedWorldState saved, NativeCharacterVitals vitals)
    {
        NativeSavedWorldFootReturn foot = saved.FootReturn ?? throw new NotSupportedException("Parked Ragnarok entry62 requires the actual independent foot return.");
        foot.Validate();
        if (unchecked((byte)packet.Mode) != 0 || unchecked((sbyte)packet.EntryIndex) != 62 || foot.EntityType105 != 0 || foot.AnimationVariant108 != 0)
        {
            throw new NotSupportedException("Parked Ragnarok reload requires the original62/0/-1 and exact normal-foot variant0 save.");
        }
        NativeSavedWorldPose parked = saved.RagnarokPose2 ?? throw new NotSupportedException("Parked Ragnarok reload has no independently saved shape1 pose2.");
        vitals.Validate();
        NativeWorldPathPosition cactuar = NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(contactExecutable ??= File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe")));
        return NativeRagnarokWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), packet, entry, memory, saved, vitals.GuardianForces[13].Flags, cactuar, foot.Pose0, foot.CameraYaw96, saved.ReadMapDisplayModeForFreshFootLoader(), parked);
    }
}
