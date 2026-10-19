using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278CreaturePose(NativeBattleModulePoolTask Task, NativeBattleCarbuncleExecutableMemory Executable);

/// <summary>Carbuncle's embedded creature model and its original mutable executable skeleton.</summary>
public static class NativeBattleCarbuncle278Creature
{
    public static NativeBattleModulePoolTask Initialize6EC060(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleEffectOwnedModelResource resource)
    {
        if (resource.Address != 0x10B6F94 || task.Payload.Length != 0x5E0 - 12)
        {
            throw new InvalidDataException("Native Carbuncle creature requires its actual task and original model resource.");
        }
        task = task.WriteWord(0x20, 2).WriteDword(0x9C, uint.MaxValue).WriteDword(0xAC, 0).WriteDword(0x98, 0).WriteByte(0x81, 0).WriteByte(0x80, 0).WriteDword(0x88, unchecked(taskAddress + 0xBC)).WriteDword(0x84, unchecked(taskAddress + 0xBC)).WriteByte(0x8D, 0).WriteDword(0x48, 0x808080);
        for (int index = 0; index < 4; index++)
        {
            int relative = BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(4 + index * 4, 4));
            task = task.WriteDword(0xBC + index * 4, unchecked(resource.Address + (uint)relative));
        }
        return task;
    }

    public static NativeBattleCarbuncle278CreaturePose Select6574D0(NativeBattleModulePoolTask task, NativeBattleCarbuncleExecutableMemory executable, NativeBattleEffectOwnedModelResource resource, byte selector)
    {
        executable.Validate();
        if ((task.ReadByte(0x8D) & 3) != 0)
        {
            throw new NotSupportedException("Native Carbuncle creature pose speed requires its current selected cursor contract.");
        }
        uint skeleton = task.ReadDword(0xBC);
        uint animations = task.ReadDword(0xC4);
        uint end = task.ReadDword(0xC8);
        if (skeleton != NativeBattleCarbuncleExecutableMemory.Address || animations != resource.Address + resource.Section(2) || end != resource.Address + resource.Section(3))
        {
            throw new InvalidDataException("Native Carbuncle creature's current section pointers changed their original owners.");
        }
        int boneCount = executable.Read(skeleton, 1)[0];
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), boneCount, selector);
        task = task.WriteByte(0x8C, selector).WriteByte(0x8D, (byte)((task.ReadByte(0x8D) & 0xF3) | 4)).WriteWord(0x8E, first.ByteOffset).WriteWord(0x90, first.BitOffset).WriteByte(0x92, 1).WriteByte(0x93, NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan())[selector]);
        return new(task, executable.SelectFirstPose(first));
    }
}
