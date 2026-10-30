using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect69StageKernel;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect69StageDrawing
{
    public static NativeBattleEffect69StageStep StageDraw6FF6E0(NativeBattleEffect69Frame frame, int group, ushort threshold, ushort randomMask, uint metricPointer, NativeBattleEffect69Inputs inputs, int tableBaseBucket = 17, byte depthShift = 0)
    {
        if (depthShift > 31)
        {
            throw new InvalidDataException("Effect69 stage drawing requires its native depth shift.");
        }
        uint temporary;
        (frame, temporary) = Allocate(frame, 112);
        frame = (inputs.PrepareStage ?? throw new NotSupportedException("Effect69 stage drawing requires its actual local-pose restore."))(frame, group, null);
        NativeBattleEffect69StageSource source = Source(frame, group, inputs);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        NativeBattleEffectArena context = ReadBytes(frame, temporary, 112);
        context = Dword(Dword(Dword(context, 4, source.ScratchAddress), 0x18, metricPointer), 0x1C, unchecked(arena + 0x5550));
        context = Word(Word(Word(Word(Word(Word(context, 0xC, 0), 0xE, 0), 0x10, 320), 0x12, 216), 0x14, threshold), 0x16, randomMask);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect69 stage drawing requires its current GTE controls.");
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int item = 0; item < source.RawGeometry.Length; item++)
        {
            NativeBattleRawMesh mesh = source.RawGeometry[item];
            RequireScratch(source, mesh.Vertices.Length, 16);
            int vertex = 0;
            foreach (NativeBattleRawVertexGroup packed in Groups(mesh))
            {
                registers = LoadBone(registers, source, packed.Bone);
                int start = vertex;
                for (int index = 0; index < packed.Count; index++)
                {
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Vector(packed.Bytes.AsSpan().Slice(index * 6, 6)), 0));
                    NativeBattleVector transformed = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, transformed.X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), transformed.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), transformed.Z);
                    frame = WriteStaging(frame, checked(source.ScratchAddress + (uint)(vertex * 16 + 8)), xyz);
                    vertex++;
                }
                NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, true, Padding(view)) };
                for (int index = start; index < vertex; index++)
                {
                    uint row = checked(source.ScratchAddress + (uint)(index * 16));
                    (byte[] packedVector, bool[] vectorKnown) = Staging(frame).ReadSnapshot(row + 8, 8);
                    registers = registers with { Colors = registers.Colors with { NormalInput = packedVector.ToImmutableArray(), NormalInputKnown = vectorKnown.ToImmutableArray() } };
                    registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                    uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect69 stage projection requires its current SXY2.");
                    ushort depth = unchecked((ushort)((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect69 stage projection requires its current SZ3.")) >> 2));
                    context = Dword(Dword(context, 0x20, screen), 0x24, depth);
                    if (depth == 0)
                    {
                        frame = WriteStaging(frame, row + 6, BitConverter.GetBytes((ushort)0x10));
                    }
                    else
                    {
                        short x = unchecked((short)screen);
                        short y = unchecked((short)(screen >> 16));
                        ushort flags = unchecked((ushort)((x < 0 ? 1 : x >= 320 * 8 ? 2 : 0) | (y < 0 ? 4 : y >= 216 * 8 ? 8 : 0)));
                        context = Word(context, 0x26, flags);
                        frame = WriteStaging(frame, row, BitConverter.GetBytes(screen));
                        frame = WriteStaging(frame, row + 4, BitConverter.GetBytes((uint)depth | ((uint)flags << 16)));
                    }
                }
            }
            context = Dword(context, 0, FaceAddress(frame, source, item, mesh));
            context = Word(Word(context, 8, mesh.Polygons.Count(face => !face.Quad)), 10, mesh.Polygons.Count(face => face.Quad));
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                int metric = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(frame, metricPointer, 4).Read(0, 4));
                uint currentMetric = metricPointer;
                metricPointer = unchecked(metricPointer + 4);
                context = Dword(context, 0x18, metricPointer);
                if (metric == -1)
                {
                    continue;
                }
                int count = face.Quad ? 4 : 3;
                for (int corner = 0; corner < count; corner++)
                {
                    int index = FaceIndex(face, corner, vertex);
                    NativeBattleEffectArena row = NativeBattleTemporaryMemory.ReadView(Staging(frame), checked(source.ScratchAddress + (uint)(index * 16)), 16);
                    context = NativeBattleEffectNodeContextMemory.Copy(row, 0, context, 0x20 + corner * 16, 16);
                }
                if (metric < threshold)
                {
                    frame = DetachFace(frame, context, face, currentMetric, randomMask, arena, inputs.CapturePresentation);
                    continue;
                }
                byte union = 0;
                byte common = 0xFF;
                uint[] screens = new uint[count];
                uint[] depths = new uint[count];
                for (int corner = 0; corner < count; corner++)
                {
                    int offset = 0x20 + corner * 16;
                    byte flags = context.Read(offset + 6, 1)[0];
                    union |= flags;
                    common &= flags;
                    screens[corner] = Dword(context, offset);
                    depths[corner] = Word(context, offset + 4);
                }
                if (union >= 0x10 || common != 0)
                {
                    continue;
                }
                registers = registers with { Geometry = registers.Geometry with { Sxy = [screens[0], screens[1], screens[2]] } };
                registers = NativeBattleGeometry.Clip45EE10(registers);
                uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect69 stage drawing requires its current NCLIP output.");
                context = Dword(context, 0x60, area);
                if (unchecked((int)area) < 0)
                {
                    continue;
                }
                registers = registers with { Geometry = registers.Geometry with { Sz = face.Quad ? [depths[0], depths[1], depths[2], depths[3]] : [registers.Geometry.Sz[0], depths[0], depths[1], depths[2]] } };
                registers = face.Quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect69 stage drawing requires its current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect69 stage drawing requires its current ZSF3."));
                uint otz = registers.Geometry.Otz ?? throw new NotSupportedException("Effect69 stage drawing requires its current OTZ.");
                context = Dword(context, 0x68, otz);
                SkinWrites packet = new(face.Quad ? 40 : 32, allocation);
                packet.Word(0, face.Quad ? 0x09000000u : 0x07000000u);
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(face.Quad ? 20 : 16));
                uint material = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(14)) & 0x200) << 16;
                packet.Word(4, color | material);
                for (int corner = 0; corner < count; corner++)
                {
                    packet.Word(8 + corner * 8, screens[corner]);
                }
                packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(8)));
                packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(12)) & 0xFDFFFFFF);
                packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(face.Quad ? 16 : 6)));
                if (face.Quad)
                {
                    packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(18)));
                }
                int bucket = unchecked(tableBaseBucket + (unchecked((int)otz) >> depthShift));
                ImmutableArray<int> sidecars = face.Quad ? depths.Select(value => (int)value).ToImmutableArray() : [(int)depths[0], (int)depths[1], (int)depths[2], 0];
                NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), sidecars);
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualIdentity owner = inputs.PilotIdentity ?? throw new InvalidDataException("Effect69 stage capture requires its executing task.");
                    System.Numerics.Vector3 Point(int corner) => new(Signed(context, 0x28 + corner * 16), Signed(context, 0x2A + corner * 16), Signed(context, 0x2C + corner * 16));
                    primitive = primitive with { PilotGeometry = new(owner with { Primitive = unchecked((int)currentMetric), ResourceGeneration = source.GeometryAddress }, Point(0), Point(1), Point(2), face.Quad ? Point(3) : default, (byte)count) { SourceDepths = new(sidecars[0], sidecars[1], sidecars[2], sidecars[3]), UseSourceDepths = true } };
                }
                attempts.Add(packet.Attempt(primitive));
                allocation += face.Quad ? 40 : 32;
            }
            context = Word(Word(context, 8, -1), 10, -1);
        }
        frame = Publish(frame, registers);
        frame = WriteBytes(frame, temporary, context, 112);
        frame = inputs.Execute(frame, new NativeBattleEffect69DeliverCommonBatch(new(attempts.ToImmutable(), allocation)));
        frame = Release(frame, 112);
        return new(frame, metricPointer);
    }

    private static NativeBattleEffect69Frame DetachFace(NativeBattleEffect69Frame frame, NativeBattleEffectArena context, NativeBattleRawPolygon face, uint metricAddress, ushort mask, uint arena, bool capturePresentation)
    {
        uint address;
        (frame, address) = AllocateDebris6FFFE0(frame, arena);
        if (address == 0)
        {
            return frame;
        }
        if (capturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena ?? throw new InvalidDataException("Effect69 capture requires its debris arena owner."), arena, checked((int)((address - arena) / 60)));
        }
        NativeBattleEffectArena debris = ReadBytes(frame, address, 0x3C);
        int corners = face.Quad ? 4 : 3;
        int[] center = new int[3];
        for (int axis = 0; axis < 3; axis++)
        {
            int sum = 0;
            for (int corner = 0; corner < corners; corner++)
            {
                sum += Signed(context, 0x28 + corner * 16 + axis * 2);
            }
            center[axis] = sum / corners;
            debris = Word(debris, axis * 2, center[axis]);
        }
        debris = Word(debris, 6, face.Quad ? 2 : 1);
        for (int corner = 0; corner < corners; corner++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                debris = Word(debris, 0x14 + corner * 6 + axis * 2, Signed(context, 0x28 + corner * 16 + axis * 2) - center[axis]);
            }
        }
        int random;
        (frame, random) = Random(frame);
        debris = Word(debris, 8, (random & mask) - (unchecked((short)mask) >> 1));
        (frame, random) = Random(frame);
        debris = Word(debris, 12, (random & mask) - (unchecked((short)mask) >> 1));
        (frame, random) = Random(frame);
        debris = Word(debris, 10, -4 * (mask + (random & mask)));
        (frame, random) = Random(frame);
        debris = Word(debris, 14, (random & 7) + 12);
        debris = debris.Write(0x2C, face.Bytes.AsSpan().Slice(8, 4)).Write(0x30, face.Bytes.AsSpan().Slice(12, 4));
        debris = Word(debris, 0x34, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(face.Quad ? 16 : 6)));
        if (face.Quad)
        {
            debris = Word(debris, 0x36, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(18)));
        }
        debris = debris.Write(0x38, face.Bytes.AsSpan().Slice(face.Quad ? 20 : 16, 4));
        (frame, random) = Random(frame);
        debris = Dword(debris, 0x10, unchecked(arena + 0x5550 + (uint)((random & 0x3F) * 20)));
        frame = WriteBytes(frame, address, debris, 0x3C);
        return WriteBytes(frame, metricAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(-1)), 4);
    }

    private static (NativeBattleEffect69Frame Frame, uint Address) AllocateDebris6FFFE0(NativeBattleEffect69Frame frame, uint arena)
    {
        uint cached = frame.State.ReadDword(DebrisCursorGlobal);
        uint selected = 0;
        if (cached != 0)
        {
            if (cached < arena || cached >= (ulong)arena + 0x5550 || (cached - arena) % 0x3C != 0)
            {
                throw new InvalidDataException("Effect69 retained debris cursor leaves its physical record bank.");
            }
            if (Word(ReadBytes(frame, cached + 6, 2), 0) == 0)
            {
                selected = cached;
            }
        }
        if (selected == 0)
        {
            for (int index = 0; index < 364; index++)
            {
                uint address = unchecked(arena + (uint)(index * 0x3C));
                if (Word(ReadBytes(frame, address + 6, 2), 0) == 0)
                {
                    selected = address;
                    break;
                }
            }
        }
        if (selected == 0)
        {
            return (frame, 0);
        }
        uint next = selected >= unchecked(arena + 0x5514) ? arena : unchecked(selected + 0x3C);
        return (frame with { State = frame.State.WriteDword(DebrisCursorGlobal, next) }, selected);
    }

    private static (NativeBattleEffect69Frame Frame, int Value) Random(NativeBattleEffect69Frame frame)
    {
        uint next = unchecked(frame.State.ReadDword(RandomGlobal) * 125 + 14) & 0x7FFF;
        return (frame with { State = frame.State.WriteDword(RandomGlobal, next) }, (int)next);
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena data, int offset, int value) => NativeBattleEffect69StageKernel.Word(data, offset, value);
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena data, int offset, uint value) => NativeBattleEffect69StageKernel.Dword(data, offset, value);
    private static ushort Word(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Read(offset, 4));
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
}
