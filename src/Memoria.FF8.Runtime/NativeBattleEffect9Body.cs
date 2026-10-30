using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9BodyDraw(NativeBattleEffect9Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect9Body
{
    private const uint VertexAddress = 0x274F638;

    public static int Gray(short coordinate, short lower, short upper, short shade)
    {
        if (coordinate <= lower)
        {
            return shade;
        }
        short span = unchecked((short)(upper - lower));
        int divisor = span > 0 ? span : 1;
        short ratio = unchecked((short)(unchecked((coordinate - lower) * shade) / divisor));
        ratio = ratio > shade ? shade : ratio < 0 ? (short)0 : ratio;
        return unchecked((short)(shade - ratio));
    }

    public static NativeBattleEffect9BodyDraw Draw(NativeBattleEffect9Frame frame, ImmutableArray<NativeBattleCureActorGeometry> meshes, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short lower, short upper, short shade, uint objectMask, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect9 body requires its current depth controls.");
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect9 body requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect9 body requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect9 body requires its current physical temporary storage."), 0x84);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 0x84);
        temporary = PutDword(temporary, 0x48, objectMask);
        temporary = PutWord(temporary, 0x7A, shade);
        temporary = PutWord(temporary, 0x7C, lower);
        short span = unchecked((short)(upper - lower));
        temporary = PutWord(temporary, 0x7E, span > 0 ? span : 1);
        temporary = temporary.Write(0x80, [0x36, 0x3E]);
        temporary = PutDword(temporary, 0x74, unchecked((uint)(shade + (shade << 8) + (shade << 16))));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleCureActorGeometry geometry in meshes)
        {
            NativeBattleRawMesh mesh = geometry.Mesh;
            mesh.Validate();
            if ((long)mesh.Vertices.Length * 12 > 0x5DC0)
            {
                throw new InvalidDataException("Effect9 posed vertex rows leave their physical work before the particle bank.");
            }
            for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
            {
                NativeBattleRawVertex source = mesh.Vertices[vertex];
                if (source.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Effect9 vertex references an absent local body bone.");
                }
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
                frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory, NativeBattlePackedMatrix32.FromMatrix(bones[source.Bone], null)) };
                registers = NativeBattleGeometry.LoadVector(new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!), 0, source.Position, null);
                frame = frame with { Memory = NativeBattleLightBackTransform.Transform(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, 0) };
                registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!);
                NativeBattleVector point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, point.X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), point.Z);
                temporary = temporary.Write(0x28, xyz);
                int row = checked((int)(VertexAddress - NativeBattleEffect9State.BaseAddress) + vertex * 12);
                NativeBattleEffectArena globals = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x28, frame.State.Globals, row, 8);
                int gray = Gray(point.Y, lower, upper, shade);
                if (point.Y > lower)
                {
                    temporary = PutWord(temporary, 0x78, gray);
                }
                uint color = unchecked((uint)(gray + (gray << 8) + (gray << 16)));
                frame = frame with { State = frame.State with { Globals = globals } };
                frame = frame with { State = frame.State.WriteDword(VertexAddress + (uint)(vertex * 12 + 8), color) };
            }
            int faceIndex = 0;
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons.Where(face => !face.Quad && face.Bytes.Length == 16).Concat(mesh.Polygons.Where(face => face.Quad && face.Bytes.Length == 20)))
            {
                int authoredFace = faceIndex++;
                ImmutableArray<byte> face = geometry.AlreadyRelocated ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, geometry.Binding);
                int count = polygon.Quad ? 4 : 3;
                int[] indices = Enumerable.Range(0, count).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(corner * 2)) & 0xFFF).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    temporary = PutDword(temporary, 0x38 + corner * 4, unchecked((uint)indices[corner]));
                    temporary = NativeBattleEffectNodeContextMemory.Copy(frame.State.Globals, checked((int)(VertexAddress - NativeBattleEffect9State.BaseAddress) + indices[corner] * 12), temporary, 0x4C + corner * 8, 8);
                    registers = Load(corner, indices[corner]);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                registers = NativeBattleGeometry.Clip45EE10(registers);
                temporary = PutDword(temporary, 0x6C, registers.Geometry.Mac0!.Value);
                if (unchecked((int)(registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect9 body requires its current face area."))) <= 0)
                {
                    continue;
                }
                uint[] screens = new uint[count];
                int[] depths = new int[count];
                for (int corner = 0; corner < 3; corner++)
                {
                    screens[corner] = registers.Geometry.Sxy[corner] ?? throw new NotSupportedException("Effect9 body requires its projected screen FIFO.");
                    depths[corner] = checked((int)(registers.Geometry.Sz[corner + 1] ?? throw new NotSupportedException("Effect9 body requires its projected depth FIFO.")));
                }
                if (polygon.Quad)
                {
                    temporary = PutDword(temporary, 0x44, unchecked((uint)indices[3]));
                    temporary = NativeBattleEffectNodeContextMemory.Copy(frame.State.Globals, checked((int)(VertexAddress - NativeBattleEffect9State.BaseAddress) + indices[3] * 12), temporary, 0x64, 8);
                    registers = Load(0, indices[3]);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    screens[3] = registers.Geometry.Sxy[2]!.Value;
                    depths[3] = checked((int)registers.Geometry.Sz[3]!.Value);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect9 body requires its current ZSF4."));
                }
                else
                {
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect9 body requires its current ZSF3."));
                }
                temporary = PutDword(temporary, 0x70, registers.Geometry.Otz!.Value >> 2);
                int bucket = checked(17 + (int)(registers.Geometry.Otz!.Value >> 2));
                SkinWrites packet = new(polygon.Quad ? 52 : 40, allocated);
                packet.Word(0, polygon.Quad ? 0x0C000000u : 0x09000000u);
                for (int corner = 0; corner < count; corner++)
                {
                    uint color = frame.State.ReadDword(VertexAddress + (uint)(indices[corner] * 12 + 8));
                    if (corner == 0)
                    {
                        color = (color & 0xFFFFFF) | (polygon.Quad ? 0x3E000000u : 0x36000000u);
                    }
                    packet.Word(4 + corner * 12, color);
                    packet.Word(8 + corner * 12, screens[corner]);
                }
                packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan().Slice(8)));
                packet.Word(24, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan().Slice(12)));
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(polygon.Quad ? 16 : 6)));
                if (polygon.Quad)
                {
                    packet.Word16(48, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(18)));
                }
                ImmutableArray<int> payload = depths.ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), polygon.Quad ? payload : payload.Add(0));
                if (pilotIdentity is { } identity)
                {
                    // Only reached corners are read, after the native front-face rejection.
                    System.Numerics.Vector3 World(int corner)
                    {
                        byte[] value = frame.State.Globals.Read(checked((int)(VertexAddress - NativeBattleEffect9State.BaseAddress) + indices[corner] * 12), 6);
                        NativeBattleVector posed = new(BinaryPrimitives.ReadInt16LittleEndian(value), BinaryPrimitives.ReadInt16LittleEndian(value.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(value.AsSpan(4)));
                        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, posed), projection);
                    }
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((identity.Primitive * 397 + geometry.AuthoredMesh) * 397 + authoredFace) }, World(0), World(1), World(2), polygon.Quad ? World(3) : default, checked((byte)count)) { SourceDepths = new(depths[0], depths[1], depths[2], polygon.Quad ? depths[3] : 0), UseSourceDepths = true } };
                }
                attempts.Add(packet.Attempt(primitive));
                allocated = checked(allocated + (polygon.Quad ? 52 : 40));

                NativeBattleGeometryRegisterStep Load(int slot, int index)
                {
                    if ((uint)index >= mesh.Vertices.Length)
                    {
                        throw new InvalidDataException("Effect9 face references an absent current posed vertex.");
                    }
                    int row = checked((int)(VertexAddress - NativeBattleEffect9State.BaseAddress) + index * 12);
                    byte[] value = frame.State.Globals.Read(row, 6);
                    (byte[] padding, bool[] known) = frame.State.Globals.ReadSnapshot(row + 6, 2);
                    short? fourth = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
                    return NativeBattleGeometry.LoadVector(registers, slot, new(BinaryPrimitives.ReadInt16LittleEndian(value), BinaryPrimitives.ReadInt16LittleEndian(value.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(value.AsSpan(4))), fourth);
                }
            }
        }
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 0x84);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x84) };
        return new(frame, new(attempts.ToImmutable(), allocated));
    }
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena memory, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return memory.Write(offset, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena memory, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return memory.Write(offset, bytes);
    }

}
