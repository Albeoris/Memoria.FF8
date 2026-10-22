using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleElectrocute275BoneProjection
{
    public static NativeBattleEffectMemory ProjectChain(NativeBattleEffectMemory memory, uint firstRecord, uint recordBankAddress, int recordBankLength, Func<int, NativeBattlePackedMatrix32> readBone, Func<int, int, byte[]> readGeometry, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, short? localVectorW = null)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Electrocute bone projection requires its actual record arena.");
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Electrocute bone projection requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Electrocute bone projection requires its current arithmetic registers."));
        HashSet<uint> visited = [];
        uint record = firstRecord;
        while (record != 0)
        {
            if (record < recordBankAddress || recordBankLength < 40 || (ulong)record - recordBankAddress > (ulong)(recordBankLength - 40) || (record - recordBankAddress) % 40 != 0 || !visited.Add(record))
            {
                throw new InvalidDataException("Electrocute bone chain leaves its actual linked record owner.");
            }
            int at = checked((int)(record - NativeBattleEffectNodeMemory.ArenaAddress));
            int bone = unchecked((sbyte)arena.Read(at + 15, 1)[0]);
            if (bone < 0)
            {
                throw new InvalidDataException("Electrocute bone chain has an invalid signed bone selector.");
            }
            NativeBattlePackedMatrix32 matrix = readBone(bone);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, Padding(matrix)) };
            int source = checked(BinaryPrimitives.ReadInt16LittleEndian(arena.Read(at, 2)) * 2);
            byte[] xyz = readGeometry(source, 6);
            if (xyz is null || xyz.Length != 6)
            {
                throw new InvalidDataException("Electrocute bone chain requires its exact current source XYZ bytes.");
            }
            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(xyz), 0);
            registers = NativeBattleGeometry.Transform(registers);
            short[] point = new short[3];
            for (int axis = 0; axis < 3; axis++)
            {
                int transformed = registers.Colors.Ir[axis] ?? throw new NotSupportedException("Electrocute bone chain requires its reached transformed IR values.");
                short offset = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(at + 4 + axis * 2, 2));
                point[axis] = unchecked((short)(transformed + offset));
            }
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(point[0], point[1], point[2]), localVectorW);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Electrocute bone chain requires its reached screen FIFO.");
            uint depth = registers.Geometry.Sz[3] ?? throw new NotSupportedException("Electrocute bone chain requires its reached depth FIFO.");
            arena = arena.Write(at + 0x10, BitConverter.GetBytes(screen)).Write(at + 0x14, BitConverter.GetBytes(unchecked((uint)(unchecked((int)depth) >> 2))));
            record = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at + 0x24, 4));
        }
        return memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }

    private static NativeBattleVector Vector(byte[] xyz) => new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
}
