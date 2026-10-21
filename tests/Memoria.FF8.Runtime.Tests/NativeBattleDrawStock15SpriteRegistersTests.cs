using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawStock15SpriteRegistersTests
{
    [Fact]
    public void Every_accepted_quad_has_its_fourth_projection_but_only_the_first_computes_cached_average()
    {
        var fixture = Fixture(0x8B5C70);
        NativeBattleDrawStock15SpriteTemporaryResult result = Emit(fixture.Task, fixture.Resources, fixture.Camera);
        Assert.Equal(4, result.Operations.OfType<NativeBattleDrawStock15MatrixProduct>().Count());
        Assert.Equal(4, result.Operations.OfType<NativeBattleDrawStock15LongTransform>().Count());
        Assert.All(result.Operations.OfType<NativeBattleDrawStock15MatrixProduct>(), operation => Assert.Equal(132, operation.TemporaryLeftMatrixOffset));
        Assert.All(result.Operations.OfType<NativeBattleDrawStock15LongTransform>(), operation => Assert.False(operation.LoadRotation));
        Assert.All(result.Operations.OfType<NativeBattleDrawStock15Matrix>(), operation => Assert.Equal(100, operation.TemporaryMatrixOffset));
        NativeBattleDrawStock15Projection[] projections = result.Operations.OfType<NativeBattleDrawStock15Projection>().ToArray();
        Assert.Equal(8, projections.Length);
        for (int index = 0; index < projections.Length; index++)
        {
            Assert.Equal(index % 2 == 0 ? 3 : 1, projections[index].Points.Length);
            Assert.Equal(index % 2 == 0 ? 68 : 92, projections[index].TemporaryVectorOffset);
            Assert.Equal(index % 2 != 0, projections[index].FinalCue);
        }
        Assert.Single(result.Operations.OfType<NativeBattleDrawStock15AverageDepth>());
        NativeBattleDrawStock15SpriteTemporaryResult rejected = Emit(fixture.Task, fixture.Resources, fixture.Camera with { Z = -10000 });
        Assert.Equal(4, rejected.Operations.OfType<NativeBattleDrawStock15MatrixProduct>().Count());
        Assert.Equal(4, rejected.Operations.OfType<NativeBattleDrawStock15Projection>().Count());
        Assert.All(rejected.Operations.OfType<NativeBattleDrawStock15Projection>(), operation => Assert.False(operation.FinalCue));
        Assert.Empty(rejected.Operations.OfType<NativeBattleDrawStock15AverageDepth>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Captured_GTE_matrix_padding_keeps_raw_bytes_and_knownness_without_overwriting_quad_padding(bool known)
    {
        var fixture = Fixture(0x8B5C70);
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(fixture.Camera, known ? (ushort)0xABCD : null);
        packed = packed with { Bytes = packed.Bytes.SetItem(18, 0xCD).SetItem(19, 0xAB) };
        NativeBattleDrawStock15SpriteTemporaryResult result = Emit(fixture.Task, fixture.Resources, fixture.Camera, packed);
        var padding = result.Staging.ReadSnapshot(NativeBattleTemporaryMemory.Address + 150, 2);
        if (known)
        {
            Assert.Equal(new byte[] { 0xCD, 0xAB }, padding.Item1);
        }
        Assert.Equal(new[] { known, known }, padding.Item2);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 118, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Staging.Read(NativeBattleTemporaryMemory.Address + 74, 2));
    }

    [Fact]
    public void Original_flags_zero_preserve_unused_scale_operands_and_CPU_composition_publishes_only_its_matrix_load()
    {
        var fixture = Fixture(0x8B6440);
        NativeBattleDrawStock15SpriteTemporaryResult result = Emit(fixture.Task, fixture.Resources, fixture.Camera);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 36, 2)));
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 20, 4)));
        ImmutableArray<NativeBattleDrawStock15ParticleOperation> preparation = NativeBattleDrawStock15SpriteRendering.Preparation(fixture.Task, fixture.Camera);
        Assert.Equal(new[] { 'Y', 'Z', 'X' }, preparation.OfType<NativeBattleDrawStock15Rotate>().Select(operation => operation.Axis));
        Assert.Equal(4, preparation.Length);
        Assert.IsType<NativeBattleDrawStock15Matrix>(preparation[3]);
        Assert.Empty(preparation.OfType<NativeBattleDrawStock15MatrixProduct>());
        Assert.Empty(preparation.OfType<NativeBattleDrawStock15LongTransform>());
    }

    private static NativeBattleDrawStock15SpriteTemporaryResult Emit(NativeBattleModulePoolTask task, NativeBattleDrawStock15ParticleResources resources, NativeBattleActionMatrix camera, NativeBattlePackedMatrix32? packed = null)
    {
        uint address = NativeBattleTemporaryMemory.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, Enumerable.Repeat((byte)0xA5, 180).ToArray());
        return NativeBattleDrawStock15SpriteRendering.Emit180(staging, address, task, resources, camera, new(camera, 216, 160, 108), 0, packed);
    }

    private static (NativeBattleModulePoolTask Task, NativeBattleDrawStock15ParticleResources Resources, NativeBattleActionMatrix Camera) Fixture(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2732CF8, 0x2732D08, 300, 100);
        NativeBattleModulePoolTask task = pool.Slots[0] with { Callback = callback };
        task = task.WriteDword(0x4C, callback == 0x8B5C70 ? 0x162B520u : 0x162B730u).WriteWord(0x1C, 10).WriteWord(0x1E, unchecked((ushort)-20)).WriteWord(0x20, 30).WriteWord(0x22, 0xBEEF);
        NativeBattleDrawStock15ParticleResources resources = new(Sprite(6, 4), Sprite(0, 2));
        return (task, resources, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 10000, 4096));
    }

    private static ImmutableArray<byte> Sprite(byte kind, int quads)
    {
        int stride = 4 + quads * 20;
        byte[] bytes = new byte[24 + 6 * stride];
        bytes[3] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 6);
        for (int frame = 0; frame < 6; frame++)
        {
            int start = 24 + frame * stride;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)start);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start), (uint)quads);
            for (int quad = 0; quad < quads; quad++)
            {
                bytes[start + 4 + quad * 20] = 1;
                bytes[start + 6 + quad * 20] = 1;
                bytes[start + 7 + quad * 20] = 0x2C;
            }
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), ushort.MaxValue);
        return bytes.ToImmutableArray();
    }
}
