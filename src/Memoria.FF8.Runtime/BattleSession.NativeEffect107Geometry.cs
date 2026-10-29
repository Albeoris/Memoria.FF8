using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>6D1C30's screen-space deformation,6BCB50 body packets and6D23D0 hierarchy packets.</summary>
public static class NativeBattleEffect107Geometry
{
    internal static NativeBattlePilotVisualGeometry RebaseCameraGeometry(NativeBattlePilotVisualGeometry geometry, NativeBattleCameraProjection projection)
    {
        System.Numerics.Vector3 World(System.Numerics.Vector3 point) => NativeBattlePilotVisualGeometry.CameraToWorld(point, projection);
        NativeBattlePilotSpriteGeometry? sprite = geometry.Sprite;
        if (sprite is not null)
        {
            sprite = sprite with { Origin = World(sprite.Origin), AxisX = NativeBattlePilotVisualGeometry.CameraToWorldDirection(sprite.AxisX, projection), AxisY = NativeBattlePilotVisualGeometry.CameraToWorldDirection(sprite.AxisY, projection), AxisZ = NativeBattlePilotVisualGeometry.CameraToWorldDirection(sprite.AxisZ, projection) };
        }
        return geometry with { A = World(geometry.A), B = World(geometry.B), C = World(geometry.C), D = geometry.CornerCount == 4 ? World(geometry.D) : default, Sprite = sprite };
    }

    public static NativeBattleProjectedVertex Deform6D1C30(NativeBattleModuleProjectedVertex projected, short centerX, short centerY, short radius, ref short extent, int intensity, Func<uint, int, ImmutableArray<byte>> readResource)
    {
        if (projected.Depth == 0)
        {
            //The original writes only the rejection WORD at scratch+6; rejected corners never consume its retained XY/depth.
            return new(0, 0x100000);
        }
        short x = unchecked((short)projected.Screen);
        short y = unchecked((short)(projected.Screen >> 16));
        short pixelX = (short)(x >> 3);
        short pixelY = (short)(y >> 3);
        int dx = unchecked((short)(pixelX - centerX));
        int dy = unchecked((short)(pixelY - centerY));
        short ax = unchecked((short)Math.Abs(dx));
        short ay = unchecked((short)Math.Abs(dy));
        if (ax >= 128)
        {
            ax = 128;
        }
        if (ay >= 128)
        {
            ay = 128;
        }
        extent = Math.Max(extent, Math.Max(ax, ay));
        uint distanceAddress = unchecked((uint)(0x12F5978 + (ay << 7) + ax));
        int distance = ReadExact(readResource, distanceAddress, 1)[0];
        if (distance != 0 && distance < radius)
        {
            int index = (distance << 7) / radius;
            uint factorAddress = unchecked((uint)(0x12F3978 + (intensity << 8) + index * 2));
            int factor = BinaryPrimitives.ReadUInt16LittleEndian(ReadExact(readResource, factorAddress, 2));
            pixelX = unchecked((short)((unchecked(dx * factor) >> 12) + centerX));
            pixelY = unchecked((short)((unchecked(dy * factor) >> 12) + centerY));
            x = unchecked((short)(pixelX * 8));
            y = unchecked((short)(pixelY * 8));
        }
        uint clip = 0;
        if (pixelX < 0)
        {
            clip |= 1;
        }
        else if (pixelX >= 320)
        {
            clip |= 2;
        }
        if (pixelY < 0)
        {
            clip |= 4;
        }
        else if (pixelY >= 216)
        {
            clip |= 8;
        }
        return new(unchecked((ushort)x) | unchecked((uint)y << 16), projected.Depth | clip << 16);
    }

