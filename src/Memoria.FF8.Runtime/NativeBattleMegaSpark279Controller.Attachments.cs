using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    public static (NativeBattleMegaSpark279Frame Frame, int Result) BirthAttachment6804B0(NativeBattleMegaSpark279Frame frame, uint row100, uint firstSource, uint secondSource, byte mode, byte amplitudeShift, byte fadeShift, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x25080C0, 0x680520);
        if (address == 0)
        {
            return (frame, 1);
        }
        var task = Read(frame, address);
        task = CopyTask(task, 16, NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(secondSource, 8).AsSpan()), 8).WriteDword(24, firstSource).WriteDword(28, row100);
        NativeBattleEffectArena fields = NativeBattleEffectArena.Clear().Write(0, new byte[] { 0, mode, amplitudeShift, fadeShift });
        task = CopyTask(task, 12, fields, 4);
        for (int index = 0; index < 8; index++)
        {
            task = task.WriteWord(32 + index * 4, 128).WriteWord(34 + index * 4, 0);
        }
        return (Write(frame, address, task), 1);
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceAttachment680520(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        byte mode = TaskView(task, 13, 1).Read(0, 1)[0];
        uint row100 = task.ReadDword(28);
        uint firstSource = task.ReadDword(24);
        NativeBattleEffectArena center;
        NativeBattleEffectArena displaced;
        NativeBattleEffectArena endpoint;
        int distance;
        if (mode == 0)
        {
            (frame, center, NativeBattleEffectArena normal) = Triangle704130(frame, row100, firstSource, resources, inputs);
            (frame, endpoint) = BonePoint279(frame, row100, TaskView(task, 16, 8), Signed(task, 22), inputs);
            int[] difference = Enumerable.Range(0, 3).Select(axis => Word279(endpoint, axis * 2) - Word279(center, axis * 2)).ToArray();
            (frame, distance, int root) = Length279(frame, difference, resources);
            byte shift = TaskView(task, 14, 1).Read(0, 1)[0];
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadIrColumn(Registers279(frame) with { Colors = Registers279(frame).Colors with { Ir0 = unchecked((uint)(root >> shift)) } }, unchecked((ushort)Word279(normal, 0)), unchecked((ushort)Word279(normal, 2)), unchecked((ushort)Word279(normal, 4)));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
            frame = Publish279(frame, registers);
            displaced = normal;
            for (int axis = 0; axis < 3; axis++)
            {
                displaced = displaced.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(registers.Colors.Ir[axis]!.Value + Word279(center, axis * 2)))));
            }
        }
        else
        {
            NativeBattleEffectArena first = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(firstSource, 8).AsSpan());
            (frame, center) = BonePoint279(frame, row100, first, Word279(first, 6), inputs);
            displaced = center;
            (frame, endpoint) = BonePoint279(frame, row100, TaskView(task, 16, 8), Signed(task, 22), inputs);
            distance = 2048;
        }
        (frame, uint temporary) = Allocate(frame, 128);
        NativeBattleEffectArena rows = Context(frame, temporary, 128);
        rows = NativeBattleEffectNodeContextMemory.Copy(center, 0, rows, 0, 8);
        rows = NativeBattleEffectNodeContextMemory.Copy(endpoint, 0, rows, 112, 8).Write(14, BitConverter.GetBytes(task.ReadWord(32))).Write(126, BitConverter.GetBytes(task.ReadWord(60)));
        for (int index = 1; index < 7; index++)
        {
            int ratio = (index << 12) / 7;
            (frame, NativeBattleEffectArena middle) = Blend279(frame, center, displaced, ratio, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), inputs);
            (frame, NativeBattleEffectArena point) = Blend279(frame, middle, endpoint, ratio, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), inputs);
            rows = NativeBattleEffectNodeContextMemory.Copy(point, 0, rows, index * 16, 6).Write(index * 16 + 14, BitConverter.GetBytes(task.ReadWord(32 + index * 4)));
        }
        for (int index = 1; index < 7; index++)
        {
            int weight = Word279(ReadBytes(frame, 0x20DFAB8 + 0x7C6C + (uint)(index * 2), 2), 0);
            int amplitude = unchecked(weight * distance) >> 12;
            for (int axis = 0; axis < 3; axis++)
            {
                NativeBattleEffectMemory memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort random);
                frame = frame with { Memory = memory };
                int jitter = unchecked((random - 16384) * amplitude) >> 18;
                rows = rows.Write(index * 16 + axis * 2, BitConverter.GetBytes(unchecked((short)(Word279(rows, index * 16 + axis * 2) + jitter))));
            }
        }
        frame = PublishContext(frame, temporary, rows, 128);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        List<uint> addresses = [];
        for (int index = 0; index < 8; index++)
        {
            uint row = temporary + (uint)(index * 16);
            frame = ProjectTemporary279(frame, row, inputs);
            addresses.Add(row);
        }
        frame = DrawTemporaryStrip279(frame, temporary, addresses, resources, inputs, inputs.CapturePresentation ? NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(279, task), firstSource, row100) : null);
        frame = Release(frame, 128);
        task = Read(frame, address);
        byte age = TaskView(task, 12, 1).Read(0, 1)[0];
        if (age >= 10)
        {
            byte shift = TaskView(task, 15, 1).Read(0, 1)[0];
            int surviving = 0;
            for (int index = 0; index < 8; index++)
            {
                int weight = Word279(ReadBytes(frame, 0x20DFAB8 + 0x7C6C + (uint)(index * 2), 2), 0);
                short next = unchecked((short)(Signed(task, 32 + index * 4) - ((5120 - weight) >> shift)));
                if (next >= 0)
                {
                    surviving++;
                }
                task = task.WriteWord(32 + index * 4, unchecked((ushort)Math.Max(0, (int)next)));
            }
            frame = Write(frame, address, task);
            if (surviving <= 1)
            {
                return (frame, 2);
            }
        }
        task = CopyTask(task, 12, NativeBattleEffectArena.Clear().Write(0, new byte[] { unchecked((byte)(age + 1)) }), 1);
        return (Write(frame, address, task), 0);
    }

    private static NativeBattleMegaSpark279Frame ProjectTemporary279(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleEffectArena point = Context(frame, address, 16);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Mega Spark attachment requires its current projection controls.");
        NativeBattleGeometryRegisterStep registers = Registers279(frame);
        NativeBattleVector vector = Vector279(point);
        short? w = Padding279(point);
        registers = NativeBattleGeometry.LoadVector(registers, 0, vector, w);
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
        if (inputs.CapturePresentation)
        {
            NativeBattleActionMatrix matrix = NativeBattleGeometry.CaptureMatrix(registers.Geometry);
            System.Numerics.Vector3 world = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, vector), projection);
            frame = frame with { PresentationPoints = frame.PresentationPoints.SetItem(address, new(world, registers.Geometry.Sxy[2]!.Value)) };
        }
        point = point.Write(0, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value)).Write(4, BitConverter.GetBytes(unchecked((short)((int)registers.Geometry.Sz[3]!.Value >> 2))));
        frame = Publish279(frame, registers);
        return PublishContext(frame, address, point, 16);
    }

    private static NativeBattleMegaSpark279Frame DrawTemporaryStrip279(NativeBattleMegaSpark279Frame frame, uint temporary, List<uint> rows, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        return DrawStrip279(frame, rows, true, resources, inputs, pilotIdentity);
    }
}
