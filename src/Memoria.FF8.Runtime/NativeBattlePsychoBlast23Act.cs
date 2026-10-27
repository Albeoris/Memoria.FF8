using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePsychoBlast23Act
{
    public static NativeBattlePsychoBlast23LeafStep Advance(NativeBattlePsychoBlast23Frame frame, uint address, NativeBattlePsychoBlast23Resources resources, NativeBattlePsychoBlast23Inputs inputs, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        uint callback = task.Callback;
        if (callback is not (NativeBattlePsychoBlast23Profile.ActPrimaryCallback or NativeBattlePsychoBlast23Profile.ActDelayedCallback or NativeBattlePsychoBlast23Profile.ActFollowupCallback))
        {
            throw new InvalidDataException("Psycho Blast ACT requires its original callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase == 0)
        {
            if (callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback || Word(task, 0x24) >= 35)
            {
                NativeBattleGfAnimationSource source = resources.Act(task.ReadDword(0x74));
                int size = unchecked((int)task.ReadDword(0x78));
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, size).Initialize7016B0(source, size);
                task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 0x94, workspace).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                task = Copy(frame.State.Basis56, 0, task, 0x30, 32);
                int position = callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback ? 0x30 : 0x28;
                task = Copy(frame.State.Basis56, position, task, 0x1C, 8);
                if (callback == NativeBattlePsychoBlast23Profile.ActDelayedCallback)
                {
                    task = task.WriteDword(0x16C, task.ReadDword(0x1C)).WriteDword(0x170, task.ReadDword(0x20));
                    task = ComputeEnd(task, frame.State.Basis56, 0x174);
                }
                if (callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback)
                {
                    frame = frame with { State = frame.State.WithWord(0x48, 1) };
                }
                frame = Draw(Put(frame, address, task), address, inputs);
                frame = Put(frame, address, Next(Read(frame, address)));
            }
        }
        else if (callback == NativeBattlePsychoBlast23Profile.ActDelayedCallback && phase == 1)
        {
            task = task.WriteWord(0x17C, unchecked((ushort)(task.ReadWord(0x17C) + 0x556)));
            if (Word(task, 0x17C) >= 4096)
            {
                task = Copy(frame.State.Basis56, 0x28, task, 0x1C, 8).WriteWord(0x17C, 4096);
                task = ComputeEnd(task, frame.State.Basis56, 0x1C);
                frame = Put(frame, address, task);
                frame = Append(frame, address, NativeBattlePsychoBlast23Profile.ActFollowupCallback, 0x17F1744, 0xD8, 2, selectActors, out uint child);
                NativeBattleModulePoolTask followup = Read(frame, child).WriteDword(0x1C, task.ReadDword(0x1C)).WriteDword(0x20, task.ReadDword(0x20));
                frame = Put(frame, child, followup);
                NativeBattleEffectHierarchyAppend debris = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, NativeBattlePsychoBlast23Profile.DebrisEmitterCallback, 176, address, selectActors);
                frame = frame with { Effect = frame.Effect with { Pools = debris.Hierarchy.Pools } };
                frame = Put(frame, address, Next(Read(frame, address)));
            }
            else
            {
                int factor = Word(task, 0x17C);
                for (int axis = 0; axis < 3; axis++)
                {
                    int start = Word(task, 0x16C + axis * 2);
                    int end = Word(task, 0x174 + axis * 2);
                    task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)(start + unchecked((end - start) * factor) / 4096)));
                }
                frame = Put(frame, address, task);
            }
            frame = Draw(frame, address, inputs);
        }
        else if (callback == NativeBattlePsychoBlast23Profile.ActDelayedCallback && phase == 2)
        {
            int scale = unchecked((int)task.ReadDword(0x58));
            if (scale == 4096)
            {
                task = task.WriteDword(0x58, 2048);
            }
            else if (scale == 2048)
            {
                task = task.WriteDword(0x58, 1024);
            }
            else if (scale == 1024)
            {
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            }
            task = ComputeEnd(task, frame.State.Basis56, 0x1C);
            frame = Draw(Put(frame, address, task), address, inputs);
        }
        else if (phase == 1)
        {
            frame = Draw(frame, address, inputs);
            task = Read(frame, address);
            NativeBattleGfAnimationSource source = resources.Act(task.ReadDword(0x74));
            int table = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4));
            int duration = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(table + 4));
            if (unchecked(duration - (int)task.ReadDword(0x98)) == 0)
            {
                frame = Put(frame, address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
            }
        }
        else if (phase != (callback == NativeBattlePsychoBlast23Profile.ActDelayedCallback ? 3 : 2))
        {
            throw new InvalidDataException("Psycho Blast ACT signed phase leaves its original handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattlePsychoBlast23Frame Append(NativeBattlePsychoBlast23Frame frame, uint parent, uint callback, uint source, int workspaceLength, short selector, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors, out uint address)
    {
        NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(4, callback, 384, parent, selectActors);
        address = append.Address;
        NativeBattleModulePoolTask task = append.Hierarchy.Read(address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)workspaceLength)).WriteWord(0x80, unchecked((ushort)selector)).WriteWord(0x82, 0);
        return frame with { Effect = frame.Effect with { Pools = append.Hierarchy.Write(address, task).Pools } };
    }

    public static NativeBattleModulePoolTask ComputeEnd(NativeBattleModulePoolTask task, NativeBattleEffectArena basis, int destination)
    {
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(ReadMatrix(task, 0x30), unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(scaled, new(0, 0, -512));
        return task.WriteWord(destination, unchecked((ushort)(ArenaWord(basis, 0x30) + delta.X))).WriteWord(destination + 2, unchecked((ushort)(ArenaWord(basis, 0x32) + delta.Y))).WriteWord(destination + 4, unchecked((ushort)(ArenaWord(basis, 0x34) + delta.Z)));
    }

    internal static NativeBattlePsychoBlast23LeafStep Finish(NativeBattlePsychoBlast23Frame frame, uint address)
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

    internal static NativeBattleModulePoolTask Read(NativeBattlePsychoBlast23Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    internal static NativeBattlePsychoBlast23Frame Put(NativeBattlePsychoBlast23Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    internal static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    internal static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static short ArenaWord(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    internal static NativeBattleActionMatrix ReadMatrix(NativeBattleModulePoolTask task, int offset) => new(Enumerable.Range(0, 9).Select(index => Word(task, offset + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(offset + 20)), unchecked((int)task.ReadDword(offset + 24)), unchecked((int)task.ReadDword(offset + 28)), 4096);
    internal static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena arena, int offset) => new(Enumerable.Range(0, 9).Select(index => ArenaWord(arena, offset + index * 2)).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 20, 4)), BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 24, 4)), BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset + 28, 4)), 4096);
    internal static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
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
    private static NativeBattlePsychoBlast23Frame Draw(NativeBattlePsychoBlast23Frame frame, uint address, NativeBattlePsychoBlast23Inputs inputs) => inputs.Execute(frame, new NativeBattlePsychoBlast23ActDraw(address));
}
