using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170Controller
{
    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceSprite(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DCF20)
        {
            throw new NotSupportedException("Acid curve sprite callback is unsupported.");
        }
        RequireCurveIndex(task);
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xD68DF8u)).Write(0x24, new byte[2]).Write(4, BitConverter.GetBytes(unchecked((short)(Signed(task, 12) % 6)))), 180);
        int point = Signed(task, 24);
        int size = Signed(task, 28);
        for (int segment = 0; segment < 4; segment++, point--, size -= 112)
        {
            if (point < 0)
            {
                break;
            }
            if (point >= 28)
            {
                continue;
            }
            frame = SetupSprite170(frame, inputs, frame.State.ReadView(0x23B1008 + (uint)(point * 8), 8), unchecked((short)size), unchecked((short)-(size >> 3)));
            frame = inputs.Execute(frame, new NativeBattleAcid170DrawSprite180(temporary, 0xD68DF8));
        }
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short speed = Signed(task, 26);
        short next = unchecked((short)(Signed(task, 24) + speed));
        if (speed > 2 && next < 14)
        {
            speed--;
        }
        task = task.WriteWord(24, unchecked((ushort)next)).WriteWord(26, unchecked((ushort)speed)).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), next >= 32 ? 2 : 0);
    }

    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceMesh(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool selected = task.Callback == 0x5DD030;
        if (!selected && task.Callback != 0x5DE0B0)
        {
            throw new NotSupportedException("Acid mesh callback is unsupported.");
        }
        if (!selected && Signed(task, 14) > 0)
        {
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
            }
            return (frame, 0);
        }
        if (selected)
        {
            RequireCurveIndex(task);
        }
        int scale = Signed(task, 28);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 24), 0)), scale, scale, scale) with { X = Signed(task, 16), Y = Signed(task, 18), Z = Signed(task, 20) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        short age = Signed(task, 12);
        if (selected)
        {
            int ratio = NativeBattleCameraMath.Sine(unchecked(age << 10) / 32);
            for (int vertex = 0; vertex < 73; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int offset = vertex * 8 + axis * 2;
                    short first = BinaryPrimitives.ReadInt16LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(8 + offset));
                    short second = BinaryPrimitives.ReadInt16LittleEndian(resources.EndpointVertices.AsSpan().Slice(offset));
                    frame = PutWord170(frame, 0x23B10F0 + (uint)offset, unchecked(first + (unchecked((second - first) * ratio) >> 12)));
                }
            }
        }
        (frame, uint temporary) = Allocate(frame, selected ? 92 : 88);
        int length = selected ? 92 : 88;
        uint source = selected ? 0xD6904Cu : 0xD69CBCu;
        uint flags = selected ? age < 8 || age >= 32 ? 0x2033u : age < 16 ? 0x20F3u : 0x24F3u : 0xF3u;
        int cue = selected ? age < 8 ? 0 : age < 16 ? (age - 8) << 9 : age < 24 ? (24 - age) << 9 : (age - 24) << 9 : age < 8 ? 2048 : age << 8;
        NativeBattleEffectArena context = Context(frame, temporary, length).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags));
        if (!selected || age >= 8 && age < 32)
        {
            context = context.Write(12, BitConverter.GetBytes(cue));
        }
        if (selected)
        {
            context = context.Write(4, BitConverter.GetBytes(0x23B10F0u));
            if (age >= 16 && age < 32)
            {
                context = context.Write(0x10, BitConverter.GetBytes(32));
            }
        }
        frame = PublishContext(frame, temporary, context, length);
        if (!selected || age < 32)
        {
            frame = inputs.Execute(frame, selected ? new NativeBattleAcid170DrawMesh92(temporary, source) : new NativeBattleAcid170DrawStatic88(temporary, source));
        }
        frame = Release(frame, length);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (!selected || age < 8)
        {
            short speed = Signed(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(Signed(task, 28) + speed)));
            if (selected)
            {
                task = task.WriteWord(30, unchecked((ushort)(speed - speed / 2)));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (selected ? 32 : 16) ? 2 : 0);
    }

    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceBody(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DE200)
        {
            throw new NotSupportedException("Acid body callback is unsupported.");
        }
        RequireCurveIndex(task);
        short age = Signed(task, 12);
        int actor = checked((int)frame.State.ReadDword(0x23AF820));
        if (age < 34)
        {
            short startX = Word170(frame, 0x23B0FD8);
            short startY = Word170(frame, 0x23B0FDA);
            short startZ = Word170(frame, 0x23B0FDC);
            short targetX = Word170(frame, 0x23B0FE0);
            short targetY = Word170(frame, 0x23B0FE2);
            short targetZ = Word170(frame, 0x23B0FE4);
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(startX - targetX, startY - targetY, startZ - targetZ);
            short nx = unchecked((short)normal.X);
            short ny = unchecked((short)normal.Y);
            short nz = unchecked((short)normal.Z);
            (frame, uint temporary) = Allocate(frame, 172);
            int plane = unchecked(-(unchecked(targetX * normal.X + targetY * normal.Y + targetZ * normal.Z) >> 12));
            NativeBattleEffectArena context = Context(frame, temporary, 172).Write(0x74, BitConverter.GetBytes(nx)).Write(0x76, BitConverter.GetBytes(ny)).Write(0x78, BitConverter.GetBytes(nz)).Write(0x7A, BitConverter.GetBytes(unchecked((short)plane))).Write(0x30, BitConverter.GetBytes(unchecked(targetX * nx + targetY * ny + targetZ * nz))).Write(0x34, BitConverter.GetBytes(-(int)ny)).Write(0x3C, BitConverter.GetBytes(age < 28 ? 81 * age : 12379 - 364 * age)).Write(0x40, BitConverter.GetBytes(6250000));
            context = NativeBattleEffectNodeContextMemory.Copy(frame.State.ReadView(0x23B0FE0, 8), 0, context, 0x7C, 8);
            context = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 16, 8), 0, context, 0x84, 8);
            frame = PublishContext(frame, temporary, context, 172);
            frame = inputs.Execute(frame, new NativeBattleAcid170ActorFlags(actor, 0, 4));
            frame = inputs.Execute(frame, new NativeBattleAcid170DrawBody172(actor, temporary));
            frame = Release(frame, 172);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) > 34)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid170ActorFlags(actor, 4, task.ReadWord(26)));
            return (frame, 2);
        }
        return (frame, 0);
    }

    private static NativeBattleAcid170Frame SetupSprite170(NativeBattleAcid170Frame frame, NativeBattleAcid170Inputs inputs, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), xyz, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null, size, depth) };
    }
}
