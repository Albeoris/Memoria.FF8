using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278RangeDraw(NativeBattleGfGeometryBatch Packets, bool FarColorWritten);

/// <summary>The recipient's signed source-Z band and three ordered textured face groups.</summary>
public static class NativeBattleCarbuncle278RangeMesh
{
    public static NativeBattleCarbuncle278RangeDraw Emit682760(NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short minimum, short maximum, int fade, ushort vOffset, NativeBattleSummonColors colors)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        bool far = false;
        int cursor = checked(Int(0) + 12);
        Group(3, 24);
        cursor = checked(cursor + 8);
        Group(6, 28);
        Group(7, 36);
        return new(new(attempts.ToImmutable(), allocated), far);

        void Group(int group, int stride)
        {
            int count = Int(cursor);
            cursor = checked(cursor + 4);
            for (int index = 0; index < count; index++)
            {
                byte[] face = Read(cursor, stride).ToArray();
                cursor = checked(cursor + stride);
                int corners = (group & 1) != 0 ? 4 : 3;
                NativeBattleModuleProjectedVertex[] vertices = new NativeBattleModuleProjectedVertex[corners];
                NativeBattleVector[] positions = new NativeBattleVector[corners];
                bool admitted = true;
                for (int corner = 0; corner < corners; corner++)
                {
                    int vertex = checked(8 + 4 * BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2, 2)));
                    ReadOnlySpan<byte> source = Read(vertex, 6);
                    positions[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(source), BinaryPrimitives.ReadInt16LittleEndian(source[2..]), BinaryPrimitives.ReadInt16LittleEndian(source[4..]));
                    if (positions[corner].Z < minimum || positions[corner].Z > maximum)
                    {
                        admitted = false;
                        break;
                    }
                }
                if (!admitted)
                {
                    continue;
                }
                for (int corner = 0; corner < corners; corner++)
                {
                    vertices[corner] = NativeBattleModuleProjection.Project45FE10(matrix, positions[corner], projection);
                }
                uint flags = group == 3 ? 0x10u : 0x20u;
                if (fade != 0)
                {
                    flags |= group == 3 ? 0x40u : 0x80u;
                }
                NativeBattleMeshPacketContext context = new(flags, 0, fade, null, null, 0, 0, 17, 2);
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, group, context, colors, allocated);
                if (attempt.Primitive is not { } polygon)
                {
                    attempts.Add(attempt);
                    continue;
                }
                far |= fade != 0;
                int[] uv = group == 3 ? [12, 20, 28, 36] : group == 6 ? [12, 24, 36] : [12, 24, 36, 48];
                byte[] data = attempt.Data.ToArray();
                ImmutableArray<byte> shifted = NativeBattleEffectTextureWindows.OffsetAxis(uv.Select(offset => data[offset + 1]), vOffset, 128);
                for (int corner = 0; corner < corners; corner++)
                {
                    data[uv[corner] + 1] = shifted[corner];
                }
                ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4, 4))).ToImmutableArray();
                polygon = polygon with { Words = words };
                attempt = attempt with { Data = data.ToImmutableArray(), Primitive = polygon };
                NativeBattleGfGeometryBatch bracket = NativeBattleEffectTextureWindows.Bracket(attempt, polygon, new(0, 0, 256, 256), new(0, 0, 64, 128));
                attempts.AddRange(bracket.Attempts);
                allocated = checked(allocated + bracket.AllocationBytes);
            }
        }

        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        ReadOnlySpan<byte> Read(int offset, int count)
        {
            if (mesh.Bytes.IsDefault || offset < 0 || offset > mesh.Bytes.Length - count)
            {
                throw new InvalidDataException("Native Carbuncle range mesh access leaves its original source bytes.");
            }
            return mesh.Bytes.AsSpan().Slice(offset, count);
        }
    }
}
