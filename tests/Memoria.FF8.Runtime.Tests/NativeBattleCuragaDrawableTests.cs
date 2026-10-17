using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaDrawableTests
{
    private const int Node = NativeBattleCuragaSpawnTests.Node;

    [Fact]
    public void Mesh_RGB_writes_only_three_bytes_and_signed_scales_multiply_columns_without_GTE_IR_saturation()
    {
        var fixture = Fixture(1);
        NativeBattleCuragaFrame frame = NativeBattleCuragaSpawnTests.Channel(fixture.Frame, 0xE4, -4096);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xE8, 2048);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xEC, 8192);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF0, 11, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF4, 22, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF8, 33, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xFC, -100);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0x16F, [0xCD]).Write(Node + 0x9E, [0xAB, 0xEF]) } };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(frame, fixture.Task, Node);
        Assert.Equal(new byte[] { 11, 22, 33, 0xCD }, result.Memory.Arena!.Read(Node + 0x16C, 4));
        Assert.Equal(new short[] { -4096, 0, 0, 0, 2048, 0, 0, 0, 8192 }, Words(result, Node + 0x8C, 9));
        Assert.Equal(new byte[] { 0xAB, 0xEF }, result.Memory.Arena.Read(Node + 0x9E, 2));
        Assert.Equal(new short[] { -100 }, Words(result, Node + 0x1CE, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Single_instance_adds_full_DWORD_depth_bias_but_multiple_instances_store_biased_low_WORDs_only(int count)
    {
        var fixture = Fixture(count);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source => BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(0x38), 1000));
        frame = frame with { State = frame.State with { CopiedCamera = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 65500 } } };
        frame = NativeBattleCuragaEmitterTests.Dwords(frame, Node + 0xDC, [unchecked((uint)-98304), 98304, 65536, 0]);
        frame = NativeBattleCuragaEmitterTests.Dwords(frame, Node + 0xEC, [131072, 196608, 262144, 0]);
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(frame, fixture.Task, Node);
        Assert.Equal(count == 1 ? 66501u : 65504u, NativeBattleCuragaEmitterTests.Dwords(result, Node + 0xC8, 1)[0]);
        if (count == 2)
        {
            Assert.Equal(new short[] { -1, 1, 965, 0, 2, 3, 968, 0 }, Words(result, Node + 0x19C, 8));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Five_basis_selectors_produce_value_identical_output_after_independent_JSON_restore(byte mode)
    {
        var fixture = Fixture(1);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x1B] = mode;
            source[0x1D] = 2;
        });
        frame = frame with { State = frame.State with { CopiedCamera = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 512) with { Z = 20000 } }, Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0xCC, [0, 1, 0, 2, 0, 3]) } };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(frame, fixture.Task, Node);
        NativeBattleCuragaFrame restored = NativeBattleCuragaNodeKinematics.AdvanceDrawable880DB0(NativeBattleCuragaSpawnTests.Clone(frame), fixture.Task, Node);
        Assert.Equal(result.Memory.Arena!.Read(Node + 0x8C, 64), restored.Memory.Arena!.Read(Node + 0x8C, 64));
        Assert.Contains(Words(result, Node + 0x8C, 9), value => value != 0);
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture(int count)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaSpawnStep spawn = NativeBattleCuragaNodeKinematics.Spawn8818A0(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, count, new(1));
        return (spawn.Frame, fixture.Task);
    }

    private static short[] Words(NativeBattleCuragaFrame frame, int offset, int count)
    {
        byte[] data = frame.Memory.Arena!.Read(offset, count * 2);
        return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(index * 2))).ToArray();
    }
}
