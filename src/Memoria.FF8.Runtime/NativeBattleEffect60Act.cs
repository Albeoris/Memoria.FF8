using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect60Act
{
    public static NativeBattleEffect60LeafStep Advance(NativeBattleEffect60Frame frame, uint address, NativeBattleEffect60Resources resources, NativeBattleEffect60Inputs inputs, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool primary = task.Callback == NativeBattleEffect60Profile.ActPrimaryCallback;
        if (!primary && task.Callback != NativeBattleEffect60Profile.ActSecondaryCallback)
        {
            throw new InvalidDataException("Effect60 ACT requires its original callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (primary && phase == 0)
        {
            if (Word(task, 0x24) >= 6)
            {
                int actor = task.ReadByte(0x2D);
                NativeBattleEffectArena packed = RequirePacked8(inputs.ReadPackedPosition8(actor));
                NativeBattleEffectArena rotation = RequirePacked8(inputs.ReadRotation8(actor));
                frame = frame with { State = frame.State with { SavedRotation8 = rotation } };
                task = Copy(packed, 0, task, 0x180, 8);
                short yaw = (short)((frame.State.Word(0x4A) - 0x800) & 0xFFF);
                frame = inputs.Execute(Put(frame, address, task), new NativeBattleEffect60SetYaw(actor, yaw));
                NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
                frame = inputs.Execute(frame, new NativeBattleEffect60Rotate(identity, 'Y', yaw, null));
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw), new(0, 0, -512));
                packed = WriteWord(packed, 0, ArenaWord(packed, 0) + offset.X);
                packed = WriteWord(packed, 2, ArenaWord(packed, 2) + offset.Y);
                packed = WriteWord(packed, 4, ArenaWord(packed, 4) + offset.Z);
                frame = inputs.Execute(frame, new NativeBattleEffect60SetPackedPosition(actor, packed));
                task = Copy(packed, 0, Read(frame, address), 0x178, 8);
                frame = Put(frame, address, Next(task));
            }
        }
        else if (primary && phase == 1)
        {
            if (Word(task, 0x24) >= 36)
            {
                task = Initialize(task, resources).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, unchecked((ushort)-512));
                task = Copy(inputs.ReadWorld32(task.ReadByte(0x2C)), task, 0x30);
                frame = Anchor(Put(frame, address, task), address, task.ReadByte(0x2C), 0x19, 1024, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect60ActDraw(address));
                frame = Append(frame, address, NativeBattleEffect60Profile.ActSecondaryCallback, 0x17A3150, 36, 1, selectActors, out _);
                frame = Put(frame, address, Next(Read(frame, address).WriteWord(0x84, 0)));
            }
        }
        else if (primary && phase == 2)
        {
            frame = MoveTarget(frame, address, resources, inputs);
        }
        else if (!primary && phase == 0)
        {
            int actor = task.ReadByte(0x2D);
            task = Initialize(task, resources).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, unchecked((ushort)-512));
            task = Copy(inputs.ReadWorld32(actor), task, 0x30);
            NativeBattleActionMatrix matrix = ReadMatrix(task, 0x30);
            short? padding = Padding(task, 0x42);
            frame = inputs.Execute(Put(frame, address, task), new NativeBattleEffect60Rotate(matrix, 'Y', 2048, padding));
            matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, 2048);
            task = Read(frame, address);
            for (int index = 0; index < 9; index++)
            {
                task = task.WriteWord(0x30 + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            frame = Anchor(Put(frame, address, task), address, actor, 0xF1, 0, inputs);
            frame = Put(frame, address, Read(frame, address).WriteWord(0x1E, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect60ActDraw(address));
            frame = Put(frame, address, Next(Read(frame, address)));
        }
        else if (!primary && phase == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect60ActDraw(address));
            task = Read(frame, address);
            if (Remaining(task, resources) == 0)
            {
                frame = Put(frame, address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
            }
        }
        else if (phase != (primary ? 3 : 2))
        {
            throw new InvalidDataException("Effect60 ACT signed phase leaves its original handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect60Frame MoveTarget(NativeBattleEffect60Frame frame, uint address, NativeBattleEffect60Resources resources, NativeBattleEffect60Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = task.ReadByte(0x2D);
        NativeBattleEffectArena previous = RequirePacked8(inputs.ReadPackedPosition8(actor));
        NativeBattleEffectArena next = previous;
        int index = Word(task, 0x84);
        if ((uint)index >= (uint)resources.TargetMotionRatios.Length)
        {
            throw new InvalidDataException("Effect60 target motion leaves its original ratio table.");
        }
        short ratio = resources.TargetMotionRatios[index];
        task = task.WriteWord(0x188, unchecked((ushort)ratio));
        if (ratio == 4096)
        {
            next = Capture(task, 0x180, 8);
        }
        else
        {
            task = task.WriteWord(0x84, unchecked((ushort)(index + 1)));
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = axis * 2;
                int start = Word(task, 0x178 + offset);
                int end = Word(task, 0x180 + offset);
                next = WriteWord(next, offset, start + unchecked((end - start) * ratio) / 4096);
            }
        }
        frame = inputs.Execute(Put(frame, address, task), new NativeBattleEffect60SetPackedPosition(actor, next));
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            int offset = axis * 2;
            int delta = ArenaWord(next, offset) - ArenaWord(previous, offset);
            task = task.WriteWord(0x1C + offset, unchecked((ushort)(Word(task, 0x1C + offset) + unchecked(delta * 3) / 2)));
        }
        frame = inputs.Execute(Put(frame, address, task), new NativeBattleEffect60ActDraw(address));
        task = Read(frame, address);
        if (Remaining(task, resources) == 0)
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            frame = inputs.Execute(Put(frame, address, task), new NativeBattleEffect60RestoreRotation(actor, frame.State.SavedRotation8));
            frame = Put(frame, address, Next(Read(frame, address)));
        }
        return frame;
    }

    public static NativeBattleEffect60Frame Append(NativeBattleEffect60Frame frame, uint parent, uint callback, uint source, int size, short selector, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors, out uint address)
    {
        NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(4, callback, 396, parent, selectActors);
        address = append.Address;
        NativeBattleModulePoolTask task = append.Hierarchy.Read(address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)size)).WriteWord(0x80, unchecked((ushort)selector)).WriteWord(0x82, 0);
        return frame with { Effect = frame.Effect with { Pools = append.Hierarchy.Write(address, task).Pools } };
    }

    private static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, NativeBattleEffect60Resources resources)
    {
        int length = unchecked((int)task.ReadDword(0x78));
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(task.ReadBytes(0x94, length).ToImmutableArray()).Initialize7016B0(resources.Act(task.ReadDword(0x74)), length);
        return task.WriteBytes(0x94, workspace.Data.AsSpan());
    }
    private static int Remaining(NativeBattleModulePoolTask task, NativeBattleEffect60Resources resources)
    {
        NativeBattleGfAnimationSource source = resources.Act(task.ReadDword(0x74));
        int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
        return unchecked(BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)) - (int)task.ReadDword(0x98));
    }
    private static NativeBattleEffect60Frame Anchor(NativeBattleEffect60Frame frame, uint address, int actor, byte selector, short distance, NativeBattleEffect60Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect60TargetAnchor(address, actor, selector, distance, 0x1C));
        NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(actor), inputs.ReadPoseSource(actor), selector, distance);
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y)).WriteWord(0x20, unchecked((ushort)point.Z));
        return Put(frame, address, task);
    }

    internal static NativeBattleEffect60LeafStep Finish(NativeBattleEffect60Frame frame, uint address)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);
    }
    internal static NativeBattleModulePoolTask Read(NativeBattleEffect60Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    internal static NativeBattleEffect60Frame Put(NativeBattleEffect60Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    internal static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    internal static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static short ArenaWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    internal static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => Word(task, offset + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
    internal static NativeBattleEffectArena RequirePacked8(NativeBattleEffectArena arena)
    {
        arena.Validate();
        if (arena.Regions.Any(region => region.Offset > 8 - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > 8 - region.Length))
        {
            throw new InvalidDataException("Effect60 packed model field exceeds its actual eight bytes.");
        }
        return arena;
    }
    internal static NativeBattleEffectArena WriteWord(NativeBattleEffectArena arena, int offset, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)value));
        return arena.Write(offset, bytes);
    }
    internal static NativeBattleEffectArena Capture(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear();
        for (int index = 0; index < length; index++)
        {
            if (task.UnknownPayloadRanges is { } ranges && ranges.Any(range => offset + index >= range.Offset && offset + index < range.Offset + range.Length))
            {
                result = result.MarkUnknown(index, 1);
            }
            else
            {
                result = result.Write(index, new byte[] { task.ReadByte(offset + index) });
            }
        }
        return result;
    }
    internal static NativeBattleModulePoolTask Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleModulePoolTask task, int destination, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(sourceOffset, length);
        for (int index = 0; index < length; index++)
        {
            task = known[index] ? task.WriteByte(destination + index, bytes[index]) : task.MarkPayloadUnknown(destination + index, 1);
        }
        return task;
    }
    internal static NativeBattleModulePoolTask Copy(NativeBattlePackedMatrix32 matrix, NativeBattleModulePoolTask task, int destination)
    {
        matrix.Validate();
        for (int index = 0; index < 32; index++)
        {
            task = matrix.Known[index] ? task.WriteByte(destination + index, matrix.Bytes[index]) : task.MarkPayloadUnknown(destination + index, 1);
        }
        return task;
    }
    private static short? Padding(NativeBattleModulePoolTask task, int offset)
    {
        if (task.UnknownPayloadRanges is { } ranges && ranges.Any(range => range.Offset < offset + 2 && range.Offset + range.Length > offset))
        {
            return null;
        }
        return Word(task, offset);
    }
}
