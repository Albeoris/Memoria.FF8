using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38DebrisPackets
{
    public static NativeBattleGfGeometryBatch Emit8DA960(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, short zsf3, short zsf4)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = Read(28), Y = Read(30), Z = Read(32) };
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, Read(70));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, Read(68));
        matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Quake debris requires its current camera matrix."), matrix);
        NativeBattleCameraProjection local = projection with { View = matrix };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        Face([0, 1, 2], 164);
        Face([5, 4, 3], 168);
        Side([1, 0, 4, 3]);
        Side([2, 1, 5, 4]);
        Side([0, 2, 3, 5]);
        return new(attempts.ToImmutable(), allocated);

        short Read(int offset) => unchecked((short)task.ReadWord(offset));
        NativeBattleVector Position(int selector) => new(Read(92 + selector * 8), Read(94 + selector * 8), Read(96 + selector * 8));
        void Face(int[] selectors, int color)
        {
            Fields fields = new(32);
            fields.Dword(0, 0x07000000);
            fields.Dword(4, task.ReadDword(color));
            for (int index = 0; index < 3; index++)
            {
                fields.Byte(12 + index * 8, task.ReadByte(148 + selectors[index]));
                fields.Byte(13 + index * 8, task.ReadByte(154 + selectors[index]));
            }
            fields.Word(14, task.ReadWord(162));
            fields.Word(22, task.ReadWord(160));
            Append(NativeBattleQuake38TrianglePackets.Emit(selectors.Select(Position).ToImmutableArray(), fields.Data.ToImmutableArray(), fields.Mask.ToImmutableArray(), local, zsf3, false));
        }
        void Side(int[] selectors)
        {
            Fields fields = new(40);
            fields.Dword(0, 0x09000000);
            fields.Dword(4, task.ReadDword(172));
            for (int index = 0; index < 4; index++)
            {
                int source = selectors[index % 2];
                fields.Byte(12 + index * 8, task.ReadByte(148 + source));
                fields.Byte(13 + index * 8, task.ReadByte(154 + source));
            }
            fields.Word(14, task.ReadWord(162));
            fields.Word(22, task.ReadWord(160));
            Append(NativeBattleQuake38QuadPackets.Emit(selectors.Select(Position).ToImmutableArray(), fields.Data.ToImmutableArray(), fields.Mask.ToImmutableArray(), local, zsf4, false));
        }
        void Append(NativeBattleGfGeometryBatch batch)
        {
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) }));
            allocated = checked(allocated + batch.AllocationBytes);
        }
    }

    private sealed class Fields(int length)
    {
        public byte[] Data { get; } = new byte[length];
        public NativeBattleGfPacketByteState[] Mask { get; } = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, length).ToArray();
        public void Byte(int offset, byte value)
        {
            Data[offset] = value;
            Mask[offset] = NativeBattleGfPacketByteState.Known;
        }
        public void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(offset), value);
            Array.Fill(Mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }
        public void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(Data.AsSpan(offset), value);
            Array.Fill(Mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
