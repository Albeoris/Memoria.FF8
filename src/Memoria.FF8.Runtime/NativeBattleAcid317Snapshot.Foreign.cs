namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid317Snapshot
{
    public static NativeBattleAcid317SnapshotStep CaptureForeign(NativeBattleEffectMemory memory, NativeCrtRandomState random, NativeBattleEffectArena temporary12, NativeBattleForeignModelGeometry geometry, uint cacheAddress, int cacheLength)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Host body snapshot requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Host body snapshot requires its current arithmetic registers."));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Host body snapshot requires its actual coefficient arena.");
        uint cache = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(temporary12.Read(8, 4));
        NativeBattleForeignModelDrawing.RequireCoefficientCache(geometry, cache, cacheAddress, cacheLength);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, geometry.World, true, true, null) };
        foreach (NativeBattleVector vertex in geometry.Vertices)
        {
            if (cache < cacheAddress || cacheLength < 0 || (ulong)cache - cacheAddress >= (ulong)cacheLength)
            {
                throw new InvalidDataException("Host body snapshot leaves its bounded coefficient cache.");
            }
            registers = NativeBattleGeometry.LoadVector(registers, 0, vertex, 0);
            registers = NativeBattleGeometry.Transform(registers);
            int y = registers.Colors.Ir[1] ?? throw new NotSupportedException("Host body snapshot requires its transformed IR2.");
            random = random.Draw(out ushort value);
            int scaled = unchecked(((value & 31) + 16) * y) >> 10;
            arena = arena.Write(checked((int)(cache - NativeBattleEffectNodeMemory.ArenaAddress)), [unchecked((byte)-scaled)]);
            cache = checked(cache + 1);
            temporary12 = temporary12.Write(8, BitConverter.GetBytes(cache));
        }
        return new(memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, random, temporary12);
    }
}
