using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22ActorDraw(NativeBattleEffectArena Work, NativeBattleGfGeometryBatch Packets);

/// <summary>Bio's deformed plain actor faces over actual posed bones and retained vertex-work bytes.</summary>
public static class NativeBattleBio22ActorPackets
{
    public static NativeBattleVector DeformVertex(NativeBattleVector vertex, short phase, short amplitude)
    {
        int angle = unchecked(phase + 12 * vertex.Y) & 4095;
        int dx = unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) / 0x40000;
        int dz = unchecked(NativeBattleCameraMath.Cosine(angle) * amplitude) / 0x40000;
        return new(unchecked((short)(vertex.X + dx)), vertex.Y, unchecked((short)(vertex.Z + dz)));
    }

    public static NativeBattleBio22ActorDraw Emit89D780(NativeBattleRawMesh mesh, IReadOnlyList<NativeBattleActionMatrix> bodyBones, NativeBattleActionMatrix mainCamera, NativeBattleCameraProjection projection, uint color40, short phase, short amplitude, short zsf3, short zsf4, NativeBattleEffectArena currentWork, int workOffset, NativeBattleModelTextureBinding? textureBinding = null)
    {
        mesh.Validate();
        mainCamera.Validate();
        currentWork.Validate();
        if (workOffset < 0 || (long)workOffset + (long)mesh.Vertices.Length * 16 > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native Bio actor vertex work exceeds its supplied current owner.");
        }
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleVector posed = NativeBattleCureActorPackets.TransformBone4607E0(mesh.Vertices[index], bodyBones);
            NativeBattleVector vertex = DeformVertex(posed, phase, amplitude);
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, vertex.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertex.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertex.Z);
            currentWork = currentWork.Write(checked(workOffset + index * 16 + 4), xyz);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons.Where(face => !face.Quad && face.Bytes.Length == 16).Concat(mesh.Polygons.Where(face => face.Quad && face.Bytes.Length == 20)))
        {
            int count = polygon.Quad ? 4 : 3;
            NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[count];
            for (int corner = 0; corner < 3; corner++)
            {
                corners[corner] = NativeBattleModuleProjection.Project45FE10(mainCamera, ReadVertex(corner), projection);
            }
            if (NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) <= 0)
            {
                continue;
            }
            if (polygon.Quad)
            {
                // The native fourth projection consumes only Screen/Depth, shared with45FAA0; its extra flags and cue outputs are unused.
                corners[3] = NativeBattleModuleProjection.Project45FE10(mainCamera, ReadVertex(3), projection);
            }
            ImmutableArray<byte> face = textureBinding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding);
            int length = polygon.Quad ? 40 : 32;
            SkinWrites packet = new(length, allocation);
            packet.Word(0, polygon.Quad ? 0x09000000u : 0x07000000u);
            uint color = (color40 & 0xFFFFFF) | (polygon.Quad ? 0x2C000000u : 0x24000000u);
            if ((face[15] & 2) != 0)
            {
                color |= 0x02000000;
            }
            packet.Word(4, color);
            packet.Word(8, corners[0].Screen);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan().Slice(8)));
            packet.Word(16, corners[1].Screen);
            packet.Word(20, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan().Slice(12)));
            packet.Word(24, corners[2].Screen);
            packet.Word16(28, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(polygon.Quad ? 16 : 6)));
            if (polygon.Quad)
            {
                packet.Word(32, corners[3].Screen);
                packet.Word16(36, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(18)));
            }
            int sum = unchecked(corners.Sum(vertex => (int)vertex.Depth));
            uint depth = unchecked((uint)(sum * (polygon.Quad ? zsf4 : zsf3))) >> 12;
            int bucket = checked(17 + (int)(depth >> 4));
            ImmutableArray<int> payload = corners.Select(vertex => (int)vertex.Depth).ToImmutableArray();
            // The fourth triangle payload is unconsumed canonical padding.
            attempts.Add(packet.Attempt(new(bucket, packet.Words(), polygon.Quad ? payload : payload.Add(0))));
            allocation = checked(allocation + length);

            NativeBattleVector ReadVertex(int corner)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & 0xFFF;
                if ((uint)index >= mesh.Vertices.Length)
                {
                    throw new InvalidDataException("Native Bio actor face references an absent current work vertex.");
                }
                byte[] xyz = currentWork.Read(checked(workOffset + index * 16 + 4), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            }
        }
        return new(currentWork, new(attempts.ToImmutable(), allocation));
    }
}
