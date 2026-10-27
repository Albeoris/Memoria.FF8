using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static ImmutableArray<byte> Preprocess6AD430(ImmutableArray<byte> source)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(4, 4)));
        if (count < 2 || source.Length < checked(0x1C + (count - 2) * 8 + 2))
        {
            throw new InvalidDataException("Saliva requires its complete mutable geometry source.");
        }
        byte[] bytes = source.ToArray();
        int minimum = short.MaxValue;
        int maximum = short.MinValue;
        for (int index = 0; index < count - 1; index++)
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x1C + index * 8, 2));
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        for (int index = 0; index < count; index++)
        {
            int value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0xC + index * 8, 2));
            short normalized = unchecked((short)(unchecked((value - minimum) << 12) / (maximum - minimum)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xE + index * 8, 2), normalized);
        }
        return bytes.ToImmutableArray();
    }

    public static NativeBattleVector Direction6ADFA0(int first, int second)
    {
        int negativeSine = -NativeBattleCameraMath.Sine(first);
        short x = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(second) * negativeSine) >> 12));
        short y = unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(second) * negativeSine) >> 12));
        return new(x, y, unchecked((short)NativeBattleCameraMath.Cosine(first)));
    }

    public static NativeBattleSaliva138Frame Deform6AAC80(NativeBattleSaliva138Frame frame, int factor, NativeBattleSaliva138Resources resources)
    {
        ImmutableArray<byte> source = frame.State.ReadMesh();
        int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(4, 4)));
        uint selected = checked(Working(frame) + 0x11B0);
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> packed = source.AsSpan().Slice(8 + index * 8, 8);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(4)));
            short w = BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(6));
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(CurveRegisters(frame), 0, point, w);
            registers = NativeBattleGeometry.Transform(registers);
            frame = CurvePublish(frame, registers);
            int tableIndex = w >> 1;
            if ((uint)tableIndex >= resources.DeformationTable.Length)
            {
                throw new InvalidDataException("Saliva deformation requires its actual signed source parameter.");
            }
            short x = unchecked((short)registers.Colors.Ir[0]!.Value);
            short y = unchecked((short)registers.Colors.Ir[1]!.Value);
            short z = unchecked((short)registers.Colors.Ir[2]!.Value);
            y = unchecked((short)(y - (unchecked(resources.DeformationTable[tableIndex] * factor) >> 16)));
            frame = WriteArena(frame, checked(selected + (uint)(index * 8)), CurveXyz(new(x, y, z)));
        }
        return frame;
    }

    public static (NativeBattleSaliva138Frame Frame, int Result) AdvanceCurve(NativeBattleSaliva138Frame frame, uint address, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 0x12);
        if (age == 0)
        {
            NativeBattleSaliva138PointStep mouth = inputs.ReadMouth(frame);
            frame = mouth.Frame;
            task = Vector(Read(frame, address), 12, mouth.Point);
            NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 12, 8), 0, NativeBattleEffectArena.Clear(), 0, 8);
            task = task.WriteWord(0x12, 1);
            task = CurveCopyTask(task, 0x1C, point);
            return (Write(frame, address, task), 0);
        }
        if (age < 10)
        {
            for (int substep = 0; substep < 2; substep++)
            {
                task = Read(frame, address);
                int parameter = unchecked((age * 2 + substep) << 11) / 10;
                int sine = NativeBattleCameraMath.Sine(parameter >> 1);
                int arc = unchecked(Signed(task, 0x1A) * sine);
                short angle = unchecked((short)((byte)age << 8));
                short sin = unchecked((short)NativeBattleCameraMath.Sine(angle));
                short cos = unchecked((short)NativeBattleCameraMath.Cosine(angle));
                NativeBattleActionMatrix rotation = new([cos, sin, 0, unchecked((short)-sin), cos, 0, 0, 0, 4096], 0, 0, 0, 4096);
                frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, 0));
                frame = frame with { Memory = NativeBattleModuleRenderCommands.ClearTranslation704070(frame.Memory) };
                frame = Deform6AAC80(frame, arc >> 13, resources);
                NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Vector(task, 12), Vector(task, 0x14), unchecked((short)(4096 - parameter)), unchecked((short)parameter), inputs.Colors);
                frame = frame with { Memory = blended.Memory };
                NativeBattleVector point = blended.Position with { Y = unchecked((short)(blended.Position.Y - (arc >> 14))) };
                NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(point.X - Signed(task, 0x1C), Signed(task, 0x1E) - point.Y, Signed(task, 0x20) - point.Z).Matrix;
                frame = frame with { State = frame.State.WriteWord(0x11E3148, unchecked((ushort)(sine + 2048))) };
                byte[] scaleBytes = frame.State.ReadView(0x11E3138, 18).Read(0, 18);
                NativeBattleActionMatrix scale = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(scaleBytes.AsSpan(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
                NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(basis, scale) with { X = point.X, Y = point.Y, Z = point.Z };
                if (substep == 1)
                {
                    NativeBattleActionMatrix viewed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
                    frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewed, null));
                    (frame, uint temporaryAddress) = Allocate(frame, 92);
                    NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 92).Write(0, BitConverter.GetBytes(0x11DF764u)).Write(4, BitConverter.GetBytes(checked(Working(frame) + 0x11B0)));
                    temporary = temporary.Write(0x10, BitConverter.GetBytes(unchecked((ushort)(((byte)age & 7) << 4))));
                    temporary = temporary.Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes(0x01000100u)).Write(0x1C, new byte[4]).Write(0x20, BitConverter.GetBytes(0x01000080u));
                    frame = PublishContext(frame, temporaryAddress, temporary, 92);
                    frame = inputs.Execute(frame, new NativeBattleSaliva138DrawCustom92(temporaryAddress));
                    frame = Release(frame, 92);
                }
                task = Read(frame, address);
                task = Vector(task, 0x1C, point).MarkPayloadUnknown(0x22, 2);
                frame = Write(frame, address, task);
                (frame, uint record) = CurveAllocatePoint(frame, point);
                if (record == 0)
                {
                    throw new NotSupportedException("Saliva exhausted its actual retained curve point bank.");
                }
                task = Read(frame, address);
                if (task.ReadWord(0x24) == 0)
                {
                    task = task.WriteDword(0x28, record).WriteDword(0x2C, record);
                }
                else
                {
                    frame = CurveDword(frame, task.ReadDword(0x2C) + 0x1C, record);
                    task = Read(frame, address).WriteDword(0x2C, record);
                }
                frame = CurveWord(frame, record + 0xE, task.ReadWord(0x24));
                frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
            }
        }
        else
        {
            uint current = task.ReadDword(0x28);
            foreach (uint record in CurveChain(frame, current))
            {
                int index = CurveSigned(frame, record + 0xE);
                if ((uint)index >= resources.AgeDrift.Length)
                {
                    throw new InvalidDataException("Saliva requires its actual curve birth ordinal.");
                }
                frame = CurveWord(frame, record + 2, unchecked((ushort)(CurveSigned(frame, record + 2) + resources.AgeDrift[index])));
            }
        }
        task = Read(frame, address);
        if (Signed(task, 0x24) >= 2)
        {
            foreach (uint record in CurveChain(frame, task.ReadDword(0x28)))
            {
                frame = CurveWord(frame, record + 0x18, unchecked((ushort)Math.Max(0, (int)unchecked((short)(CurveSigned(frame, record + 0x18) - 4)))));
            }
            uint head = task.ReadDword(0x28);
            while (CurveSigned(frame, head + 0x18) <= 32)
            {
                uint next = CurveReadDword(frame, head + 0x1C);
                if (CurveSigned(frame, next + 0x18) > 32)
                {
                    break;
                }
                frame = CurveWord(frame, head + 6, ushort.MaxValue);
                task = Read(frame, address).WriteWord(0x24, unchecked((ushort)(Read(frame, address).ReadWord(0x24) - 1))).WriteDword(0x28, next);
                frame = Write(frame, address, task);
                if (Signed(task, 0x24) < 2)
                {
                    return (frame, 2);
                }
                head = next;
            }
        }
        task = Read(frame, address);
        if (Signed(task, 0x24) >= 3)
        {
            frame = LoadMatrix(frame, inputs.ReadCamera(frame));
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Saliva requires its actual projection controls.");
            foreach (uint record in CurveChain(frame, task.ReadDword(0x28)))
            {
                NativeBattleVector point = new(CurveSigned(frame, record), CurveSigned(frame, record + 2), CurveSigned(frame, record + 4));
                NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(CurveRegisters(frame), 0, point, CurveSigned(frame, record + 6));
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                frame = CurvePublish(frame, registers);
                frame = CurveDword(frame, record + 8, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Saliva requires its actual projected point."));
                frame = CurveWord(frame, record + 12, unchecked((ushort)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Saliva requires its actual projected depth.")) >> 2)));
            }
            (frame, NativeBattleGfGeometryBatch batch) = DrawCurveRibbon(frame, task.ReadDword(0x28), Signed(task, 0x24), resources);
            frame = inputs.Execute(frame, new NativeBattleSaliva138DeliverRibbonBatch(batch));
        }
        task = Read(frame, address);
        return (Write(frame, address, task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + 1)))), 0);
    }

    private static NativeBattleModulePoolTask CurveCopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
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

    private static NativeBattleGeometryRegisterStep CurveRegisters(NativeBattleSaliva138Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Saliva requires its actual geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Saliva requires its actual arithmetic registers."));
    private static NativeBattleSaliva138Frame CurvePublish(NativeBattleSaliva138Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static byte[] CurveXyz(NativeBattleVector point)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return bytes;
    }
    private static int CurveArenaOffset(NativeBattleSaliva138Frame frame, uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
    private static short CurveSigned(NativeBattleSaliva138Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(CurveArenaOffset(frame, address, 2), 2));
    private static uint CurveReadDword(NativeBattleSaliva138Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(CurveArenaOffset(frame, address, 4), 4));
    private static NativeBattleSaliva138Frame CurveWord(NativeBattleSaliva138Frame frame, uint address, ushort value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleSaliva138Frame CurveDword(NativeBattleSaliva138Frame frame, uint address, uint value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static IEnumerable<uint> CurveChain(NativeBattleSaliva138Frame frame, uint head)
    {
        HashSet<uint> seen = [];
        for (uint current = head; current != 0; current = CurveReadDword(frame, current + 0x1C))
        {
            uint offset = unchecked(current - Working(frame) - 0x5B0);
            if (offset >= 96 * 32 || offset % 32 != 0 || !seen.Add(current))
            {
                throw new InvalidDataException("Saliva curve leaves its actual linked point bank.");
            }
            yield return current;
        }
    }
    private static (NativeBattleSaliva138Frame Frame, uint Address) CurveAllocatePoint(NativeBattleSaliva138Frame frame, NativeBattleVector point)
    {
        for (int index = 0; index < 96; index++)
        {
            uint address = checked(Working(frame) + 0x5B0u + (uint)(index * 32));
            if (CurveSigned(frame, address + 6) == -1)
            {
                frame = WriteArena(frame, address, CurveXyz(point));
                frame = CurveWord(frame, address + 6, 200);
                frame = CurveWord(frame, address + 0x18, 128);
                frame = CurveWord(frame, address + 0x1A, 0);
                return (CurveDword(frame, address + 0x1C, 0), address);
            }
        }
        return (frame, 0);
    }

    public static (NativeBattleSaliva138Frame Frame, NativeBattleGfGeometryBatch Batch) DrawCurveRibbon(NativeBattleSaliva138Frame frame, uint head, short count, NativeBattleSaliva138Resources resources)
    {
        if (count < 3 || count > 96)
        {
            throw new InvalidDataException("Saliva requires its actual projected curve count.");
        }
        uint[] chain = CurveChain(frame, head).ToArray();
        if (chain.Length != count)
        {
            throw new InvalidDataException("Saliva curve count differs from its actual NULL-terminated chain.");
        }
        short? previousX = null;
        short? previousY = null;
        for (int index = 0; index < count; index++)
        {
            frame = CurveEdge(frame, chain[index], index + 1 < count ? chain[index + 1] : 0, index != 0, ref previousX, ref previousY, resources.DistanceTable);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count - 1; index++)
        {
            uint current = chain[index];
            uint next = chain[index + 1];
            short depth = CurveSigned(frame, current + 0xC);
            if (depth <= 0 || CurveSigned(frame, next + 0xC) <= 0)
            {
                continue;
            }
            SkinWrites packet = new(52, allocated);
            packet.Word(0, 0x0C000000);
            packet.Word(8, CurveReadDword(frame, current + 8));
            packet.Word(20, CurveReadDword(frame, current + 0x10));
            packet.Word(32, CurveReadDword(frame, next + 8));
            packet.Word(44, CurveReadDword(frame, next + 0x10));
            uint firstColor = CurveGray(CurveSigned(frame, current + 0x18));
            uint secondColor = CurveGray(CurveSigned(frame, next + 0x18));
            packet.Word(4, firstColor);
            packet.Word(16, firstColor);
            packet.Byte(7, 0x3E);
            packet.Word(28, secondColor);
            packet.Word(40, secondColor);
            packet.Word(12, 0x3C14C008);
            packet.Word(24, 0x00BAC038);
            packet.Word16(36, 0xC008);
            packet.Word16(48, 0xC038);
            int bucket = 17 + ((depth + CurveSigned(frame, next + 0xC)) >> 3);
            attempts.Add(packet.Attempt(new(bucket, packet.Words(), [])));
            allocated += 52;
        }
        return (frame, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleSaliva138Frame CurveEdge(NativeBattleSaliva138Frame frame, uint current, uint next, bool hasPrevious, ref short? previousX, ref short? previousY, ImmutableArray<byte> distances)
    {
        if (CurveSigned(frame, current + 0xC) < 0)
        {
            return frame;
        }
        int offsetX = 0;
        int offsetY = 0;
        if (next != 0)
        {
            if (CurveSigned(frame, next + 0xC) < 0)
            {
                return frame;
            }
            int dx = CurveSigned(frame, next + 8) - CurveSigned(frame, current + 8);
            int dy = CurveSigned(frame, current + 0xA) - CurveSigned(frame, next + 0xA);
            if (distances.IsDefault || distances.Length != 16384)
            {
                throw new NotSupportedException("Saliva requires its actual edge distance table.");
            }
            int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            int directionX;
            int directionY;
            if (hasPrevious)
            {
                int oldX = previousX ?? throw new NotSupportedException("Saliva requires its produced preceding edge direction.");
                int oldY = previousY ?? throw new NotSupportedException("Saliva requires its produced preceding edge direction.");
                if (distance == 0)
                {
                    directionX = oldX;
                    directionY = oldY;
                }
                else
                {
                    int normalizedX = unchecked(dy << 12) / distance;
                    int normalizedY = unchecked(dx << 12) / distance;
                    previousX = unchecked((short)normalizedX);
                    previousY = unchecked((short)normalizedY);
                    int sumX = unchecked(normalizedX + oldX);
                    int sumY = unchecked(normalizedY + oldY);
                    if (sumX == 0 && sumY == 0)
                    {
                        sumX = unchecked(normalizedX * 2);
                        sumY = unchecked(normalizedY * 2);
                    }
                    int factor = unchecked(0x4000 - (unchecked(sumX * sumX + sumY * sumY) >> 13));
                    directionX = unchecked(factor * sumX) >> 13;
                    directionY = unchecked(factor * sumY) >> 13;
                }
            }
            else
            {
                if (distance == 0)
                {
                    return CurveWord(frame, current + 0xC, ushort.MaxValue);
                }
                directionX = unchecked(dy << 12) / distance;
                directionY = unchecked(dx << 12) / distance;
                previousX = unchecked((short)directionX);
                previousY = unchecked((short)directionY);
            }
            int width = unchecked(CurveSigned(frame, next + 6) << 5) / (CurveSigned(frame, current + 0xC) + 4096);
            offsetX = unchecked(width * directionX) >> 12;
            offsetY = unchecked(width * directionY) >> 12;
        }
        short x = CurveSigned(frame, current + 8);
        short y = CurveSigned(frame, current + 0xA);
        frame = CurveWord(frame, current + 8, unchecked((ushort)(x - offsetX)));
        frame = CurveWord(frame, current + 0x10, unchecked((ushort)(x + offsetX)));
        frame = CurveWord(frame, current + 0x12, unchecked((ushort)(y + offsetY)));
        return CurveWord(frame, current + 0xA, unchecked((ushort)(y - offsetY)));
    }

    private static uint CurveGray(short intensity)
    {
        uint value = unchecked((uint)(int)intensity);
        return ((value << 8 | value) << 8) | value;
    }
}
