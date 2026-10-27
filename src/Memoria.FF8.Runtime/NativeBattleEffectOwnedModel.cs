using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One effect task's symbolic embedded model and exact current decoded skeleton pose.</summary>
public sealed record NativeBattleEffectOwnedModelState([property: JsonRequired] uint TaskAddress, [property: JsonRequired] uint ResourceAddress, [property: JsonRequired] NativeBattleDecodedPose? DecodedPose);
public sealed record NativeBattleEffectOwnedModelStep(NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, bool Completed);

/// <summary>8DD0F0/190/1C0/220 over an explicit effect-owned model, independent of actor rows.</summary>
public static class NativeBattleEffectOwnedModel
{
    public static NativeBattleEffectOwnedModelStep Initialize8DD0F0(NativeBattleModulePoolTask task, uint taskAddress, NativeBattleEffectOwnedModelResource resource, ushort argument3, uint actualDefaultColor44)
    {
        RequireTask(task);
        resource.PoseSource.Validate();
        _ = checked(taskAddress + 0x140);
        if (taskAddress == 0 || resource.Address == 0 || resource.Bytes.IsDefault || resource.Bytes.Length < 24)
        {
            throw new InvalidDataException("Native effect model requires its exact task and complete source directory owners.");
        }
        task = task.WriteDword(0xA8, 0).WriteByte(0x90, 0).WriteByte(0x91, 0).WriteByte(0x30, (byte)(task.ReadByte(0x30) | 2)).WriteDword(0x5C, actualDefaultColor44).WriteDword(0x94, checked(taskAddress + 0xCC)).WriteDword(0x98, checked(taskAddress + 0xCC)).WriteDword(0xB4, checked(taskAddress + 0xDC));
        for (int index = 0; index < 4; index++)
        {
            task = task.WriteDword(0xCC + index * 4, SectionPointer(resource, index));
        }
        task = task.WriteDword(0xAC, uint.MaxValue).WriteDword(0x58, 0x00808080).WriteWord(0xDE, argument3).WriteDword(0x10C, SectionPointer(resource, 3)).WriteWord(0x13E, 4096);
        return new(task, new(taskAddress, resource.Address, null), false);
    }

    public static NativeBattleEffectOwnedModelStep Select8DD190(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource, short animation)
    {
        Validate(task, state, resource);
        return Select(task.WriteWord(0x136, 0), state, resource, animation);
    }

    public static NativeBattleEffectOwnedModelStep AdvanceLoop8DD1C0(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleEffectOwnedModelStep step = Advance(task, state, resource);
        if (!step.Completed)
        {
            return step;
        }
        task = step.Task.WriteWord(0x138, unchecked((ushort)(step.Task.ReadWord(0x138) + 1)));
        NativeBattleEffectOwnedModelStep restarted = Select(task, step.State, resource, task.ReadByte(0x9C));
        return restarted with { Completed = true };
    }

    public static NativeBattleEffectOwnedModelStep AdvanceOnce8DD220(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleEffectOwnedModelStep step = Advance(task, state, resource);
        return step.Completed ? step with { Task = step.Task.WriteWord(0x138, 0) } : step;
    }

    public static void Validate(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        RequireTask(task);
        resource.PoseSource.Validate();
        if (state.TaskAddress == 0 || state.ResourceAddress != resource.Address || task.ReadDword(0x94) != checked(state.TaskAddress + 0xCC) || task.ReadDword(0x98) != checked(state.TaskAddress + 0xCC) || task.ReadDword(0xB4) != checked(state.TaskAddress + 0xDC) || task.ReadDword(0x10C) != SectionPointer(resource, 3))
        {
            throw new InvalidDataException("Native effect model's raw pointers contradict its symbolic task/resource owners.");
        }
        for (int index = 0; index < 4; index++)
        {
            if (task.ReadDword(0xCC + index * 4) != SectionPointer(resource, index))
            {
                throw new InvalidDataException("Native effect model's section directory changed owner.");
            }
        }
        if (state.DecodedPose is { } decoded && (decoded.Bones.IsDefault || decoded.Bones.Length != resource.PoseSource.BoneCount))
        {
            throw new InvalidDataException("Native effect model lacks its complete current decoded skeleton pose.");
        }
    }

    private static NativeBattleEffectOwnedModelStep Select(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource, short animation)
    {
        RequireNormalSpeed(task);
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        if (animation < 0 || animation >= counts.Length || animation > byte.MaxValue)
        {
            throw new NotSupportedException("Native effect model's signed pose selector has no original animation owner.");
        }
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(resource.PoseSource.Animations.AsSpan(), resource.PoseSource.BoneCount, animation);
        task = task.WriteByte(0x9C, (byte)animation).WriteByte(0x9D, (byte)((task.ReadByte(0x9D) & 0xF3) | 4)).WriteWord(0x9E, first.ByteOffset).WriteWord(0xA0, first.BitOffset).WriteByte(0xA2, 1).WriteByte(0xA3, counts[animation]);
        return new(task, state with { DecodedPose = first.Pose }, false);
    }

