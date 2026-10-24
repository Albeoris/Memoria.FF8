using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int NativeEffect287Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect287 requires an actual physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private NativeBattleResourcePointer NativeEffect287SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : RequireStaging(frame).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect287 requires its actual staged skeleton directory pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect287 requires its current CPU resource owner.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect287 requires its current physical resource row.")].Require(12), null);
    }

    private uint NativeEffect287SkeletonAddress(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, int actor)
    {
        NativeBattleResourcePointer pointer = NativeEffect287SkeletonPointer(graphics, frame, actor);
        return pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect287 requires its actual addressed skeleton owner.");
    }

    private (byte[] Bytes, bool[] Known) ReadNativeEffect287SkeletonSnapshot(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, int actor, int offset, int length)
    {
        NativeBattleResourcePointer pointer = NativeEffect287SkeletonPointer(graphics, frame, actor);
        if (pointer.Cpu is { Bank: < 3 } cpu)
        {
            NativeBattleModelCpuBank bank = (NativeModelCpuResources ?? throw new NotSupportedException("Effect287 requires its current CPU bank owner.")).Cpu.Banks[cpu.Bank];
            NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
            int first = checked(cpu.Offset + offset);
            foreach (NativeBattleEffectMemoryRegion region in bank.KnownRanges)
            {
                int start = Math.Max(first, region.Offset);
                int end = Math.Min(first + length, region.Offset + region.Data.Length);
                if (start < end)
                {
                    output = output.Write(start - first, region.Data.AsSpan().Slice(start - region.Offset, end - start));
                }
            }
            return output.ReadSnapshot(0, length);
        }
        uint address = checked((pointer.Cpu?.SharedAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect287 requires its actual shared skeleton owner.")) + (uint)offset);
        return ReadBytes(frame, address, length).ReadSnapshot(0, length);
    }

    private NativeBattleEffectArena CaptureNativeEffect287Model(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, uint address)
    {
        NativeBattleGraphicsModel model = graphics.Models[NativeEffect287Actor(graphics, address)];
        _ = NativeEffect287SkeletonAddress(graphics, frame, model.Index);
        return CaptureNativeEffect210Model(model).Write(0x64, BitConverter.GetBytes(NativeBattleModelResourceRow.Address100(model)));
    }

    private NativeBattlePackedMatrix32 ReadNativeEffect287BoneMatrix(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, uint directory, int bone)
    {
        NativeBattleGraphicsModel[] owners = graphics.Models.Where(model => (model.ActiveResourceRow100 is not null || model.ActiveStagedResourceRow100 is not null) && NativeBattleModelResourceRow.Address100(model) == directory).ToArray();
        if (owners.Length != 1)
        {
            throw new NotSupportedException("Effect287 bone access requires its unique current physical directory owner.");
        }
        NativeBattleGraphicsModel owner = owners[0];
        NativeFileStaging = RequireStaging(frame);
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, owner.Index).PoseSource ?? throw new NotSupportedException("Effect287 requires its current skeleton definition.");
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(owner, source);
        if ((uint)bone >= bones.Length)
        {
            throw new InvalidDataException("Effect287 bone selector leaves its current skeleton.");
        }
        (byte[] bytes, bool[] known) = ReadNativeEffect287SkeletonSnapshot(graphics, frame, owner.Index, 32 + bone * 48 + 18, 2);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(bones[bone], null);
        return packed with { Bytes = packed.Bytes.SetItem(18, bytes[0]).SetItem(19, bytes[1]), Known = packed.Known.SetItem(18, known[0]).SetItem(19, known[1]) };
    }

    private NativeBattleEffect287MacStep NativeEffect287PointMac(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, NativeBattleEffectArena point8, NativeBattleEffectArena retained16)
    {
        uint model = frame.State.ReadDword(NativeBattleEffect287Profile.CasterGlobal);
        NativeBattleGraphicsModel owner = graphics.Models[NativeEffect287Actor(graphics, model)];
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point8.Read(6, 2));
        frame = LoadMatrix(frame, ReadNativeEffect287BoneMatrix(graphics, frame, NativeBattleModelResourceRow.Address100(owner), bone));
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadVector(Registers(frame), 0, new(BinaryPrimitives.ReadInt16LittleEndian(point8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point8.Read(4, 2))), unchecked((short)bone));
        step = NativeBattleGeometry.Transform(step);
        for (int axis = 0; axis < 3; axis++)
        {
            retained16 = retained16.Write(axis * 4, BitConverter.GetBytes(step.Colors.Mac[axis] ?? throw new NotSupportedException("Effect287 point transform requires its current full MAC output.")));
        }
        return new(Publish(frame, step), retained16);
    }

    private NativeBattleEffect287PointStep NativeEffect287LocalPoint(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, uint address, int selector, NativeBattleEffectArena retained8)
    {
        int actor = NativeEffect287Actor(graphics, address);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeFileStaging = RequireStaging(frame);
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect287 local point requires its current skeleton.");
        _ = NativeBattleBodyBoneMatrices.Require(model, source);
        int bone = selector;
        if (selector >= 0xF0)
        {
            int offset = checked(selector - 0xEC);
            if ((uint)offset >= 16)
            {
                throw new NotSupportedException("Effect287 local selector leaves its actual skeleton header.");
            }
            bone = offset == 4 ? model.SequenceState?.Geometry4 ?? source.Skeleton[offset] : offset == 5 ? model.SequenceState?.Geometry5 ?? source.Skeleton[offset] : source.Skeleton[offset];
        }
        if ((uint)bone >= source.BoneCount)
        {
            throw new InvalidDataException("Effect287 local selector leaves its current bone directory.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        if (parent >= source.BoneCount)
        {
            throw new InvalidDataException("Effect287 local point parent leaves its current skeleton.");
        }
        short length = parent < 0 ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + parent * 48, 2));
        NativeBattlePackedMatrix32 matrix = ReadNativeEffect287BoneMatrix(graphics, frame, NativeBattleModelResourceRow.Address100(model), bone);
        (frame, uint temporary) = Allocate(frame, 60);
        NativeBattleEffectArena context = Context(frame, temporary, 60).Write(0x34, BitConverter.GetBytes(0u)).Write(0x38, BitConverter.GetBytes(length));
        frame = LoadMatrix(frame, matrix);
        (byte[] input, bool[] inputKnown) = context.ReadSnapshot(0x34, 8);
        NativeBattleGeometryRegisterStep step = Registers(frame);
        step = step with { Colors = step.Colors with { NormalInput = input.ToImmutableArray(), NormalInputKnown = inputKnown.ToImmutableArray() } };
        step = NativeBattleGeometry.Transform(step);
        for (int axis = 0; axis < 3; axis++)
        {
            int mac = step.Colors.Mac[axis] ?? throw new NotSupportedException("Effect287 local point requires its current full MAC output.");
            context = context.Write(0x20 + axis * 4, BitConverter.GetBytes(mac));
            retained8 = retained8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)mac)));
        }
        context = context.Write(0x30, BitConverter.GetBytes(step.Colors.Flags ?? throw new NotSupportedException("Effect287 local point requires its current flags.")));
        frame = PublishContext(Publish(frame, step), temporary, context, 60);
        return new(Release(frame, 60), retained8);
    }

    private NativeBattleEffect287PointStep NativeEffect287Anchor(NativeBattleGraphicsState graphics, NativeBattleEffect287Frame frame, uint address, NativeBattleEffectArena retained8)
    {
        int actor = NativeEffect287Actor(graphics, address);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect287 anchor requires its current model transform.");
        if ((model.Flags0 & 2) == 0)
        {
            return new(frame, retained8.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
        NativeFileStaging = RequireStaging(frame);
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect287 anchor requires its current skeleton.");
        NativeBattleEffectArena first = retained8;
        NativeBattleEffectArena second = retained8;
        foreach (byte selector in new byte[] { 0xF0, 0xF1 })
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0);
            frame = frame with { Memory = ObserveNativeBoneAnchor(frame.Memory, producer), Staging = NativeFileStaging };
            for (int axis = 0; axis < 3; axis++)
            {
                short value = unchecked((short)(frame.Memory.ScriptedColorRegisters!.Mac[axis] ?? throw new NotSupportedException("Effect287 anchor requires its current MAC output.")));
                if (selector == 0xF0)
                {
                    first = first.Write(axis * 2, BitConverter.GetBytes(value));
                }
                else
                {
                    second = second.Write(axis * 2, BitConverter.GetBytes(value));
                }
            }
        }
        for (int axis = 0; axis < 3; axis++)
        {
            int left = BinaryPrimitives.ReadInt16LittleEndian(first.Read(axis * 2, 2));
            int right = BinaryPrimitives.ReadInt16LittleEndian(second.Read(axis * 2, 2));
            retained8 = retained8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
        }
        return new(frame, retained8.Write(6, BitConverter.GetBytes(transform.Angle36)));
    }
}
