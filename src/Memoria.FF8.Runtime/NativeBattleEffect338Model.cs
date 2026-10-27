using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect338Skeleton([property: JsonRequired] NativeBattleEffectArena Storage)
{
    public const uint Address = 0x136BB48;
    public const int Length = 0x550;

    public static NativeBattleEffect338Skeleton FromOriginal(NativeBattleEffectOwnedModelResource resource)
    {
        RequireResource(resource);
        return new(NativeBattleEffectArena.Clear().Write(0, resource.Bytes.AsSpan().Slice(0x18, Length)));
    }

    public void Validate()
    {
        Storage.Validate();
        Storage.ValidateExtent(Length);
    }

    public NativeBattleEffect338Skeleton Publish(NativeBattleDecodedPose pose)
    {
        Validate();
        if (pose.Bones.IsDefault || pose.Bones.Length != Storage.Read(0, 1)[0])
        {
            throw new InvalidDataException("Effect338 pose requires its actual skeleton bone count.");
        }
        NativeBattleEffectArena storage = NativeBattleArenaPose.WritePose(Storage, 0, pose);
        return this with { Storage = NativeBattleArenaPose.RestoreBoneMatrices(storage, 0, pose) };
    }

    public static void RequireResource(NativeBattleEffectOwnedModelResource resource)
    {
        if (resource.Address != 0x136BB30 || resource.Bytes.Length != 0x4DD0 || resource.Section(0) != 0x18 || resource.Section(1) != 0x568 || resource.Section(2) != 0x3730 || resource.Section(3) != 0x4DC4 || resource.Section(4) != 0x4DD0)
        {
            throw new InvalidDataException("Effect338 requires its original embedded four-section model.");
        }
        resource.PoseSource.Validate();
    }
}

public sealed record NativeBattleEffect338PoseStep(NativeBattleEffect338Skeleton Skeleton, NativeBattleModulePoolTask Task);

public static class NativeBattleEffect338Model
{
    public static NativeBattleModulePoolTask Initialize6EC060(NativeBattleModulePoolTask task, uint address, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleEffect338Skeleton.RequireResource(resource);
        task = task.WriteWord(0x24, 2).WriteDword(0xA0, uint.MaxValue).WriteDword(0xB0, 0).WriteDword(0x9C, 0).WriteByte(0x85, 0).WriteByte(0x84, 0).WriteDword(0x8C, unchecked(address + 0xC0)).WriteDword(0x88, unchecked(address + 0xC0)).WriteByte(0x91, 0).WriteDword(0x4C, 0x808080);
        for (int section = 0; section < 4; section++)
        {
            task = task.WriteDword(0xC0 + section * 4, checked(resource.Address + (uint)resource.Section(section)));
        }
        return task;
    }

    public static NativeBattleEffect338PoseStep Select6574D0(NativeBattleEffect338Skeleton skeleton, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, byte selector)
    {
        Require(skeleton, task, resource);
        int count = skeleton.Storage.Read(0, 1)[0];
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), count, selector);
        task = task.WriteByte(0x90, selector).WriteByte(0x91, (byte)((task.ReadByte(0x91) & 0xF3) | 4)).WriteWord(0x92, first.ByteOffset).WriteWord(0x94, first.BitOffset).WriteByte(0x96, 1).WriteByte(0x97, NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan())[selector]);
        return new(skeleton.Publish(first.Pose), task);
    }

    public static NativeBattleEffect338PoseStep Advance6FBDB0(NativeBattleEffect338Skeleton skeleton, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource)
    {
        Require(skeleton, task, resource);
        if (task.ReadByte(0x96) >= task.ReadByte(0x97))
        {
            return Select6574D0(skeleton, task, resource, task.ReadByte(0x90));
        }
        int count = skeleton.Storage.Read(0, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton.Storage, 0, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(resource.PoseSource.Animations.AsSpan(), count, task.ReadByte(0x90), pose, task.ReadWord(0x92), task.ReadWord(0x94));
        task = task.WriteWord(0x92, next.ByteOffset).WriteWord(0x94, next.BitOffset).WriteByte(0x96, unchecked((byte)(task.ReadByte(0x96) + 1))).WriteByte(0x91, (byte)((task.ReadByte(0x91) & 0xF3) | ((((task.ReadByte(0x91) >> 2) + 1) & 1) << 2)));
        return new(skeleton.Publish(next.Pose), task);
    }

    private static void Require(NativeBattleEffect338Skeleton skeleton, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource)
    {
        skeleton.Validate();
        NativeBattleEffect338Skeleton.RequireResource(resource);
        if ((task.ReadByte(0x91) & 3) != 0)
        {
            throw new NotSupportedException("Effect338 pose speed requires its actual current cursor contract.");
        }
        for (int section = 0; section < 4; section++)
        {
            if (task.ReadDword(0xC0 + section * 4) != checked(resource.Address + (uint)resource.Section(section)))
            {
                throw new InvalidDataException("Effect338 current model directory has changed its original section owner.");
            }
        }
    }
}
