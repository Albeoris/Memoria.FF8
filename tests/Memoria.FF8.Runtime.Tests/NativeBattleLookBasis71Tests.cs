using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLookBasis71Tests
{
    [Fact]
    public void Look_basis_transposes_actual_saturated_vectors_and_preserves_destination_suffix_and_temporary_tail()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Ir0 = 123 } };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address + 18, new byte[] { 0xBC, 0xDE });
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        bool[] known = Enumerable.Repeat(false, 32).ToArray();
        known[20] = true;
        NativeBattlePackedMatrix32 destination = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleLookBasisStep result = NativeBattleLookBasis.Execute8DDA50(memory, staging, destination, new(-32768, 123, -9), new(0, 4096, 0));
        short[] rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(result.Matrix.Bytes.AsSpan().Slice(index * 2, 2))).ToArray();
        Assert.Equal(new short[] { -9, 0, -32768, 0, 4096, 123, 32767, 0, -9 }, rotation);
        Assert.Equal(destination.Bytes.Skip(18), result.Matrix.Bytes.Skip(18));
        Assert.Equal(destination.Known.Skip(18), result.Matrix.Known.Skip(18));
        Assert.All(result.Matrix.Known.Take(18), Assert.True);
        Assert.Equal(new int?[] { -9, 0, 32768 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(0x00400000u, result.Memory.ScriptedColorRegisters.Flags);
        Assert.Equal(123u, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(new byte[] { 0xBC, 0xDE }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 18, 2));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(NativeBattleTemporaryMemory.Address + 20, 1));
    }

    [Fact]
    public void Look_basis_requires_actual_temporary_cursor_and_geometry_owners()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattlePackedMatrix32 destination = new(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleLookBasis.Execute8DDA50(memory, NativeBattleFileStaging.Unknown(), destination, new(0, 0, 4096), new(0, 4096, 0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleLookBasis.Execute8DDA50(memory with { GeometryRegisters = null }, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), destination, new(0, 0, 4096), new(0, 4096, 0)));
    }
}
