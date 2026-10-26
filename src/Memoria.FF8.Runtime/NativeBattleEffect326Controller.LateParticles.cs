using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeRibbon630400(NativeBattleEffect326Frame frame)
    {
        (frame, uint address) = AppendLate(frame, 0x630430);
        for (uint slot = 0x24F2180; slot < 0x24F2248; slot += 20)
        {
            frame = WriteBytes(frame, slot, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0u)), 4);
        }
        return frame;
    }

    private static (NativeBattleEffect326Frame Frame, uint Address) AppendLate(NativeBattleEffect326Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect326 requires an available mandatory late child slot.");
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0)), address);
    }

    private static NativeBattleEffect326Step AdvanceLateClock(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs, int end)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)age)));
        return new(frame, age >= end ? 2 : 0);
    }

    public static NativeBattleEffect326Step AdvanceRibbon630430(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        uint scale = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE19674u + (uint)(age * 4)), 4));
        (frame, uint temporary) = Allocate(frame, 64);
        NativeBattleEffectArena context = Context(frame, temporary, 64);
        NativeBattleEffectArena endpoints = ReadBytes(frame, 0x24F1310, 16);
        int x = Word(endpoints, 8) - Word(endpoints, 0);
        int y = Word(endpoints, 10) - Word(endpoints, 2);
        int z = Word(endpoints, 12) - Word(endpoints, 4);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        context = context.Write(0x30, BitConverter.GetBytes(direction.X)).Write(0x34, BitConverter.GetBytes(direction.Y)).Write(0x38, BitConverter.GetBytes(direction.Z));
        int distance = unchecked(NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(x, y, z)) * (int)scale) >> 12;
        context = context.Write(0x28, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 0) + (unchecked(direction.X * distance) >> 12))))).Write(0x2A, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 2) + (unchecked(direction.Y * distance) >> 12))))).Write(0x2C, BitConverter.GetBytes(unchecked((short)(Word(endpoints, 4) + (unchecked(direction.Z * distance) >> 12)))));
        int ring = age % 10;
        if (ring < 0)
        {
            throw new InvalidDataException("Effect326 ribbon requires its live nonnegative history cursor.");
        }
        NativeBattleEffectArena history = ReadBytes(frame, 0x24F2180, 200).Write(ring * 20, BitConverter.GetBytes(1u));
        history = CopyBytes(endpoints, 0, history, ring * 20 + 4, 8);
        history = CopyBytes(context, 0x28, history, ring * 20 + 12, 8);
        NativeBattleEffectArena firstControls = ReadBytes(frame, 0x24F0BD0, 80);
        NativeBattleEffectArena secondControls = ReadBytes(frame, 0x24F0C28, 80);
        int count = 0;
        for (; count < 10; count++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(history.Read(ring * 20, 4)) == 0)
            {
                break;
            }
            firstControls = CopyBytes(history, ring * 20 + 4, firstControls, count * 8, 8);
            secondControls = count == 0 ? CopyBytes(endpoints, 8, secondControls, 0, 8) : CopyBytes(history, ring * 20 + 12, secondControls, count * 8, 8);
            ring = ring == 0 ? 9 : ring - 1;
        }
        frame = WriteBytes(frame, 0x24F2180, history, 200);
        frame = WriteBytes(frame, 0x24F0BD0, firstControls, 80);
        frame = WriteBytes(frame, 0x24F0C28, secondControls, 80);
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = Context(frame, workAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, side == 0 ? firstControls : secondControls, 0, work);
                uint destination = side == 0 ? 0x24F1068u : 0x24F1168u;
                NativeBattleEffectArena samples = ReadBytes(frame, destination, 256);
                for (int sample = 0; sample < 32; sample++)
                {
                    samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 31, samples, sample * 8);
                }
                frame = WriteBytes(frame, destination, samples, 256);
                frame = PublishContext(frame, workAddress, work, 400);
            }
            frame = Release(frame, 400);
            frame = LoadMatrix(frame, ReadPacked(frame, 0x24F2268));
            NativeBattleRibbonPair72Step drawn = NativeBattleRibbonPair72.DrawLoaded(frame.Memory, ReadBytes(frame, 0x24F1068, 256), ReadBytes(frame, 0x24F1168, 256), context, inputs.ReadProjection(frame), inputs.Colors, 0x3A903030);
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Temporary64;
            frame = PublishContext(frame, temporary, context, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        }
        else
        {
            frame = PublishContext(frame, temporary, context, 64);
        }
        frame = Release(frame, 64);
        return AdvanceLateClock(frame, address, inputs, 6);

        static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    }

    public static NativeBattleEffect326Step AdvanceLateBase630160(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        if ((ReadBytes(frame, 0x24F12E4, 1).Read(0, 1)[0] & 4) == 0)
        {
            return new(frame, 2);
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0x400, 0, 0x800));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, 0x1A00, 0x1A00, 0x1A00);
        NativeBattleActionMatrix bone = NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, 0x24F12A8).ReadMatrix(), ReadPacked(frame, 0x24F2288).ReadMatrix());
        local = NativeBattleGfMatrices.Compose56C2F0(bone, local);
        frame = StoreLateOverlayPoint(frame, 0x24F1310, local, new(0, 1000, -1000));
        frame = StoreLateOverlayPoint(frame, 0x24F1318, local, new(0, 6200, 600));
        NativeBattleActionMatrix modelView = NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, 0x24F2268).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(modelView, null));
        frame = ProjectLateOverlayPoint(frame, 0x24F1310, inputs);
        int firstDepth = unchecked((int)(Registers(frame).Geometry.Otz ?? throw new NotSupportedException("Effect326 overlay requires its retained OTZ before the first projection.")));
        frame = ProjectLateOverlayPoint(frame, 0x24F1318, inputs);
        int secondDepth = unchecked((int)(Registers(frame).Geometry.Otz ?? throw new NotSupportedException("Effect326 overlay requires its retained OTZ before the second projection.")));
        int tableBase = Math.Max(Math.Min(firstDepth, secondDepth) / 4 - 8, 0);
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) > 0)
        {
            (frame, uint temporary) = Allocate(frame, 88);
            NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE18B38u)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x33u));
            NativeBattleDefaultActMeshStep drawn = NativeBattleEffect326MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE18B38), inputs.ReadProjection(frame), context, inputs.Colors, 14, tableBaseBucket: tableBase);
            frame = frame with { Memory = drawn.Memory };
            frame = PublishContext(frame, temporary, drawn.Context88, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
            frame = Release(frame, 88);
        }
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            uint control = frame.State.ReadDword(0x24F12CC);
            uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, control, 4).Read(0, 4));
            frame = WriteBytes(frame, 0x24F2288, ReadBytes(frame, checked(skeleton + 0xB30), 32), 32);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect326Frame StoreLateOverlayPoint(NativeBattleEffect326Frame frame, uint address, NativeBattleActionMatrix matrix, NativeBattleVector offset)
    {
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(matrix, offset);
        NativeBattleEffectArena point = ReadBytes(frame, address, 8).Write(0, BitConverter.GetBytes(unchecked((short)(rotated.X + matrix.X)))).Write(2, BitConverter.GetBytes(unchecked((short)(rotated.Y + matrix.Y)))).Write(4, BitConverter.GetBytes(unchecked((short)(rotated.Z + matrix.Z))));
        return WriteBytes(frame, address, point, 8);
    }

    private static NativeBattleEffect326Frame ProjectLateOverlayPoint(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleEffectArena point = ReadBytes(frame, address, 8);
        NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, vector, Padding(point, 6));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect326 overlay requires its actual projection controls.");
        return Publish(frame, NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb));
    }

    public static NativeBattleEffect326Frame InitializeLateBurst630810(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs)
    {
        (frame, uint address) = AppendLate(frame, 0x630870);
        int actor = unchecked((int)frame.State.ReadDword(0x24F0C80));
        NativeBattleEffect326PointStep anchor = (inputs.ReadAnchor571400 ?? throw new NotSupportedException("Effect326 late burst requires its actual571400 target anchor."))(frame, actor, TaskView(Read(frame, address), 0x10, 8));
        frame = anchor.Frame;
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 0x10, anchor.Point8, 8);
        int angle = LateRoll(ref frame) & 4095;
        task = task.WriteWord(0x18, unchecked((ushort)angle)).WriteWord(0x1C, 0x1000).WriteWord(0x20, 0x1E00);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect326Frame InitializeHistoryBurst630A70(NativeBattleEffect326Frame frame)
    {
        (frame, uint address) = AppendLate(frame, 0x630AB0);
        int angle = LateRoll(ref frame) & 4095;
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x18, unchecked((ushort)angle)).WriteWord(0x1C, 0x600).WriteWord(0x20, 0xA00);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect326Step AdvanceLateBurst630870(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        return AdvanceLateAlignedMesh(frame, address, resources, inputs, false);
    }

    public static NativeBattleEffect326Step AdvanceHistoryBurst630AB0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        return AdvanceLateAlignedMesh(frame, address, resources, inputs, true);
    }

    private static NativeBattleEffect326Step AdvanceLateAlignedMesh(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs, bool history)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena position = history ? ReadBytes(frame, 0x24FD250, 8) : TaskView(task, 0x10, 8);
        NativeBattleEffectArena eye = ReadBytes(frame, 0x24FD358, 6);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(LateWord(eye, 0) - LateWord(position, 0), LateWord(eye, 2) - LateWord(position, 2), LateWord(eye, 4) - LateWord(position, 4));
        byte[] source = resources.ReadStatic(0xE19740, 12);
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(BinaryPrimitives.ReadInt32LittleEndian(source), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(8)), direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        NativeBattleActionMatrix roll = NativeBattleGfMatrices.Euler56CE30(new(0, 0, Signed(task, 0x18)));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Compose56C2F0(basis, roll) with { X = LateWord(position, 0), Y = LateWord(position, 2), Z = LateWord(position, 4) };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 0x1C), Signed(task, 0x1C), Signed(task, 0x20));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        int age = Signed(task, 12);
        uint mesh = history ? 0xE13040u : 0xE11798u;
        uint colorTable = history ? 0xE197B0u : 0xE19790u;
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(colorTable + (uint)((age % 8) * 4)), 4));
        int fadeStart = history ? 7 : 6;
        (frame, uint temporary) = Allocate(frame, 144);
        NativeBattleEffectArena context = Context(frame, temporary, 144).Write(0, BitConverter.GetBytes(mesh)).Write(8, BitConverter.GetBytes(0u)).Write(0x10, BitConverter.GetBytes(0u)).Write(0x14, BitConverter.GetBytes(unchecked(age << 4))).Write(0x18, BitConverter.GetBytes((ushort)0)).Write(0x1A, BitConverter.GetBytes((ushort)0)).Write(0x1C, BitConverter.GetBytes((ushort)64)).Write(0x1E, BitConverter.GetBytes((ushort)128)).Write(0x20, BitConverter.GetBytes(age < fadeStart ? 0x33u : 0xF3u)).Write(0x24, BitConverter.GetBytes(color));
        if (age >= fadeStart)
        {
            context = context.Write(12, BitConverter.GetBytes(history ? unchecked((age - 7) << 10) : unchecked((age - 6) * 682)));
        }
        NativeBattleWindowedMesh144Step drawn = NativeBattleWindowedMesh144.DrawLoaded(frame.Memory, resources.Mesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors, groupMask: (1 << 2) | (1 << 3) | (1 << 6) | (1 << 7));
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Context144, 144);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        return AdvanceLateClock(Release(frame, 144), address, inputs, history ? 11 : 12);
    }

    private static int LateRoll(ref NativeBattleEffect326Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static short LateWord(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static NativeBattleEffectArena LateSetWord(NativeBattleEffectArena source, int offset, int value) => source.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));


    public static NativeBattleEffect326Frame InitializeLatePair630CB0(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources)
    {
        (frame, uint first) = AppendLate(frame, 0x630D30);
        NativeBattleModulePoolTask task = Read(frame, first).WriteWord(14, 4);
        NativeBattleEffect326PointStep point = ReadOwnedBone502170(frame, resources, 0x24F1268, 0xF1, 0, TaskView(task, 0x10, 8));
        frame = point.Frame;
        task = CopyTask(Read(frame, first), 0x10, point.Point8, 8).WriteWord(14, 4).WriteWord(0x10, 800).WriteWord(0x12, 0);
        frame = Write(frame, first, task);
        (frame, uint second) = AppendLate(frame, 0x630D30);
        NativeBattleModulePoolTask other = Read(frame, second).WriteWord(14, 8).WriteWord(0x10, unchecked((ushort)-Signed(task, 0x10))).WriteWord(0x12, 0).WriteWord(0x14, task.ReadWord(0x14));
        return Write(frame, second, other);
    }

    public static NativeBattleEffect326Step AdvanceLatePair630D30(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint bank = frame.State.ReadDword(0xE19670);
        (frame, uint scratchAddress) = Allocate(frame, 72);
        if ((inputs.ReadFlags(frame) & 0x201) == 0 && Signed(task, 12) <= 6)
        {
            NativeBattleEffectArena scratch = CopyBytes(TaskView(task, 0x10, 8), 0, Context(frame, scratchAddress, 72), 0, 8);
            int directionSign = LateWord(scratch, 0) < 0 ? -1 : 1;
            for (int birth = 0; birth < 6; birth++)
            {
                uint? free = FindLateParticle(frame, bank);
                if (free is not uint destination)
                {
                    break;
                }
                NativeBattleEffectArena row = ReadBytes(frame, destination, 24).Write(0, BitConverter.GetBytes(unchecked((uint)Signed(task, 14))));
                row = LateSetWord(row, 4, 0);
                row = LateSetWord(row, 6, LateRoll(ref frame) % 1536 + 512);
                row = CopyBytes(scratch, 0, row, 8, 8);
                int x = (LateRoll(ref frame) & 2047) * directionSign;
                int z = (LateRoll(ref frame) & 8191) - 4096;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, 0, z);
                scratch = scratch.Write(0x28, BitConverter.GetBytes(direction.X)).Write(0x2C, BitConverter.GetBytes(direction.Y)).Write(0x30, BitConverter.GetBytes(direction.Z));
                int positionSpeed = LateRoll(ref frame) % 400 + 300;
                row = LateSetWord(row, 8, LateWord(row, 8) + (unchecked(direction.X * positionSpeed) >> 12));
                row = LateSetWord(row, 10, -(LateRoll(ref frame) % 100));
                row = LateSetWord(row, 12, LateWord(row, 12) + (unchecked(direction.Z * positionSpeed) >> 12));
                int velocitySpeed = LateRoll(ref frame) % 100 + 90;
                row = LateSetWord(row, 16, unchecked(direction.X * velocitySpeed) >> 12);
                row = LateSetWord(row, 18, -15 - LateRoll(ref frame) % 25);
                row = LateSetWord(row, 20, unchecked(direction.Z * velocitySpeed) >> 12);
                frame = WriteBytes(frame, destination, row, 24);
            }
            frame = PublishContext(frame, scratchAddress, scratch, 72);
        }
        (frame, uint spriteAddress) = Allocate(frame, 180);
        NativeBattlePackedMatrix32 view = ReadPacked(frame, 0x24F2268);
        NativeBattleEffectArena temporary = CopyBytes(PackedArena(view), 0, Context(frame, scratchAddress, 72), 8, 32);
        frame = PublishContext(frame, scratchAddress, temporary, 72);
        NativeBattleEffectArena sprite = Context(frame, spriteAddress, 180).Write(0, BitConverter.GetBytes(0xE10E04u)).Write(0x24, BitConverter.GetBytes((ushort)8));
        frame = PublishContext(frame, spriteAddress, sprite, 180);
        frame = frame with { Memory = NativeBattleEffect315Gte.LoadAlternateTranslation64DDA0(NativeBattleEffect315Gte.LoadRotation45DE50(frame.Memory, view), view) };
        int survivors = 0;
        uint mask = unchecked((uint)Signed(task, 14));
        for (int index = 0; index < 150; index++)
        {
            uint location = checked(bank + (uint)(index * 24));
            NativeBattleEffectArena row = ReadBytes(frame, location, 24);
            if ((BinaryPrimitives.ReadUInt32LittleEndian(row.Read(0, 4)) & mask) == 0)
            {
                continue;
            }
            NativeBattleVector position = new(LateWord(row, 8), LateWord(row, 10), LateWord(row, 12));
            frame = Publish(frame, NativeBattleGeometry.LoadVector(Registers(frame), 0, position, Padding(row, 14)));
            frame = frame with { Memory = NativeBattleEffect315Gte.SetDiagonal64DE00(NativeBattleEffect315Gte.TransformAlternate4607E0(frame.Memory), unchecked((ushort)LateWord(row, 6))) };
            frame = PublishContext(frame, spriteAddress, LateSetWord(Context(frame, spriteAddress, 180), 4, LateWord(row, 4)), 180);
            frame = frame with { Memory = NativeBattleEffect315Gte.MacToTranslation64DE40(frame.Memory) };
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, Context(frame, spriteAddress, 180), resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2);
            frame = PublishContext(frame with { Memory = drawn.Memory }, spriteAddress, drawn.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
            if ((inputs.ReadFlags(frame) & 0x201) != 0)
            {
                continue;
            }
            if (LateWord(drawn.Temporary180, 0x28) < 0)
            {
                row = row.Write(0, BitConverter.GetBytes(0u));
            }
            else
            {
                row = LateSetWord(row, 4, LateWord(row, 4) + 1);
                int width = LateWord(row, 6);
                row = LateSetWord(row, 6, width + (width >> 5));
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocity = LateWord(row, 16 + axis * 2);
                    row = LateSetWord(row, 8 + axis * 2, LateWord(row, 8 + axis * 2) + velocity);
                    row = LateSetWord(row, 16 + axis * 2, axis == 1 ? velocity + (velocity >> 6) : velocity - (velocity >> 3));
                }
                survivors++;
            }
            frame = WriteBytes(frame, location, row, 24);
        }
        frame = Release(Release(frame, 180), 72);
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1))));
        return new(frame, survivors == 0 ? 2 : 0);
    }

    private static uint? FindLateParticle(NativeBattleEffect326Frame frame, uint bank)
    {
        for (int index = 0; index < 150; index++)
        {
            uint address = checked(bank + (uint)(index * 24));
            if (BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4)) == 0)
            {
                return address;
            }
        }
        return null;
    }


    public static NativeBattleEffect326Frame InitializeTargetBurst631150(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(checked(0xE19710u + (uint)(index * 8)), 8));
            (frame, uint first) = AppendLate(frame, index < 3 ? 0x6312F0u : 0x632350u);
            NativeBattleModulePoolTask task = Read(frame, first).WriteWord(14, unchecked((ushort)LateWord(source, 6))).WriteWord(0x10, unchecked((ushort)LateWord(source, 0))).WriteWord(0x12, unchecked((ushort)LateWord(source, 2))).WriteWord(0x14, unchecked((ushort)LateWord(source, 4))).WriteWord(0x16, 0).WriteWord(0x1C, index < 3 ? (ushort)0x4000 : (ushort)0x2DC0).WriteWord(0x1E, index < 3 ? (ushort)0x2000 : (ushort)0xF40).WriteWord(0x20, unchecked((ushort)(40 - LateWord(source, 4)))).WriteWord(0x22, unchecked((ushort)LateWord(source, 4)));
            frame = Write(frame, first, task);
            for (int trail = 0; trail < (index < 3 ? 2 : 4); trail++)
            {
                (frame, uint child) = AppendLate(frame, index < 3 ? 0x6321A0u : 0x632500u);
                NativeBattleModulePoolTask next = CopyTask(Read(frame, child), 0x10, TaskView(task, 0x10, 8), 8).WriteWord(14, task.ReadWord(14)).WriteWord(0x1C, task.ReadWord(0x1C)).WriteWord(0x1E, task.ReadWord(0x1E));
                short delay = unchecked((short)(Signed(task, 0x14) + trail));
                next = next.WriteWord(0x14, unchecked((ushort)delay)).WriteWord(0x16, 0x800).WriteWord(0x20, unchecked((ushort)(40 - delay))).WriteWord(0x22, unchecked((ushort)delay));
                frame = Write(frame, child, next);
            }
        }
        return frame;
    }

    public static NativeBattleEffect326Step AdvanceTargetTrail326(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback is not (0x6312F0 or 0x6321A0 or 0x632350 or 0x632500))
        {
            throw new InvalidDataException("Effect326 target trail requires its actual selected callback.");
        }
        bool second = task.Callback is 0x632350 or 0x632500;
        bool trail = task.Callback is 0x6321A0 or 0x632500;
        bool paused = (inputs.ReadFlags(frame) & 0x201) != 0;
        if (Signed(task, 0x22) > 0)
        {
            if (!paused)
            {
                frame = Write(frame, address, task.WriteWord(0x22, unchecked((ushort)(Signed(task, 0x22) - 1))));
            }
            return new(frame, 0);
        }
        int age = Signed(task, 12);
        short focal = unchecked((short)(inputs.ReadProjection(frame).FocalLength ?? throw new NotSupportedException("Effect326 target trail requires its actual focal WORD.")));
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = focal };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        int selector = Signed(task, 14);
        uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE196C0u + (uint)(selector * 4)), 4));
        uint material = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(0xE19730u + (uint)(selector * 4)), 4));
        int fade = Signed(task, 0x16);
        int fadeStep = (4096 - fade) / 8;
        if (age < 8)
        {
            fade = unchecked(4096 - fadeStep * age);
        }
        else if (age >= Signed(task, 0x20) - 8)
        {
            fade = unchecked(fade + (age - (Signed(task, 0x20) - 8)) * fadeStep);
        }
        (frame, uint temporary) = Allocate(frame, 108);
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(mesh)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(fade)).Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(0x20, BitConverter.GetBytes(material));
        LateColoredMeshStep drawn = LateColoredMesh108.Draw6314A0(frame.Memory, resources.Mesh(mesh), inputs.ReadProjection(frame), context, inputs.Colors);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        frame = Release(frame, 108);
        if (paused)
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        if (age < (second ? 6 : 4))
        {
            short velocity = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(Signed(task, 0x1C) - velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / (second ? 3 : 2))));
        }
        short nextAge = unchecked((short)(Signed(task, 12) + 1));
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)nextAge)));
        return new(frame, nextAge >= (trail ? 7 : Signed(task, 0x20)) ? 2 : 0);
    }

    private sealed record LateColoredMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context, NativeBattleGfGeometryBatch Batch);

    private static class LateColoredMesh108
    {
        public static LateColoredMeshStep Draw6314A0(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context108, NativeBattleSummonColors colors, byte depthShift = 3)
        {
            uint flags = Dword(context108, 0x1C);
            if (Dword(context108, 0) != mesh.Address || mesh.Address is not (0xE18798 or 0xE183F8 or 0xE113F8 or 0xE11058) || flags != 0xF3 || depthShift != 3)
            {
                throw new NotSupportedException("Effect326 colored mesh requires its selected native context.");
            }
            uint material = Dword(context108, 0x20);
            NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect326 colored mesh requires its actual geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect326 colored mesh requires its actual arithmetic registers."));
            NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect326 colored mesh requires its current projection controls.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect326 colored mesh requires its actual primitive process.");
            NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect326 colored mesh requires its current far color owner.");
            byte[] rgb = context108.Read(8, 3);
            far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
            uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
            context108 = Put(Put(context108, 4, mesh.Address + 8), 0x18, 0);
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
            int allocated = 0;
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            for (int group = 0; group < 8; group++)
            {
                if (cursor < 0 || cursor > mesh.Bytes.Length - 4)
                {
                    throw new InvalidDataException("Effect326 colored mesh leaves its actual polygon owner.");
                }
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
                cursor = checked(cursor + 4);
                context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));
                if (count < 0 || count != 0 && group != 3 || count > (mesh.Bytes.Length - cursor) / 24)
                {
                    throw new NotSupportedException("Effect326 colored mesh requires its reached plain FT4 group.");
                }
                for (int faceIndex = 0; faceIndex < count; faceIndex++)
                {
                    byte[] face = mesh.Bytes.AsSpan(cursor, 24).ToArray();
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Load(corner, corner);
                    }
                    registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                    uint firstFlags = registers.Colors.Flags!.Value;
                    NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                    }
                    NativeBattleMeshPacketVariant variant = new(material, null, false, true)
                    {
                        ObserveFlags = value => context108 = Put(context108, 0x34, value),
                        ObserveArea = _ =>
                        {
                            registers = NativeBattleGeometry.Clip45EE10(registers);
                            context108 = Put(context108, 0x28, registers.Geometry.Mac0!.Value);
                        }
                    };
                    NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(ReadCorner), 3, new(flags, farColor, unchecked((int)Dword(context108, 12)), null, null, 0, 0, 17, depthShift), colors, allocated, variant: variant);
                    if (attempt.Primitive is not null)
                    {
                        context108 = Put(context108, 0x30, registers.Geometry.Otz!.Value);
                        uint command = (material | 0x2C000000) | 0x02000000;
                        registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, command, Dword(context108, 12), far, colors).Registers };
                        allocated = checked(allocated + attempt.PacketLength);
                    }
                    attempts.Add(attempt);
                    cursor = checked(cursor + 24);
                    context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));

                    void Load(int corner, int slot)
                    {
                        int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                        ReadOnlySpan<byte> packed = mesh.Bytes.AsSpan(offset, 8);
                        registers = NativeBattleGeometry.LoadVector(registers, slot, new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), BinaryPrimitives.ReadInt16LittleEndian(packed[6..]));
                    }

                    NativeBattleModuleProjectedVertex ReadCorner(int corner)
                    {
                        if (corner == 3)
                        {
                            Load(3, 0);
                            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                            projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect326 colored mesh requires its actual ZSF4."));
                        }
                        return projected[corner];
                    }
                }
            }
            memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
            return new(memory, context108, new(attempts.ToImmutable(), allocated));
        }

        private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

        private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));

        private sealed class Corners(Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
        {
            private readonly Dictionary<int, NativeBattleModuleProjectedVertex> values = [];
            public int Count => 4;
            public NativeBattleModuleProjectedVertex this[int index]
            {
                get
                {
                    if (!values.TryGetValue(index, out NativeBattleModuleProjectedVertex value))
                    {
                        value = read(index);
                        values.Add(index, value);
                    }
                    return value;
                }
            }
            public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

}
