using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The ordinary Bio GT3 producer over the current shared mesh and actual task transform.</summary>
public static class NativeBattleBio22GeometryPackets
{
    public static NativeBattleGfGeometryBatch Emit89AB90(NativeBattleModulePoolTask task, ImmutableArray<byte> currentMesh, NativeBattleCameraProjection projection, short zsf3)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleActionMatrix world = World(task);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Bio geometry requires its actual current camera view."), world);
        return Emit(currentMesh, point => NativeBattleModuleProjection.Project45FE10(matrix, point, projection), zsf3);
    }

    public static NativeBattleGfGeometryBatch Emit89AD30(NativeBattleModulePoolTask task, ImmutableArray<byte> currentMesh, NativeBattleCameraProjection projection, short zsf3)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleActionMatrix world = World(task);
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native Bio deformation requires its actual current camera view.");
        return Emit(currentMesh, point => NativeBattleModuleProjection.Project45FE10(camera, DeformedWorld(task, world, point), projection), zsf3);
    }

    public static NativeBattleVector Transform89AD30(NativeBattleModulePoolTask task, NativeBattleVector local) => DeformedWorld(task, World(task), local);

    private static NativeBattleActionMatrix World(NativeBattleModulePoolTask task)
    {
        NativeBattleVector angles = new(unchecked((short)task.ReadWord(0x44)), unchecked((short)task.ReadWord(0x46)), unchecked((short)task.ReadWord(0x48)));
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(angles) with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        return NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
    }

    private static NativeBattleVector DeformedWorld(NativeBattleModulePoolTask task, NativeBattleActionMatrix world, NativeBattleVector local)
    {
        NativeBattleVector point = NativeBattleCureActorPackets.TransformBone4607E0(new(local, 0), [world]);
        if (point.Y <= -0x100)
        {
            return point;
        }
        int y = Math.Min((int)point.Y, 0x200);
        int factor = y + 0x100;
        int x = unchecked(point.X + unchecked((point.X - unchecked((short)task.ReadWord(0x1C))) * factor) / 0x200);
        int z = unchecked(point.Z + unchecked((point.Z - unchecked((short)task.ReadWord(0x20))) * factor) / 0x200);
        return new(unchecked((short)x), (short)Math.Min(y, 0), unchecked((short)z));
    }

    private static NativeBattleGfGeometryBatch Emit(ImmutableArray<byte> currentMesh, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project, short zsf3)
    {
        int countOffset = checked(0x18 + 4 * (Dword(0) / 4));
        int count = Dword(countOffset);
        if (count <= 0)
        {
            return new([], 0);
        }
        int first = checked(countOffset + 4);
        Require(first, checked(count * 28));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            int faceOffset = checked(first + faceIndex * 28);
            ReadOnlySpan<byte> face = currentMesh.AsSpan().Slice(faceOffset, 28);
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[3];
            for (int corner = 0; corner < 3; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2)) >> 1;
                int vertex = checked(8 + index * 8);
                Require(vertex, 8);
                NativeBattleVector point = new(Word(vertex), Word(vertex + 2), Word(vertex + 4));
                projected[corner] = project(point);
            }
            if (NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
            {
                continue;
            }
            byte[] packet = new byte[40];
            DwordStore(0, 0x09000000);
            DwordStore(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
            DwordStore(8, projected[0].Screen);
            DwordStore(12, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(12)));
            DwordStore(16, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(20)));
            DwordStore(20, projected[1].Screen);
            DwordStore(24, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(16)));
            DwordStore(28, BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(24)));
            DwordStore(32, projected[2].Screen);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(36), BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(10)));
            uint sum = unchecked((uint)projected[0].Depth + projected[1].Depth + projected[2].Depth);
            uint depth = unchecked(sum * (uint)(int)zsf3) >> 12;
            int bucket = unchecked(17 + (unchecked((int)depth) >> 2));
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 40).Select(offset => offset < 38 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, words, [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0]);
            attempts.Add(new(allocation, packet.ToImmutableArray(), mask, primitive));
            allocation = checked(allocation + 40);

            void DwordStore(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
        }
        return new(attempts.ToImmutable(), allocation);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(currentMesh.AsSpan().Slice(offset, 2));
        int Dword(int offset)
        {
            Require(offset, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(currentMesh.AsSpan().Slice(offset, 4));
        }
        void Require(int offset, int length)
        {
            if (currentMesh.IsDefault || offset < 0 || length < 0 || offset > currentMesh.Length - length)
            {
                throw new InvalidDataException("Native Bio geometry read leaves its current mesh owner.");
            }
        }
    }
}
