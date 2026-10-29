using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleTornado146Pieces
{
    public static NativeBattleModulePoolTask Move(NativeBattleModulePoolTask task, int height, out bool complete)
    {
        int age = S(12);
        short y = S(18);
        if (y < height)
        {
            W(28, S(28) + S(30));
            W(32, S(32) + S(34));
        }
        int frame = age - 4;
        if (frame >= 0 && frame < 2)
        {
            W(18, S(18) + S(22));
        }
        else if (frame >= 2 && frame - 2 < 50)
        {
            if (frame == 2)
            {
                W(22, S(22) >> 1);
            }
            short velocity = S(22);
            W(18, S(18) + velocity);
            W(22, velocity + velocity / 16);
            if (S(18) < -5000)
            {
                complete = true;
                return task;
            }
            W(24, S(24) - (S(22) >> 2));
        }
        W(12, age + 1);
        complete = S(12) >= 60;
        return task;
        short S(int offset) => unchecked((short)task.ReadWord(offset));
        void W(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)value));
    }

    public static NativeBattleGfGeometryBatch Draw(NativeBattleStaticMeshResource mesh, NativeBattleModulePoolTask task, int height, short pivotX, short pivotZ, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Action<int, byte[]>? observe = null)
    {
        short S(int offset) => unchecked((short)task.ReadWord(offset));
        bool airborne = S(18) < height;
        NativeBattleActionMatrix orbit = NativeBattleGfMatrices.Euler56CD50(new(0, S(24), 0)) with { X = S(16), Y = airborne ? S(18) : 0, Z = S(20) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, orbit);
        if (airborne)
        {
            NativeBattleActionMatrix spin = NativeBattleGfMatrices.Euler56CD50(new(S(28), 0, S(32))) with { X = pivotX, Z = pivotZ };
            matrix = NativeBattleGfMatrices.Compose56C2F0(matrix, spin);
        }
        observe?.Invoke(0x1C, BitConverter.GetBytes(mesh.Address + 8));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor += 4;
            if (group is not (2 or 6) && count != 0)
            {
                throw new NotSupportedException("Native Tornado pieces require their original FT3 and GT3 lists.");
            }
            for (int polygon = 0; polygon < count; polygon++, cursor += strides[group])
            {
                ReadOnlySpan<byte> face = mesh.Bytes.AsSpan().Slice(cursor, strides[group]);
                bool gouraud = group == 6;
                int length = gouraud ? 40 : 32;
                uint[] words = new uint[length / 4];
                bool[] written = new bool[length];
                Store(0, (uint)(length / 4 - 1) << 24);
                Store(1, BinaryPrimitives.ReadUInt32LittleEndian(face));
                Store(3, BinaryPrimitives.ReadUInt32LittleEndian(face[12..]));
                Store(gouraud ? 6 : 5, BinaryPrimitives.ReadUInt32LittleEndian(face[16..]));
                Store(gouraud ? 9 : 7, BinaryPrimitives.ReadUInt32LittleEndian(face[8..]) >> 16);
                if (gouraud && !airborne)
                {
                    Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]));
                    Store(7, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]));
                }
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[3];
                NativeBattleVector[] points = new NativeBattleVector[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2));
                    int offset = 8 + vertex * 4;
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset + 4)));
                    point = airborne ? new(unchecked((short)(point.X - pivotX)), point.Y, unchecked((short)(point.Z - pivotZ))) : point with { Y = unchecked((short)(point.Y + S(18))) };
                    points[corner] = point;
                    byte[] raw = mesh.Bytes.AsSpan().Slice(offset, 8).ToArray();
                    BinaryPrimitives.WriteInt16LittleEndian(raw, point.X);
                    BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), point.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), point.Z);
                    observe?.Invoke(0x58 + corner * 20, raw);
                    projected[corner] = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
                }
                uint projectionFlags = projected.Aggregate(0u, (flags, vertex) => flags | vertex.Flags);
                observe?.Invoke(0x54, BitConverter.GetBytes(projectionFlags));
                bool accepted = (projectionFlags & 0x60000) == 0;
                bool projectedAccepted = accepted;
                if (accepted)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Store(gouraud ? 2 + corner * 3 : 2 + corner * 2, projected[corner].Screen);
                    }
                    if (airborne)
                    {
                        accepted = !projected.All(vertex => unchecked((short)vertex.Screen) < 0 || unchecked((short)vertex.Screen) > 2560) && !projected.All(vertex => unchecked((short)(vertex.Screen >> 16)) < 0 || unchecked((short)(vertex.Screen >> 16)) > 1728);
                    }
                    if (accepted && gouraud && airborne)
                    {
                        Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face[20..]));
                        Store(7, BinaryPrimitives.ReadUInt32LittleEndian(face[24..]));
                    }
                }
                uint average = unchecked((uint)projected.Sum(vertex => (int)vertex.Depth) * 0x155u) >> 12;
                if (projectedAccepted)
                {
                    observe?.Invoke(0x48, BitConverter.GetBytes(NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen)));
                    observe?.Invoke(0x50, BitConverter.GetBytes(average));
                    if (!airborne)
                    {
                        for (int corner = 0; corner < 3; corner++)
                        {
                            observe?.Invoke(0xA0 + corner * 20, BitConverter.GetBytes(Math.Clamp(150 - points[corner].Y, -32768, 32767)));
                        }
                    }
                }
                bool stage = !airborne && (points.Any(point => point.Y > 150) || points.All(point => point.Y == 150));
                int bucket = checked((stage ? 4122 : 17) + (int)(average >> (stage ? 6 : 2)));
                NativeBattleGeometryPrimitive? primitive = accepted ? new(bucket, words.ToImmutableArray(), [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0]) : null;
                if (primitive is not null)
                {
                    System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, points[corner]), projection);
                    primitive = primitive with { PilotGeometry = new(NativeBattlePilotVisualLifetimes.Identity(146, task, cursor), World(0), World(1), World(2), default, 3) };
                }
                byte[] bytes = new byte[length];
                for (int index = 0; index < words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
                }
                attempts.Add(new(allocated, bytes.ToImmutableArray(), written.Select(value => value ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray(), primitive));
                if (accepted)
                {
                    allocated += length;
                }
                void Store(int index, uint value)
                {
                    words[index] = value;
                    written.AsSpan(index * 4, 4).Fill(true);
                }
            }
            if (group is 2 or 6)
            {
                observe?.Invoke(0x20, BitConverter.GetBytes(mesh.Address + (uint)cursor));
            }
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
