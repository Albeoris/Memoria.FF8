using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWall189ClipPackets
{
    public static NativeBattleModuleMeshTemporaryResult Emit5BD1C0(NativeBattleFileStaging staging, uint temporary, NativeBattleWall189Resources resources, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short age, uint orderingTable, uint packetCursor, NativeBattleScriptedGraphicsMemory registers, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleStaticMeshResource mesh = resources.Shield;
        NativeBattleShivaPlane plane = NativeBattleShivaPlaneClipping.CreatePlane5C3130(new(0, unchecked((short)(2013 * age / 20 - 2013)), 0), new(0, -4096, 0));
        Word(88, 0);
        Word(90, unchecked((short)(2013 * age / 20 - 2013)));
        Word(92, 0);
        Word(184, plane.X);
        Word(186, plane.Y);
        Word(188, plane.Z);
        Word(190, plane.W);
        Dword(28, mesh.Address);
        Dword(32, checked(mesh.Address + 8));
        Dword(36, 0);
        Dword(60, orderingTable);
        Dword(64, 2);
        Dword(68, packetCursor);
        if (resources.Selectors.IsDefault || resources.Selectors.Length != 56 || resources.Selectors.Any(value => value is < 0 or > 5))
        {
            throw new InvalidDataException("Native Wall requires its actual clipping-selector directory.");
        }
        for (int index = 0; index < resources.Selectors.Length; index++)
        {
            Dword(200 + index * 4, checked(temporary + 88 + (uint)resources.Selectors[index] * 16));
        }
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 8);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
        cursor += 4;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            ReadOnlySpan<byte> face = mesh.Bytes.AsSpan().Slice(cursor, 20);
            NativeBattleShivaClipVertex first = Vertex(face, 4, 12, 88);
            NativeBattleShivaClipVertex second = Vertex(face, 6, 16, 104);
            NativeBattleShivaClipVertex third = Vertex(face, 8, 10, 120);
            ImmutableArray<NativeBattleShivaClipVertex> triangle = [first, second, third];
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(face) & 0x02FFFFFF;
            uint material0 = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(12));
            uint material1 = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(16));
            Dword(48, color);
            Dword(52, material0);
            Dword(56, material1);
            uint flags = 0;
            foreach (NativeBattleShivaClipVertex vertex in triangle)
            {
                flags |= NativeBattleModuleProjection.Project45FE10(matrix, vertex.Position, projection).Flags;
            }
            Dword(84, flags);
            if ((flags & 0x60000) == 0)
            {
                short[] distances = triangle.Select(vertex => NativeBattleShivaPlaneClipping.Distance460860(vertex.Position, plane)).ToArray();
                int crossing = 0;
                for (int index = 0; index < 3; index++)
                {
                    int bit = distances[index] != 0 && (distances[index] ^ distances[(index + 1) % 3]) < 0 ? 1 << index : 0;
                    crossing |= bit;
                    Dword(144 + index * 16, unchecked((uint)bit));
                    Dword(148 + index * 16, unchecked((uint)(int)distances[index]));
                }
                Dword(44, unchecked((uint)crossing));
                if (crossing != 0)
                {
                    for (int index = 0; index < 3; index++)
                    {
                        if ((crossing & (1 << index)) != 0)
                        {
                            Point(136 + index * 16, NativeBattleShivaPlaneClipping.Intersect5C35F0(triangle[index], triangle[(index + 1) % 3], plane));
                        }
                    }
                }
                ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0(triangle, plane, resources.Selectors);
                if (!clipped.IsEmpty)
                {
                    short zsf3 = registers.Zsf3 ?? throw new NotSupportedException("Native Wall clipping requires its actual ZSF3 register.");
                    short zsf4 = registers.Zsf4 ?? throw new NotSupportedException("Native Wall clipping requires its actual ZSF4 register.");
                    NativeBattleGfGeometryAttempt emitted = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, matrix, projection, color, material0, material1, zsf3, zsf4, registers.Dqa, registers.Dqb, allocated);
                    if (emitted.Primitive is { } primitive && pilotIdentity is { } identity)
                    {
                        System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, clipped[corner].Position), projection);
                        emitted = emitted with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, World(0), World(1), World(2), clipped.Length == 4 ? World(3) : default, checked((byte)clipped.Length)) } };
                    }
                    attempts.Add(emitted);
                    allocated = checked(allocated + emitted.PacketLength);
                    uint total = 0;
                    foreach (NativeBattleShivaClipVertex vertex in clipped)
                    {
                        total += NativeBattleModuleProjection.Project45FE10(matrix, vertex.Position, projection).Depth;
                    }
                    Dword(80, unchecked(total * (uint)(int)(clipped.Length == 4 ? zsf4 : zsf3)) >> 12);
                    Dword(68, checked(packetCursor + (uint)allocated));
                }
            }
            cursor += 20;
            Dword(40, checked(mesh.Address + (uint)cursor));
        }
        if (BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)) != 0)
        {
            throw new NotSupportedException("Native Wall clip resource reached its unproved FT4 directory.");
        }
        Dword(40, checked(mesh.Address + (uint)cursor + 4));
        return new(staging, new(attempts.ToImmutable(), allocated));

        NativeBattleShivaClipVertex Vertex(ReadOnlySpan<byte> face, int indexOffset, int uvOffset, int destination)
        {
            int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(indexOffset)) * 4);
            ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(offset, 8);
            staging = staging.Write(checked(temporary + (uint)destination), bytes);
            NativeBattleShivaClipVertex vertex = new(new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4))), BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(uvOffset)));
            Word(destination + 6, unchecked((short)vertex.Uv));
            return vertex;
        }
        void Point(int offset, NativeBattleShivaClipVertex vertex)
        {
            Word(offset, vertex.Position.X);
            Word(offset + 2, vertex.Position.Y);
            Word(offset + 4, vertex.Position.Z);
            Word(offset + 6, unchecked((short)vertex.Uv));
        }
        void Word(int offset, short value)
        {
            staging = staging.Write(checked(temporary + (uint)offset), BitConverter.GetBytes(value));
        }
        void Dword(int offset, uint value)
        {
            staging = staging.Write(checked(temporary + (uint)offset), BitConverter.GetBytes(value));
        }
    }
}