    private static NativeBattleEffectOwnedModelStep Advance(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        Validate(task, state, resource);
        RequireNormalSpeed(task);
        NativeBattleDecodedPose current = state.DecodedPose ?? throw new NotSupportedException("Native effect model must select its compressed pose before advancing.");
        int animation = task.ReadByte(0x9C);
        ImmutableArray<byte> counts = NativeBattlePoseDecoder.ReadFrameCounts(resource.PoseSource.Animations.AsSpan());
        if (animation >= counts.Length || task.ReadByte(0xA3) != counts[animation] || task.ReadByte(0xA2) == 0)
        {
            throw new InvalidDataException("Native effect model's current pose counters contradict its source.");
        }
        task = task.WriteWord(0x136, unchecked((ushort)(task.ReadWord(0x136) + 1)));
        if (task.ReadByte(0xA2) >= task.ReadByte(0xA3))
        {
            return new(task, state, true);
        }
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(resource.PoseSource.Animations.AsSpan(), resource.PoseSource.BoneCount, animation, current, task.ReadWord(0x9E), task.ReadWord(0xA0));
        task = task.WriteWord(0x9E, next.ByteOffset).WriteWord(0xA0, next.BitOffset).WriteByte(0xA2, unchecked((byte)(task.ReadByte(0xA2) + 1))).WriteByte(0x9D, (byte)((task.ReadByte(0x9D) & 0xF3) | ((((task.ReadByte(0x9D) >> 2) + 1) & 1) << 2)));
        return new(task, state with { DecodedPose = next.Pose }, false);
    }

    private static uint SectionPointer(NativeBattleEffectOwnedModelResource resource, int index)
    {
        int offset = BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(4 + index * 4, 4));
        bool emptyLast = index == 3 && offset == resource.Bytes.Length && BinaryPrimitives.ReadUInt32LittleEndian(resource.Bytes.AsSpan()) == 4 && BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(20)) == resource.Bytes.Length;
        if (offset < 0 || offset >= resource.Bytes.Length && !emptyLast)
        {
            throw new InvalidDataException("Native effect model section leaves its authenticated resource.");
        }
        return checked(resource.Address + (uint)offset);
    }
    private static void RequireNormalSpeed(NativeBattleModulePoolTask task)
    {
        if ((task.ReadByte(0x9D) & 3) != 0)
        {
            throw new NotSupportedException("Native effect model's reached cursor contract requires normal pose speed.");
        }
    }
    private static void RequireTask(NativeBattleModulePoolTask task)
    {
        if (task.PayloadLength < 0x140 - 12)
        {
            throw new InvalidDataException("Native embedded model requires its complete physical effect task.");
        }
    }
}

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleVector ReadEffectOwnedBone(NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattleActionMatrix world, byte selector, short fraction = 0)
    {
        (Matrix selected, int distance) = ResolveEffectOwnedBone(source, pose, world, selector, fraction);
        return new(unchecked((short)(selected.X + ((long)selected.Rotation[2] * distance >> 12))), unchecked((short)(selected.Y + ((long)selected.Rotation[5] * distance >> 12))), unchecked((short)(selected.Z + ((long)selected.Rotation[8] * distance >> 12))));
    }

    public static NativeBattleBoneAnchorProducer ResolveEffectOwnedBoneAnchor502170(NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattleActionMatrix world, byte selector, short fraction = 0)
    {
        (Matrix selected, int distance) = ResolveEffectOwnedBone(source, pose, world, selector, fraction);
        return new(new(selected.Rotation.ToImmutableArray(), selected.X, selected.Y, selected.Z, 4096), unchecked((short)distance), default);
    }

    private static (Matrix Matrix, int Distance) ResolveEffectOwnedBone(NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattleActionMatrix world, byte selector, short fraction)
    {
        source.Validate();
        world.Validate();
        if (pose.Bones.IsDefault || pose.Bones.Length != source.BoneCount)
        {
            throw new InvalidDataException("Native effect model requires every current decoded bone.");
        }
        Matrix[] bones = BoneMatrices(source, pose);
        int bone = selector;
        if (selector >= 240)
        {
            int offset = selector - 236;
            if (offset >= 16)
            {
                throw new NotSupportedException("Native effect model selector exceeds its skeleton header aliases.");
            }
            bone = source.Skeleton[offset];
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if (bone >= bones.Length)
        {
            throw new InvalidDataException("Native effect model bone selector exceeds its original skeleton.");
        }
        Matrix selected = Compose(new(world.Rotation.ToArray(), world.X, world.Y, world.Z), bones[bone]);
        short parent = ReadWord(source.Skeleton, 16 + bone * 48);
        int distance = parent < 0 ? 0 : fraction * ReadWord(source.Skeleton, 18 + bone * 48) >> 12;
        return (selected, distance);
    }

    public static ImmutableArray<NativeBattleActionMatrix> EffectOwnedGeometryMatrices(NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattleActionMatrix world, NativeBattleActionMatrix view)
    {
        world.Validate();
        view.Validate();
        Matrix modelView = Compose(new(view.Rotation.ToArray(), view.X, view.Y, view.Z), new(world.Rotation.ToArray(), world.X, world.Y, world.Z));
        return ComposeCurrentPose(source, pose, new(modelView.Rotation.ToImmutableArray(), modelView.X, modelView.Y, modelView.Z, 4096));
    }

    public static ImmutableArray<NativeBattleActionMatrix> ComposeCurrentPose(NativeBattlePoseSource source, NativeBattleDecodedPose pose, NativeBattleActionMatrix modelView)
    {
        source.Validate();
        modelView.Validate();
        if (pose.Bones.IsDefault || pose.Bones.Length != source.BoneCount)
        {
            throw new InvalidDataException("Native effect model requires every current decoded bone.");
        }
        Matrix composed = new(modelView.Rotation.ToArray(), modelView.X, modelView.Y, modelView.Z);
        return BoneMatrices(source, pose).Select(bone => Compose(composed, bone)).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
