using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131MathTests
{
    [Fact]
    public void Preprocess_preserves_first_two_vertices_and_scales_through_retained_group_zero_WORDs()
    {
        byte[] bytes = Mesh();
        Put(bytes, 8, 101);
        Put(bytes, 16, 102);
        Put(bytes, 24, -5);
        Put(bytes, 26, 7);
        Put(bytes, 0x530, 4);
        Put(bytes, 0x532, -4);
        ImmutableArray<byte> actual = NativeBattleBadBreath131Controller.Preprocess6B41D0(bytes.ToImmutableArray());
        Assert.Equal((short)101, Read(actual, 8));
        Assert.Equal((short)102, Read(actual, 16));
        Assert.Equal((short)-7, Read(actual, 24));
        Assert.Equal((short)8, Read(actual, 26));
        Assert.Equal((short)5, Read(actual, 0x530));
        Assert.Equal((short)-5, Read(actual, 0x532));
        Assert.Equal((short)((-10 << 12) / 1650), Read(actual, 14));
    }

    [Fact]
    public void Warm_preprocess_compounds_retained_XY_instead_of_resetting_immutable_source()
    {
        byte[] bytes = Mesh();
        Put(bytes, 24, 100);
        ImmutableArray<byte> once = NativeBattleBadBreath131Controller.Preprocess6B41D0(bytes.ToImmutableArray());
        ImmutableArray<byte> twice = NativeBattleBadBreath131Controller.Preprocess6B41D0(once);
        Assert.Equal((short)125, Read(once, 24));
        Assert.Equal((short)156, Read(twice, 24));
    }

    [Fact]
    public void Preprocess_preserves_native_zero_span_division_failure()
    {
        byte[] bytes = Mesh();
        for (int index = 0; index < 166; index++)
        {
            Put(bytes, 12 + index * 8, 0);
        }
        Assert.Throws<DivideByZeroException>(() => NativeBattleBadBreath131Controller.Preprocess6B41D0(bytes.ToImmutableArray()));
    }

    [Fact]
    public void Shared_deformation_uses_current_MVMVA_and_exact_wave_indices_preserving_output_W()
    {
        byte[] source = new byte[1328];
        for (int index = 0; index < 165; index++)
        {
            Put(source, 8 + index * 8, 3);
            Put(source, 10 + index * 8, -5);
            Put(source, 12 + index * 8, 10);
            Put(source, 14 + index * 8, -2);
        }
        short[] wave = new short[8192];
        wave[4094] = 32;
        wave[2] = 2048;
        wave[3] = -2048;
        const int destination = 0xA00;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear().MarkUnknown(destination, 1320),
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown()
        };
        NativeBattleEffectMemory actual = NativeBattleStripDeformation.Transform(memory, source.ToImmutableArray(), 5, wave.ToImmutableArray(), destination);
        Assert.Equal((short)2, BinaryPrimitives.ReadInt16LittleEndian(actual.Arena!.Read(destination, 2)));
        Assert.Equal((short)-6, BinaryPrimitives.ReadInt16LittleEndian(actual.Arena.Read(destination + 2, 2)));
        Assert.Equal((short)10, BinaryPrimitives.ReadInt16LittleEndian(actual.Arena.Read(destination + 4, 2)));
        Assert.Throws<NotSupportedException>(() => actual.Arena.Read(destination + 6, 2));
        Assert.Equal(new int?[] { 3, -5, 10 }, actual.ScriptedColorRegisters!.Ir.ToArray());
        Assert.Null(actual.GeometryRegisters!.Sxy[2]);
        Assert.Null(actual.ScriptedColorRegisters.Material);
        Assert.Throws<NotSupportedException>(() => actual.Arena.Read(destination + 164 * 8 + 6, 2));
    }

    private static byte[] Mesh()
    {
        byte[] bytes = new byte[5248];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1328);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 165);
        for (int index = 0; index < 165; index++)
        {
            Put(bytes, 12 + index * 8, unchecked((short)(-10 * (index + 1))));
        }
        return bytes;
    }
    private static void Put(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    private static short Read(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
}
