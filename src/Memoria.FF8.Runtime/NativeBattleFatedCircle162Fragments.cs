using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5FBD00(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (Word(task, 26) > 0)
        {
            return Delay(frame, pool, slot, inputs.GraphicsFlags, task, 26);
        }
        int fragment = Word(task, 14);
        if (fragment is < 0 or >= 8)
        {
            throw new InvalidDataException("Native5FBD00 requires an original fragment directory index.");
        }
        uint mesh = unchecked((uint)ReadDword(frame.Staging, 0xDA8C74u + (uint)fragment * 4));
        short pivotX = ReadWord(frame.Staging, 0xDA8C94u + (uint)fragment * 4);
        short pivotZ = ReadWord(frame.Staging, 0xDA8CB4u + (uint)fragment * 4);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        NativeBattleActionMatrix parent = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleGfGeometryBatch packets = NativeBattleFatedCircle162Packets.Emit5FBEB0(frame.Staging, mesh, parent, new(Word(task, 28), Word(task, 32), unchecked((short)(2 * Word(task, 12)))), new(pivotX, 0, pivotZ), projection, colors);
        frame = inputs.Execute(frame, new NativeBattleFatedCircle162Geometry(packets));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short vertical = Word(task, 22);
        short forward = Word(task, 24);
        short firstSpeed = Word(task, 30);
        short secondSpeed = Word(task, 34);
        task = task.WriteWord(18, unchecked((ushort)(Word(task, 18) + vertical))).WriteWord(20, unchecked((ushort)(Word(task, 20) + forward))).WriteWord(22, unchecked((ushort)(vertical - vertical / 4)));
        if (forward < -150)
        {
            task = task.WriteWord(24, unchecked((ushort)(forward - (forward >> 5))));
        }
        task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + firstSpeed))).WriteWord(30, unchecked((ushort)(firstSpeed + (firstSpeed >> 4)))).WriteWord(32, unchecked((ushort)(Word(task, 32) + secondSpeed))).WriteWord(34, unchecked((ushort)(secondSpeed + (secondSpeed >> 4)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return Finish(frame, pool, slot, task, 30);
    }
}

public static partial class NativeBattleFatedCircle162Packets
{
    public static NativeBattleGfGeometryBatch Emit5FBEB0(NativeBattleFileStaging staging, uint mesh, NativeBattleActionMatrix parent, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(angles) with { X = pivot.X, Y = pivot.Y, Z = pivot.Z };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(parent, local);
        uint cursor = checked(mesh + BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(mesh, 4)) + 8);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Group(2, 20);
        cursor += 12;
        Group(6, 28);
        return new(attempts.ToImmutable(), allocated);

        void Group(int group, int stride)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(staging.Read(cursor, 4));
            cursor += 4;
            for (int index = 0; index < count; index++)
            {
                byte[] face = staging.Read(cursor, stride);
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    uint vertex = checked(mesh + 8u + 4u * BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)));
                    byte[] source = staging.Read(vertex, 6);
                    NativeBattleVector point = new(unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(source) - pivot.X)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(2)), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(4)) - pivot.Z)));
                    corners[corner] = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
                }
                NativeBattleGfGeometryAttempt attempt = NativeBattleModulePackets.EmitFace572200(face, corners, group, new(0, null, null, null, null, null, 17, 2), colors, allocated);
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocated += attempt.PacketLength;
                }
                cursor += checked((uint)stride);
            }
        }
    }
}
