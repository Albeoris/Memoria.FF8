using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Controller;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Trail
{
    public static (NativeBattleDisolvingAcid132Frame Frame, uint Point) Create6B1EC0(NativeBattleDisolvingAcid132Frame frame, NativeBattleEffectArena start, NativeBattleEffectArena end, short actor, short width, NativeBattleEffectArena direction, NativeBattleEffectArena sourceVector, NativeBattleActionMatrix casterWorld)
    {
        (frame, uint point) = AllocatePoint(frame, width);
        if (point == 0)
        {
            return (frame, 0);
        }
        (frame, uint address) = Append(frame, 4, 0x6B2030);
        if (address == 0)
        {
            return (Word(frame, point + 6, ushort.MaxValue), 0);
        }
        frame = Word(frame, point + 14, 0);
        short parity = Signed(sourceVector, 6);
        if (parity == 0)
        {
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(casterWorld, new(unchecked((short)(Signed(sourceVector, 0) >> 1)), 0, 0));
            end = end.Write(0, BitConverter.GetBytes(unchecked((short)(Signed(end, 0) + offset.X)))).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes(unchecked((short)(Signed(end, 4) + offset.Z))));
            end = end.Write(6, BitConverter.GetBytes((ushort)1));
        }
        else
        {
            end = end.Write(6, BitConverter.GetBytes((ushort)0));
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0).WriteDword(16, point).WriteDword(20, point);
        task = Copy(task, 24, start);
        task = Copy(task, 32, start);
        task = task.WriteWord(32, unchecked((ushort)(Signed(task, 32) + (Signed(direction, 0) >> 1)))).WriteWord(34, unchecked((ushort)(Signed(task, 34) + Signed(direction, 2)))).WriteWord(36, unchecked((ushort)(Signed(task, 36) + (Signed(direction, 4) >> 1))));
        task = Copy(task, 40, end);
        task = task.WriteWord(30, 0).WriteWord(14, 1).WriteWord(56, unchecked((ushort)actor)).WriteWord(58, unchecked((ushort)width)).WriteWord(60, 8);
        return (Write(frame, address, task), point);
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) Advance6B2030(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool exhausted = false;
        if (Signed(task, 60) > 0)
        {
            (frame, uint point) = AllocatePoint(frame, Signed(task, 58));
            if (point == 0)
            {
                task = task.WriteWord(30, 4096);
                frame = Write(frame, address, task);
                exhausted = true;
            }
            else
            {
                frame = Word(frame, point + 14, task.ReadWord(14));
                frame = Dword(frame, task.ReadDword(20) + 24, point);
                task = task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))).WriteWord(60, unchecked((ushort)(task.ReadWord(60) - 1))).WriteDword(20, point);
            }
        }
        if (!exhausted)
        {
            task = task.WriteWord(30, unchecked((ushort)(task.ReadWord(30) + 256)));
            frame = Write(frame, address, task);
            int index = Signed(task, 30) / 256;
            if ((uint)index >= resources.Envelope.Length)
            {
                throw new NotSupportedException("Disolving Acid requires its actual current curve envelope row.");
            }
            int factor = resources.Envelope[index];
            foreach (uint point in Chain(frame, task.ReadDword(16)))
            {
                factor = unchecked(factor - 192);
                task = Read(frame, address);
                if (factor > 4224 && point == task.ReadDword(16))
                {
                    if (PointSigned(frame, point + 14) == 0)
                    {
                        NativeBattleEffectArena target = TaskView(task, 40, 8);
                        frame = Signed(task, 46) == 0 ? CreatePrimaryAct(frame, target, resources) : CreateImpactAct(frame, target, resources);
                    }
                    uint next = PointDword(frame, point + 24);
                    frame = Word(frame, point + 6, ushort.MaxValue);
                    task = Read(frame, address).WriteWord(14, unchecked((ushort)(Read(frame, address).ReadWord(14) - 1))).WriteDword(16, next);
                    frame = Write(frame, address, task);
                    if (Signed(task, 14) < 2)
                    {
                        return (frame, 2);
                    }
                }
                if (factor >= 4224)
                {
                    factor = 4096;
                }
                NativeBattleGeometryRegisterStep registers = Registers(frame);
                registers = Blend(registers, Vector(task, 24), Vector(task, 32), factor, inputs.Colors);
                NativeBattleVector middle = Ir(registers);
                registers = Blend(registers, middle, Vector(task, 40), factor, inputs.Colors);
                frame = Publish(frame, registers);
                frame = WriteArena(frame, point, Xyz(Ir(registers)));
            }
            for (int attempt = 0; attempt < 3; attempt++)
            {
                frame = DrawRandom(frame, out ushort selector);
                uint[] chain = Chain(frame, Read(frame, address).ReadDword(16)).ToArray();
                for (int segment = 0; segment < chain.Length - 1; segment++)
                {
                    uint point = chain[segment];
                    uint next = chain[segment + 1];
                    if (PointSigned(frame, point + 14) != (selector & 3))
                    {
                        continue;
                    }
                    (frame, uint child) = Append(frame, 5, 0x6B2310);
                    if (child == 0)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask sprite = CopyTask(Read(frame, child), 12, NativeBattleEffectNodeContextMemory.Copy(Arena(frame), Offset(point, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), 8).WriteWord(18, 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort random);
                        int delta = (PointSigned(frame, next + (uint)(axis * 2)) - PointSigned(frame, point + (uint)(axis * 2))) >> 1;
                        sprite = sprite.WriteWord(20 + axis * 2, unchecked((ushort)((random & 127) + delta - 64)));
                    }
                    frame = Write(frame, child, sprite);
                }
            }
        }
        frame = LoadMatrix(frame, inputs.ReadCamera(frame));
        Dictionary<uint, System.Numerics.Vector3>? world = inputs.CapturePresentation ? [] : null;
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Disolving Acid requires its actual projection controls.");
        foreach (uint point in Chain(frame, Read(frame, address).ReadDword(16)))
        {
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, PointVector(frame, point), PointSigned(frame, point + 6));
            if (world is not null)
            {
                world[point] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(NativeBattleGeometry.CaptureMatrix(registers.Geometry), PointVector(frame, point)), inputs.ReadProjection(frame));
            }
            registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            frame = Publish(frame, registers);
            frame = Dword(frame, point + 8, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Disolving Acid requires its projected point."));
            frame = Word(frame, point + 12, unchecked((ushort)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Disolving Acid requires its projected depth.")) >> 2)));
        }
        task = Read(frame, address);
        if (Signed(task, 14) >= 2)
        {
            (frame, NativeBattleGfGeometryBatch batch) = DrawRibbon6B2870(frame, task.ReadDword(16), Signed(task, 14), resources, world is null ? null : NativeBattlePilotVisualLifetimes.Identity(132, Read(frame, address)), world);
            frame = inputs.Execute(frame, new NativeBattleDisolvingAcid132DeliverRibbonBatch(batch));
        }
        task = Read(frame, address);
        if (Signed(task, 12) < 8)
        {
            frame = DrawHead(frame, TaskView(task, 24, 8), task.ReadWord(12), inputs, NativeBattlePilotVisualLifetimes.Identity(132, Read(frame, address)) with { ResourceGeneration = 0x1212D90 });
        }
        task = Read(frame, address);
        return (Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleDisolvingAcid132Frame DrawHead(NativeBattleDisolvingAcid132Frame frame, NativeBattleEffectArena point, ushort age, NativeBattleDisolvingAcid132Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector xyz = new(Signed(point, 0), Signed(point, 2), Signed(point, 4));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), xyz, Signed(point, 6), 4096, -1024) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Disolving Acid requires its addressed temporary memory."), 180);
        uint temporary = allocation.Address;
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, temporary, 0x1212D90, 0).Write(temporary + 4, BitConverter.GetBytes(age)) };
        frame = inputs.Execute(frame, new NativeBattleDisolvingAcid132DrawSprite180(temporary, 0x1212D90) { PilotIdentity = pilotIdentity });
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 180) };
    }

    private static NativeBattleGeometryRegisterStep Blend(NativeBattleGeometryRegisterStep registers, NativeBattleVector first, NativeBattleVector second, int factor, NativeBattleSummonColors colors)
    {
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - factor)) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)first.X), unchecked((ushort)first.Y), unchecked((ushort)first.Z));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)second.X), unchecked((ushort)second.Y), unchecked((ushort)second.Z));
        return NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
    }

    private static NativeBattleDisolvingAcid132Frame Publish(NativeBattleDisolvingAcid132Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleDisolvingAcid132Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Disolving Acid requires its actual geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Disolving Acid requires its actual arithmetic registers."));
    private static NativeBattleVector Ir(NativeBattleGeometryRegisterStep registers) => new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Signed(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleVector PointVector(NativeBattleDisolvingAcid132Frame frame, uint point) => new(PointSigned(frame, point), PointSigned(frame, point + 2), PointSigned(frame, point + 4));
    private static short PointSigned(NativeBattleDisolvingAcid132Frame frame, uint address) => Signed(Arena(frame), Offset(address, 2));
    private static uint PointDword(NativeBattleDisolvingAcid132Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static NativeBattleDisolvingAcid132Frame Word(NativeBattleDisolvingAcid132Frame frame, uint address, ushort value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleDisolvingAcid132Frame Dword(NativeBattleDisolvingAcid132Frame frame, uint address, uint value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleModulePoolTask Copy(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena arena)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, 8);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }
    private static byte[] Xyz(NativeBattleVector vector)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vector.Z);
        return bytes;
    }
    private static IEnumerable<uint> Chain(NativeBattleDisolvingAcid132Frame frame, uint head)
    {
        HashSet<uint> seen = [];
        uint bank = checked(frame.State.ReadDword(WorkingGlobal) + PointsOffset);
        for (uint point = head; point != 0; point = PointDword(frame, point + 24))
        {
            uint offset = unchecked(point - bank);
            if (offset >= 128 * 28 || offset % 28 != 0 || !seen.Add(point))
            {
                throw new InvalidDataException("Disolving Acid curve leaves its actual linked point bank.");
            }
            yield return point;
        }
    }
    private static (NativeBattleDisolvingAcid132Frame Frame, uint Point) AllocatePoint(NativeBattleDisolvingAcid132Frame frame, short width)
    {
        uint bank = checked(frame.State.ReadDword(WorkingGlobal) + PointsOffset);
        for (int index = 0; index < 128; index++)
        {
            uint point = checked(bank + (uint)(index * 28));
            if (PointSigned(frame, point + 6) == -1)
            {
                frame = Dword(frame, point + 24, 0);
                frame = Word(frame, point + 6, unchecked((ushort)width));
                NativeBattlePilotVisualLifetimes.Allocated(Arena(frame), bank, index);
                return (frame, point);
            }
        }
        return (frame, 0);
    }
}
