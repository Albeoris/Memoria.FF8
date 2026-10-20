namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89ModelStep(NativeBattleRinoa89Frame Frame, NativeBattleModulePoolTask Task);

public static class NativeBattleRinoa89Model
{
    private const int Model = 52;
    private const int Directory = 232;

    public static NativeBattleModulePoolTask Initialize6EC060(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleEffectOwnedModelResource resource)
    {
        RequireResource(resource);
        task = task.WriteWord(Model, 2).WriteDword(Model + 124, uint.MaxValue).WriteDword(Model + 140, 0).WriteDword(Model + 120, 0).WriteByte(Model + 97, 0).WriteByte(Model + 96, 0).WriteDword(Model + 104, checked(taskAddress + Directory)).WriteDword(Model + 100, checked(taskAddress + Directory)).WriteByte(Model + 109, 0).WriteDword(Model + 40, 0x808080);
        for (int index = 0; index < 4; index++)
        {
            task = task.WriteDword(Directory + index * 4, checked(resource.Address + (uint)resource.Section(index)));
        }
        return task;
    }

    public static NativeBattleRinoa89ModelStep Select6574D0(NativeBattleRinoa89Frame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, short selector)
    {
        RequireResource(resource);
        RequireSpeed(task);
        NativeBattleEffectArena skeleton = ReadSkeleton(frame.Staging, resource);
        int count = skeleton.Read(0, 1)[0];
        NativeBattlePoseFirstFrame selected = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), count, selector);
        task = task.WriteByte(Model + 108, checked((byte)selector)).WriteByte(Model + 109, (byte)((task.ReadByte(Model + 109) & 0xF3) | 4)).WriteWord(Model + 110, selected.ByteOffset).WriteWord(Model + 112, selected.BitOffset).WriteByte(Model + 114, 1).WriteByte(Model + 115, NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan())[selector]);
        return Publish(frame, task, resource, skeleton, selected);
    }

    public static NativeBattleRinoa89ModelStep Advance6FBDB0(NativeBattleRinoa89Frame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource)
    {
        RequireResource(resource);
        RequireSpeed(task);
        byte selector = task.ReadByte(Model + 108);
        if (task.ReadByte(Model + 114) >= task.ReadByte(Model + 115))
        {
            return Select6574D0(frame, task, resource, selector);
        }
        NativeBattleEffectArena skeleton = ReadSkeleton(frame.Staging, resource);
        int count = skeleton.Read(0, 1)[0];
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(resource.PoseSource.Animations.AsSpan(), count, selector, current, task.ReadWord(Model + 110), task.ReadWord(Model + 112));
        byte flags = task.ReadByte(Model + 109);
        task = task.WriteWord(Model + 110, next.ByteOffset).WriteWord(Model + 112, next.BitOffset).WriteByte(Model + 114, unchecked((byte)(task.ReadByte(Model + 114) + 1))).WriteByte(Model + 109, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        return Publish(frame, task, resource, skeleton, next);
    }

    public static NativeBattleEffectArena ReadSkeleton(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource)
    {
        RequireResource(resource);
        uint address = checked(resource.Address + (uint)resource.Section(0));
        int length = resource.Section(1) - resource.Section(0);
        return NativeBattleTemporaryMemory.ReadView(staging, address, length);
    }

    public static NativeBattleFileStaging PublishSkeleton(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, NativeBattleEffectArena skeleton)
    {
        uint address = checked(resource.Address + (uint)resource.Section(0));
        int length = resource.Section(1) - resource.Section(0);
        return NativeBattleTemporaryMemory.PublishView(staging, address, skeleton, length);
    }

    private static NativeBattleRinoa89ModelStep Publish(NativeBattleRinoa89Frame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, NativeBattleEffectArena skeleton, NativeBattlePoseFirstFrame pose)
    {
        skeleton = NativeBattleArenaPose.WritePose(skeleton, 0, pose.Pose);
        skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, pose.Pose);
        return new(frame with { Staging = PublishSkeleton(frame.Staging, resource, skeleton) }, task);
    }

    private static void RequireResource(NativeBattleEffectOwnedModelResource resource)
    {
        if (resource.Address != 0x14913FC || resource.Bytes.Length != 16692 || resource.Section(0) != 24 || resource.Section(1) != 1672 || resource.Section(2) != 13124 || resource.Section(3) != 16680)
        {
            throw new InvalidDataException("Native Wishing Star requires its actual embedded model and skeleton directory.");
        }
        resource.PoseSource.Validate();
    }

    private static void RequireSpeed(NativeBattleModulePoolTask task)
    {
        if ((task.ReadByte(Model + 109) & 3) != 0)
        {
            throw new NotSupportedException("Native Wishing Star requires its reached normal pose cursor speed.");
        }
    }
}
