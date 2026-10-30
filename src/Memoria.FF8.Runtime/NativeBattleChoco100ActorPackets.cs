using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100Wave(short Height, ushort Radius, ushort Flatten, ushort Frequency, ushort Angle);
public sealed record NativeBattleChoco100ActorDraw(NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleChoco100ActorPackets
{
    public static NativeBattleVector DeformVertex(NativeBattleVector vertex, NativeBattleChoco100Wave wave)
    {
        short amplitude = unchecked((short)(unchecked(wave.Height * unchecked((short)wave.Radius)) / 4096));
        short flatZ = unchecked((short)(vertex.Z - unchecked(vertex.Z * unchecked((short)wave.Flatten)) / 4096));
        int angle = unchecked((ushort)(unchecked(wave.Frequency * vertex.Y) + wave.Angle)) & 4095;
        short displacement = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) / 32768));
        return new(vertex.X, vertex.Y, unchecked((short)(flatZ + displacement)));
    }

    public static NativeBattleChoco100ActorDraw Emit716DD0(NativeBattleRawMesh mesh, IReadOnlyList<NativeBattleActionMatrix> bodyBones, NativeBattleActionMatrix contextCamera, NativeBattleCameraProjection projection, uint actorColor, NativeBattleChoco100Wave wave, NativeBattleEffectArena arena, int workArenaOffset, NativeBattleModelTextureBinding? textureBinding = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        mesh.Validate();
        arena.Validate();
        contextCamera.Validate();
        if (bodyBones.Count == 0 || workArenaOffset < 0 || workArenaOffset > NativeBattleEffectArena.Length - 0xA000 || mesh.Vertices.Length > 0xA000 / 16)
        {
            throw new NotSupportedException("Native deformed actor requires its posed body bones and actual A000-byte TIM work owner.");
        }
        NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[mesh.Vertices.Length];
        NativeBattleVector[]? reached = pilotIdentity.HasValue ? new NativeBattleVector[mesh.Vertices.Length] : null;
        byte[] work = new byte[checked(mesh.Vertices.Length * 16)];
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleVector transformed = NativeBattleCureActorPackets.TransformBone4607E0(mesh.Vertices[index], bodyBones);
            NativeBattleVector deformed = DeformVertex(transformed, wave);
            if (reached is not null)
            {
                reached[index] = deformed;
            }
            int offset = index * 16;
            BinaryPrimitives.WriteInt16LittleEndian(work.AsSpan(offset), deformed.X);
            BinaryPrimitives.WriteInt16LittleEndian(work.AsSpan(offset + 2), deformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(work.AsSpan(offset + 4), deformed.Z);
            projected[index] = NativeBattleModuleProjection.Project45FE10(contextCamera, deformed, projection);
            BinaryPrimitives.WriteUInt32LittleEndian(work.AsSpan(offset + 8), projected[index].Screen);
            BinaryPrimitives.WriteUInt32LittleEndian(work.AsSpan(offset + 12), projected[index].Depth);
        }
        if (work.Length != 0)
        {
            arena = arena.Write(workArenaOffset, work);
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                arena.MarkUnknown(workArenaOffset + index * 16 + 6, 2);
            }
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        int polygonIdentity = -1;
        //Only the original plain FT3 and FT4 directories are visited, in that order.
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons.Where(polygon => !polygon.Quad && polygon.Bytes.Length == 16).Concat(mesh.Polygons.Where(polygon => polygon.Quad && polygon.Bytes.Length == 20)))
        {
            polygonIdentity++;
            ImmutableArray<byte> source = textureBinding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding);
            int count = polygon.Quad ? 4 : 3;
            NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(corner * 2)) & 0xFFF;
                corners[corner] = (uint)index < projected.Length ? projected[index] : throw new InvalidDataException("Native deformed actor polygon references an absent work vertex.");
            }
            if (NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) <= 0)
            {
                continue;
            }
            int length = polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocation);
            packet.Word(0, polygon.Quad ? 0x09000000u : 0x07000000u);
            uint color = (polygon.Quad ? 0x2C000000u : 0x24000000u) | (actorColor & 0xFFFFFF);
            if ((source[15] & 2) != 0)
            {
                color |= 0x02000000;
            }
            packet.Word(4, color);
            packet.Word(8, corners[0].Screen);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(8)));
            packet.Word(16, corners[1].Screen);
            packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(12)));
            packet.Word(24, corners[2].Screen);
            packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(polygon.Quad ? 16 : 6)));
            if (polygon.Quad)
            {
                packet.Word(32, corners[3].Screen);
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(18)));
            }
            int depthSum = unchecked(corners.Sum(vertex => (int)vertex.Depth));
            int bucket = 17 + (polygon.Quad ? depthSum >> 6 : (depthSum / 3) >> 4);
            ImmutableArray<int> depths = corners.Select(vertex => (int)vertex.Depth).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), polygon.Quad ? depths : depths.Add(0));
            if (pilotIdentity is { } identity)
            {
                System.Numerics.Vector3 World(int corner)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(corner * 2)) & 0xFFF;
                    return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(contextCamera, reached![vertex]), projection);
                }
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + polygonIdentity) }, World(0), World(1), World(2), polygon.Quad ? World(3) : default, checked((byte)count)) { SourceDepths = new(depths[0], depths[1], depths[2], polygon.Quad ? depths[3] : 0), UseSourceDepths = true } };
            }
            attempts.Add(packet.Attempt(primitive));
            allocation = checked(allocation + length);
        }
        return new(arena, new(attempts.ToImmutable(), allocation));
    }
}