    public static NativeBattleGfGeometryBatch Emit6BCB50(ImmutableArray<NativeBattleRawPolygon> polygons, IReadOnlyList<NativeBattleProjectedVertex> vertices, uint color, NativeBattleModelTextureBinding? binding, int tableBaseBucket = 17, byte depthShift = 0, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (polygons.IsDefault || depthShift > 31)
        {
            throw new InvalidDataException("Native6BCB50 requires original plain polygon groups and a bounded depth shift.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            NativeBattleRawPolygon polygon = polygons[polygonIndex];
            int length = polygon.Quad ? 20 : 16;
            if (polygon.Bytes.Length != length)
            {
                //6D1AD0 copies all six group counts, but6BCB50 consumes only the first plain triangle/quad groups.
                continue;
            }
            ReadOnlySpan<byte> source = (binding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, binding)).AsSpan();
            NativeBattleProjectedVertex[] corners = new NativeBattleProjectedVertex[polygon.Quad ? 4 : 3];
            uint any = 0;
            uint shared = uint.MaxValue;
            for (int corner = 0; corner < corners.Length; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(source[(corner * 2)..]) & 0xFFF;
                if ((uint)index >= vertices.Count)
                {
                    throw new InvalidDataException("Native6BCB50 polygon exceeds its actual projected vertex bank.");
                }
                corners[corner] = vertices[index];
                uint clip = vertices[index].DepthAndClip >> 16 & 255;
                any |= clip;
                shared &= clip;
            }
            if (any >= 16 || shared != 0 || NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) < 0)
            {
                continue;
            }
            PacketWrites writes = new(polygon.Quad ? 40 : 32, cursor);
            writes.Dword(0, polygon.Quad ? 0x9000000u : 0x7000000u);
            uint pageUv = BinaryPrimitives.ReadUInt32LittleEndian(source[12..]);
            uint command = color | (polygon.Quad ? 0x2C000000u : 0x24000000u) | ((pageUv & 0x2000000) != 0 ? 0x2000000u : 0);
            writes.Dword(4, command);
            for (int corner = 0; corner < corners.Length; corner++)
            {
                writes.Dword(8 + corner * 8, corners[corner].Screen);
            }
            writes.Dword(12, BinaryPrimitives.ReadUInt32LittleEndian(source[8..]));
            writes.Dword(20, pageUv & 0xFDFFFFFF);
            writes.Word(28, BinaryPrimitives.ReadUInt16LittleEndian(source[(polygon.Quad ? 16 : 6)..]));
            if (polygon.Quad)
            {
                writes.Word(36, BinaryPrimitives.ReadUInt16LittleEndian(source[18..]));
            }
            int bucket = checked(tableBaseBucket + (Average(corners.Select(vertex => vertex.Depth), polygon.Quad) >> depthShift));
            NativeBattleGfGeometryAttempt attempt = writes.Finish(bucket, Payload(corners.Select(vertex => vertex.Depth), polygon.Quad));
            if (pilotIdentity is { } identity && attempt.Primitive is { } primitive)
            {
                System.Numerics.Vector3 Screen(int corner) => new(unchecked((short)corners[corner].Screen), unchecked((short)(corners[corner].Screen >> 16)), corners[corner].Depth);
                attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + polygonIndex) }, Screen(0), Screen(1), Screen(2), polygon.Quad ? Screen(3) : default, checked((byte)corners.Length)) { ScreenSpace = true, SourceDepths = new(corners[0].Depth, corners[1].Depth, corners[2].Depth, polygon.Quad ? corners[3].Depth : 0), UseSourceDepths = true } } };
            }
            attempts.Add(attempt);
            cursor = checked(cursor + writes.Length);
        }
        return new(attempts.ToImmutable(), cursor);
    }

    public static NativeBattleGfGeometryBatch EmitBatch6D23D0(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, Func<int, NativeBattleVector>? readSelectedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.IsDefault || context.DepthShift > 31 || selectedVertices.IsDefault && readSelectedVertex is null)
        {
            throw new InvalidDataException("Native6D23D0 requires its actual geometry, selected vertex pointer and depth shift.");
        }
        matrix.Validate();
        int position = checked(geometryOffset + BinaryPrimitives.ReadInt32LittleEndian(Read(source, geometryOffset, 4)) + 16);
        int cursor = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 2; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source, position, 4));
            position = checked(position + 4);
            for (int polygon = 0; polygon < count; polygon++)
            {
                bool quad = group == 1;
                ReadOnlySpan<byte> face = Read(source, position, quad ? 24 : 20);
                NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[vertices.Length] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    vertices[corner] = Project(BinaryPrimitives.ReadUInt16LittleEndian(face[(4 + corner * 2)..]) * 4, world, corner);
                }
                PacketWrites writes = new(quad ? 36 : 28, cursor);
                writes.Dword(0, quad ? 0x8000000u : 0x6000000u);
                writes.Dword(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
                bool admitted = ((vertices[0].Flags | vertices[1].Flags | vertices[2].Flags) & 0x60000) == 0;
                if (admitted)
                {
                    admitted = NativeBattleModulePackets.Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) >= 0 || (context.Flags & 0x20) != 0;
                }
                if (admitted)
                {
                    if (quad)
                    {
                        vertices[3] = Project(BinaryPrimitives.ReadUInt16LittleEndian(face[10..]) * 4, world, 3);
                    }
                    for (int corner = 0; corner < vertices.Length; corner++)
                    {
                        writes.Dword(8 + corner * 8, vertices[corner].Screen);
                    }
                    admitted = !vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) && !vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0);
                }
                int depth = 0;
                if (admitted)
                {
                    writes.Dword(12, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
                    writes.Dword(20, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
                    if (quad)
                    {
                        writes.Dword(28, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]));
                    }
                    //These leaves reuse contextDWORD0C as an OT bias instead of the regular mesh color-cue factor.
                    depth = unchecked(Average(vertices.Select(vertex => vertex.Depth), quad) + context.DepthCue);
                    admitted = depth >= 0;
                }
                if (admitted)
                {
                    int bucket = checked(context.TableBaseBucket + (depth >> context.DepthShift));
                    //The physical polygon precedes both mode commands; native OT insertion is mode220,polygon,mode240.
                    PacketWrites before = Mode(cursor + writes.Length, 0xE1000220);
                    PacketWrites after = Mode(cursor + writes.Length + 8, 0xE1000240);
                    attempts.Add(before.Finish(bucket, [0, 0, 0, 0]));
                    NativeBattleGfGeometryAttempt attempt = writes.Finish(bucket, Payload(vertices.Select(vertex => vertex.Depth), quad));
                    if (pilotIdentity is { } identity && world is not null && attempt.Primitive is { } primitive)
                    {
                        attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + position) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) } };
                    }
                    attempts.Add(attempt);
                    attempts.Add(after.Finish(bucket, [0, 0, 0, 0]));
                    cursor = checked(cursor + writes.Length + 16);
                }
                else
                {
                    attempts.Add(writes.Finish(null, []));
                }
                position = checked(position + (quad ? 24 : 20));
            }
        }
        return new(attempts.ToImmutable(), cursor);

        NativeBattleModuleProjectedVertex Project(int offset, System.Numerics.Vector3[]? world, int corner)
        {
            NativeBattleVector local;
            if (readSelectedVertex is not null)
            {
                local = readSelectedVertex(offset);
            }
            else
            {
                ReadOnlySpan<byte> xyz = Read(selectedVertices, offset, 6);
                local = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
            }
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, local, projection);
            if (world is not null)
            {
                world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, local), projection);
            }
            return projected;
        }
    }

    private static PacketWrites Mode(int cursor, uint command)
    {
        PacketWrites writes = new(8, cursor);
        writes.Dword(0, 0x1000000);
        writes.Dword(4, command);
        return writes;
    }

    private static int Average(IEnumerable<ushort> depths, bool quad) => (int)(unchecked((uint)depths.Sum(depth => (int)depth) * (quad ? 0x100u : 0x155u)) >> 12);

    private static ImmutableArray<int> Payload(IEnumerable<ushort> depths, bool quad)
    {
        ImmutableArray<int> result = depths.Select(depth => (int)depth).ToImmutableArray();
        return quad ? result : result.Add(0);
    }

    private static ReadOnlySpan<byte> ReadExact(Func<uint, int, ImmutableArray<byte>> read, uint address, int length)
    {
        ImmutableArray<byte> result = read(address, length);
        if (result.IsDefault || result.Length != length)
        {
            throw new InvalidDataException("Native effect107 resource reader did not preserve its exact load width.");
        }
        return result.AsSpan();
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native effect107 geometry access leaves its actual source bytes.");
        }
        return source.AsSpan().Slice(offset, length);
    }

    private sealed class PacketWrites(int length, int offset)
    {
        private readonly byte[] bytes = new byte[length];
        private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
        public int Length => length;

        public void Dword(int position, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position), value);
            mask.AsSpan(position, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        public void Word(int position, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(position), value);
            mask.AsSpan(position, 2).Fill(NativeBattleGfPacketByteState.Known);
        }

        public NativeBattleGfGeometryAttempt Finish(int? bucket, ImmutableArray<int> payload)
        {
            ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive? primitive = bucket is int value ? new NativeBattleGeometryPrimitive(value, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() } : null;
            return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        }
    }
}

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, short Depth) DrawNativeEffect107Model(NativeBattleGraphicsState graphics, int model, short screenX, short screenY, short priorDepth, int intensity, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGraphicsModel target = graphics.Models[model];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, model);
        NativeBattleEffect107Resources resources = graphics.OriginalEffect?.Binding.Effect107Resources ?? throw new NotSupportedException("Native6D19A0 requires its original effect107 lookup tables.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6D19A0 requires its prior camera registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6D19A0 requires its prior camera matrix.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, target, projection, 4112, false);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native6D19A0 requires its actual body skeleton.");
        target = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(target, source, view);
        graphics = graphics with { Models = graphics.Models.SetItem(model, target) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(target, source);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native6D19A0 requires its actual body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native6D19A0 requires its observed linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native6D19A0 requires its actual linked skeleton ownership."));
        uint objectMask = target.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native6D19A0 requires its actual model object mask.");
        uint color = target.Color40 ?? throw new NotSupportedException("Native6D19A0 requires its model color DWORD40.");
        NativeBattleModelTextureBinding bodyBinding = target.BodyTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its body texture relocation.");
        short extent = 0;
        for (int index = 0; index < geometry.Length; index++)
        {
            if ((objectMask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = shared && index == 1 ? linked.Single() : geometry[index];
            NativeBattleModelTextureBinding binding = shared && index == 1 ? target.LinkedTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its linked texture relocation.") : bodyBinding;
            graphics = Submit(graphics, mesh, matrices, resource.GeometryRelocated507550 ? null : binding, index);
        }
        if (!shared && !linked.IsEmpty)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(target, resource.LinkedPoseSource ?? throw new NotSupportedException("Native6D19A0 requires its independent linked skeleton."), view);
            for (int index = 0; index < linked.Length; index++)
            {
                graphics = Submit(graphics, linked[index], linkedMatrices, target.LinkedTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its linked texture relocation."), 0x10000 + index);
            }
        }
        graphics = graphics with { Models = graphics.Models.SetItem(model, NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[model], source)) };
        return (graphics, extent);

        NativeBattleGraphicsState Submit(NativeBattleGraphicsState current, NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleModelTextureBinding? binding, int meshIndex)
        {
            mesh.Validate();
            List<NativeBattleProjectedVertex> projected = [];
            foreach (NativeBattleRawVertex vertex in mesh.Vertices)
            {
                if (vertex.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Native6D1AD0 geometry group exceeds its actual posed skeleton.");
                }
                NativeBattleModuleProjectedVertex point = NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection);
                projected.Add(NativeBattleEffect107Geometry.Deform6D1C30(point, screenX, screenY, priorDepth, ref extent, intensity, resources.Read));
            }
            NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, color, binding, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + meshIndex) } : null);
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6D19A0 requires its retained packet memory.");
            NativeBattlePrimitiveFrame frame = current.PrimitiveFrame ?? throw new NotSupportedException("Native6D19A0 requires its initialized OT.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native6D19A0 requires its actual common packet cursor."), frame.Pending, batch);
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
            return current with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        }
    }
}
