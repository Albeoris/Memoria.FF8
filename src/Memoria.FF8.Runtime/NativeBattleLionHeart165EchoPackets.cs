using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165EchoDraw(NativeBattleLionHeart165Frame Frame, NativeBattleGfGeometryBatch Batch, uint PacketCursor);

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165EchoDraw DrawEchoMesh5EF0C0(NativeBattleLionHeart165Frame frame, uint temporary, uint verticesAddress, uint polygonAddress, NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleShivaPlane plane, ImmutableArray<int> selectors, uint color, uint orderingTable, uint packetCursor, short zsf3, short zsf4, short dqa, int dqb, NativeBattleModelTextureBinding? textureBinding = null, Action<ushort>? observeDepth = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        mesh.Validate();
        if (selectors.IsDefault || selectors.Length != 56 || selectors.Any(value => value is < 0 or > 5))
        {
            throw new InvalidDataException("Native Lion Heart echo requires its complete clipping selector table.");
        }
        NativeBattleFileStaging staging = frame.Staging;
        staging = WriteDword(WriteDword(WriteDword(WriteDword(WriteDword(WriteDword(staging, temporary + 4, verticesAddress), temporary + 16, color), temporary + 48, color), temporary + 60, orderingTable), temporary + 64, 2), temporary + 68, packetCursor);
        for (int index = 0; index < selectors.Length; index++)
        {
            staging = WriteDword(staging, temporary + 200 + (uint)index * 4, temporary + 88 + (uint)selectors[index] * 16);
        }
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (vertex.Bone >= bones.Length)
            {
                throw new InvalidDataException("Native Lion Heart echo vertex leaves its actual posed skeleton.");
            }
            staging = Point(staging, verticesAddress + (uint)index * 8, NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position));
        }
        int plainTriangles = mesh.Polygons.Count(polygon => !polygon.Quad && polygon.Bytes.Length == 16);
        int plainQuads = mesh.Polygons.Count(polygon => polygon.Quad && polygon.Bytes.Length == 20);
        int coloredTriangles = mesh.Polygons.Count(polygon => !polygon.Quad && polygon.Bytes.Length == 20);
        int coloredQuads = mesh.Polygons.Count(polygon => polygon.Quad && polygon.Bytes.Length == 24);
        staging = WriteDword(staging, temporary, polygonAddress);
        staging = WriteWord(WriteWord(WriteWord(WriteWord(staging, temporary + 8, checked((ushort)plainTriangles)), temporary + 10, checked((ushort)plainQuads)), temporary + 12, checked((ushort)coloredTriangles)), temporary + 14, checked((ushort)coloredQuads));
        frame = frame with { Staging = staging };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int polygonIndex = -1;
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
        {
            polygonIndex++;
            if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
            {
                continue;
            }
            ImmutableArray<byte> relocated = textureBinding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding);
            ReadOnlySpan<byte> bytes = relocated.AsSpan();
            uint material0 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8));
            uint material1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12));
            NativeBattleShivaClipVertex first = ReadVertex(bytes, 0, 8);
            NativeBattleShivaClipVertex second = ReadVertex(bytes, 2, 12);
            NativeBattleShivaClipVertex third = ReadVertex(bytes, 4, polygon.Quad ? 16 : 6);
            NativeBattleFileStaging current = WriteDword(WriteDword(frame.Staging, temporary + 52, material0), temporary + 56, material1);
            frame = frame with { Staging = current };
            if (ProcessTriangle([first, second, third], true))
            {
                if (polygon.Quad)
                {
                    NativeBattleShivaClipVertex fourth = ReadVertex(bytes, 6, 18);
                    _ = ProcessTriangle([fourth, second, third], false);
                }
            }
        }
        return new(frame, new(attempts.ToImmutable(), allocated), checked(packetCursor + (uint)allocated));

        NativeBattleShivaClipVertex ReadVertex(ReadOnlySpan<byte> face, int offset, int uv)
        {
            int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(offset)) & 0xFFF;
            uint address = checked(verticesAddress + (uint)index * 8);
            return new(new(ReadWord(frame.Staging, address), ReadWord(frame.Staging, address + 2), ReadWord(frame.Staging, address + 4)), BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(uv)));
        }
        bool ProcessTriangle(ImmutableArray<NativeBattleShivaClipVertex> triangle, bool firstTriangle)
        {
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[3];
            NativeBattleFileStaging current = frame.Staging;
            for (int index = 0; index < 3; index++)
            {
                uint destination = temporary + 88 + (uint)index * 16;
                NativeBattleShivaClipVertex vertex = triangle[index];
                current = WriteWord(Point(current, destination, vertex.Position), destination + 6, vertex.Uv);
                projected[index] = NativeBattleModuleProjection.Project45FE10(view, vertex.Position, projection);
                observeDepth?.Invoke(projected[index].Depth);
            }
            uint flags = projected[0].Flags | projected[1].Flags | projected[2].Flags;
            current = WriteDword(current, temporary + 84, flags);
            frame = frame with { Staging = current };
            if (firstTriangle)
            {
                if ((flags & 0x60000) != 0)
                {
                    return false;
                }
                int area = NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen);
                frame = frame with { Staging = WriteDword(frame.Staging, temporary + 72, unchecked((uint)area)) };
                if (area <= 0)
                {
                    return false;
                }
            }
            short[] distances = triangle.Select(vertex => NativeBattleShivaPlaneClipping.Distance460860(vertex.Position, plane)).ToArray();
            int crossing = 0;
            current = frame.Staging;
            for (int index = 0; index < 3; index++)
            {
                int bit = distances[index] != 0 && (distances[index] ^ distances[(index + 1) % 3]) < 0 ? 1 << index : 0;
                crossing |= bit;
                current = WriteDword(WriteDword(current, temporary + 144 + (uint)index * 16, (uint)bit), temporary + 148 + (uint)index * 16, unchecked((uint)(int)distances[index]));
            }
            current = WriteDword(current, temporary + 44, (uint)crossing);
            if (crossing != 0)
            {
                for (int index = 0; index < 3; index++)
                {
                    if ((crossing & (1 << index)) == 0)
                    {
                        continue;
                    }
                    NativeBattleShivaClipVertex intersection = NativeBattleShivaPlaneClipping.Intersect5C35F0(triangle[index], triangle[(index + 1) % 3], plane);
                    uint destination = temporary + 136 + (uint)index * 16;
                    current = WriteWord(Point(current, destination, intersection.Position), destination + 6, intersection.Uv);
                }
            }
            frame = frame with { Staging = current };
            ImmutableArray<NativeBattleShivaClipVertex> clipped = NativeBattleShivaPlaneClipping.Clip5C34B0(triangle, plane, selectors);
            if (!clipped.IsEmpty)
            {
                NativeBattleGfGeometryAttempt emitted = NativeBattleShivaPlaneClipping.Emit5C3410(clipped, view, projection, color, unchecked((uint)ReadDword(frame.Staging, temporary + 52)), unchecked((uint)ReadDword(frame.Staging, temporary + 56)), zsf3, zsf4, dqa, dqb, allocated, observeDepth);
                if (pilotIdentity is { } identity && emitted.Primitive is { } primitive)
                {
                    System.Numerics.Vector3 World(int index) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(view, clipped[index].Position), projection);
                    int topology = NativeBattleShivaVisualIdentity.ClipTopology(triangle.Select(vertex => vertex.Position), plane);
                    NativeBattlePilotVisualIdentity faceIdentity = identity with { Primitive = unchecked((identity.Primitive * 65537 + polygonIndex * 2 + (firstTriangle ? 0 : 1)) * 31 + topology) };
                    emitted = emitted with { Primitive = primitive with { PilotGeometry = new(faceIdentity, World(0), World(1), World(2), clipped.Length == 4 ? World(3) : default, checked((byte)clipped.Length)) } };
                }
                attempts.Add(emitted);
                allocated = checked(allocated + emitted.PacketLength);
                uint average = 0;
                foreach (NativeBattleShivaClipVertex vertex in clipped)
                {
                    average += NativeBattleModuleProjection.Project45FE10(view, vertex.Position, projection).Depth;
                }
                average = unchecked(average * (uint)(int)(clipped.Length == 4 ? zsf4 : zsf3)) >> 12;
                current = WriteDword(WriteDword(frame.Staging, temporary + 80, average), temporary + 68, checked(packetCursor + (uint)allocated));
                frame = frame with { Staging = current };
            }
            return true;
        }
    }
}
