using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291TrailDrawing
{
    public static NativeBattleActionMatrix Prepare6F1650(ReadOnlySpan<byte> record, ImmutableArray<byte> trigonometry, NativeBattleActionMatrix view)
    {
        if (record.Length < 24 || trigonometry.IsDefault || trigonometry.Length != 0x4000)
        {
            throw new InvalidDataException("Native Pandemona trails require their current packed record and complete lookup table.");
        }
        short scale = BinaryPrimitives.ReadInt16LittleEndian(record[0xE..]);
        int angleX = BinaryPrimitives.ReadUInt16LittleEndian(record[0x10..]) & 0xFFF;
        int sine = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angleX * 4)) * scale) >> 12;
        int cosine = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angleX * 4 + 2)) * scale) >> 12;
        NativeBattleActionMatrix x = new([scale, 0, 0, 0, unchecked((short)cosine), unchecked((short)sine), 0, unchecked((short)-sine), unchecked((short)cosine)], 0, 0, 0, 4096);
        int angleZ = BinaryPrimitives.ReadUInt16LittleEndian(record[0x12..]) & 0xFFF;
        short sz = BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angleZ * 4));
        short cz = BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angleZ * 4 + 2));
        NativeBattleActionMatrix z = new([cz, unchecked((short)-sz), 0, sz, cz, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.MultiplyGteRotation45E470(x, z);
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, local);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]));
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(view, point);
        return camera with { X = translation.X, Y = translation.Y, Z = translation.Z };
    }

    public static NativeBattleGfGeometryBatch Draw6F17C0(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short zsf3, Action<ImmutableArray<byte>>? observeInput = null, Action<uint>? observeFlags = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (mesh.Bytes.IsDefault || mesh.Bytes.Length < 8)
        {
            throw new InvalidDataException("Native Pandemona trail drawing requires its current original mesh.");
        }
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 24);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
        cursor = checked(cursor + 4);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            byte[] face = mesh.Bytes.AsSpan().Slice(cursor, 28).ToArray();
            NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[3];
            System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[3] : null;
            for (int corner = 0; corner < 3; corner++)
            {
                int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                ReadOnlySpan<byte> vector = mesh.Bytes.AsSpan().Slice(offset, 6);
                NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(vector), BinaryPrimitives.ReadInt16LittleEndian(vector[2..]), BinaryPrimitives.ReadInt16LittleEndian(vector[4..]));
                if (corner == 0)
                {
                    observeInput?.Invoke(mesh.Bytes.AsSpan().Slice(offset, 8).ToArray().ToImmutableArray());
                }
                vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
                if (world is not null)
                {
                    world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                }
            }
            uint flags = vertices[0].Flags | vertices[1].Flags | vertices[2].Flags;
            observeFlags?.Invoke(flags);
            byte[] data = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            Store(0, 0x09000000);
            Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
            Store(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)));
            Store(24, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)));
            Store(36, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10)));
            NativeBattleGeometryPrimitive? primitive = null;
            if ((flags & 0x60000) == 0)
            {
                int nclip = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen);
                Store(8, vertices[0].Screen);
                Store(20, vertices[1].Screen);
                Store(32, vertices[2].Screen);
                Store(16, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)));
                Store(28, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(24)));
                if (nclip >= 0)
                {
                    uint sum = unchecked((uint)(vertices[0].Depth + vertices[1].Depth + vertices[2].Depth));
                    uint average = unchecked(sum * (uint)(int)zsf3) >> 12;
                    bool outsideX = vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00);
                    bool outsideY = vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0);
                    if (!outsideX && !outsideY)
                    {
                        ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                        primitive = new(checked(17 + (int)(average >> 2)), words, [vertices[0].Depth, vertices[1].Depth, vertices[2].Depth, 0]);
                        if (pilotIdentity is { } identity && world is not null)
                        {
                            primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, world[0], world[1], world[2], default, 3) };
                        }
                    }
                }
            }
            NativeBattleGfGeometryAttempt attempt = new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);
            if (primitive is not null)
            {
                allocated = checked(allocated + 40);
            }
            void Store(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
            attempts.Add(attempt);
            cursor = checked(cursor + 28);
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
