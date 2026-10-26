using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218ModelBuild(NativeBattleEffectArena Model, NativeBattleEffectArena Descriptor);
public sealed record NativeBattleEffect218ModelPose(NativeBattleEffectArena Model, NativeBattleEffectArena Skeleton, bool Completed);

public static class NativeBattleEffect218Model
{
    public static NativeBattleEffect218ModelBuild Initialize633FB0(NativeBattleEffectArena model, NativeBattleEffectArena descriptor, uint descriptorAddress, NativeBattleEffectOwnedModelResource resource, ushort argument)
    {
        resource.PoseSource.Validate();
        if ((descriptorAddress != 0x24F3500 || resource.Address != 0x20DFAB8) && (descriptorAddress != 0x24F3338 || resource.Address != 0xF1DD54))
        {
            throw new InvalidDataException("Effect218 model requires its actual physical descriptor.");
        }
        model = model.Write(0, BitConverter.GetBytes((ushort)2)).Write(7, [0]).Write(0x28, BitConverter.GetBytes(0x808080u)).Write(0x60, [0]).Write(0x61, [0]).Write(0x64, BitConverter.GetBytes(descriptorAddress + 12)).Write(0x68, BitConverter.GetBytes(descriptorAddress + 12)).Write(0x78, new byte[4]).Write(0x7C, BitConverter.GetBytes(uint.MaxValue)).Write(0x84, BitConverter.GetBytes(descriptorAddress)).Write(0x8C, new byte[4]);
        for (int index = 0; index < 3; index++)
        {
            descriptor = descriptor.Write(12 + index * 4, BitConverter.GetBytes(checked(resource.Address + (uint)resource.Section(index))));
        }
        descriptor = descriptor.Write(2, BitConverter.GetBytes(argument)).Write(0x30, BitConverter.GetBytes(checked(resource.Address + (uint)resource.Section(3))));
        return new(model, descriptor);
    }

    public static NativeBattleEffectArena InitializeRotation634040(NativeBattleEffectArena matrix)
    {
        return matrix.Write(0, new byte[16]).Write(0, BitConverter.GetBytes((ushort)4096)).Write(8, BitConverter.GetBytes((ushort)4096)).Write(16, BitConverter.GetBytes((ushort)4096));
    }

    public static NativeBattleEffect218ModelPose Select634020(NativeBattleEffectArena model, NativeBattleEffectArena skeleton, ReadOnlySpan<byte> animations, byte selector)
    {
        byte flags = model.Read(0x6D, 1)[0];
        RequireSpeed(flags);
        int count = skeleton.Read(0, 1)[0];
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations, count, selector);
        byte[] control = new byte[8];
        control[0] = selector;
        control[1] = (byte)((flags & 0xF3) | 4);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), first.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), first.BitOffset);
        control[6] = 1;
        control[7] = NativeBattlePoseDecoder.ReadFrameCounts(animations)[selector];
        skeleton = Publish(skeleton, first.Pose);
        return new(model.Write(0x6C, control), skeleton, false);
    }

    public static NativeBattleEffect218ModelPose Advance6373F0(NativeBattleEffectArena model, NativeBattleEffectArena skeleton, ReadOnlySpan<byte> animations)
    {
        byte[] control = model.Read(0x6C, 8);
        RequireSpeed(control[1]);
        if (control[6] >= control[7])
        {
            return (model.Read(0, 1)[0] & 1) == 0 ? Select634020(model, skeleton, animations, control[0]) with { Completed = true } : new(model, skeleton, true);
        }
        int count = skeleton.Read(0, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(animations, count, control[0], pose, BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(2)), BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(4)));
        control[1] = (byte)((control[1] & 0xF3) | ((((control[1] >> 2) + 1) & 1) << 2));
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), next.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), next.BitOffset);
        control[6] = unchecked((byte)(control[6] + 1));
        return new(model.Write(0x6C, control), Publish(skeleton, next.Pose), false);
    }

    private static NativeBattleEffectArena Publish(NativeBattleEffectArena skeleton, NativeBattleDecodedPose pose) => NativeBattleArenaPose.RestoreBoneMatrices(NativeBattleArenaPose.WritePose(skeleton, 0, pose), 0, pose);

    private static void RequireSpeed(byte flags)
    {
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException("Effect218 pose speed requires its actual current cursor contract.");
        }
    }
}
