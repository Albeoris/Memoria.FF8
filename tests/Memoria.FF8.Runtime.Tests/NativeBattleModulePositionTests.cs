using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModulePositionTests
{
    [Fact]
    public void PositionProducerRetainsLocalWordsPhysicalModelAndPackedResourceAcrossJson()
    {
        NativeBattleModuleResource resource = Resource();
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendPositionResource(6, new(-1, 2, -3), resource, 0x81234505);
        ImmutableArray<byte> payload = state.Slots[state.Tail!.Value].Payload;
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan().Slice(2)));
        Assert.Equal(new byte[] { 255, 255, 2, 0, 253, 255, 0, 0 }, payload.AsSpan().Slice(4, 8).ToArray());
        Assert.Equal(0x1D97668u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan().Slice(12)));
        Assert.Equal(0x81234505u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan().Slice(16)));
        Assert.Equal(resource.Address, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan().Slice(20)));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))));
    }

    [Fact]
    public void PlacementTransformsAtCallbackTimeAndAppendsItsActualSpriteInTheSamePassEvenWhenPaused()
    {
        NativeBattleModuleResource resource = Resource();
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendPositionResource(2, new(1, 2, 3), resource, 0x00010005);
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
        NativeBattleModuleInputs inputs = new(1)
        {
            Resources = [resource],
            TransformModelPosition = (model, local) =>
            {
                Assert.Equal(2, model);
                Assert.Equal(new NativeBattleVector(1, 2, 3), local);
                return new(100, -200, 300);
            }
        };
        NativeBattleModuleStep result = state.Advance(inputs);
        Assert.Equal(new NativeBattleModuleAnchor(100, -200, 300, 0), Assert.Single(result.Draws).Anchor);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored.Advance(inputs)));
        Assert.Equal(NativeBattleModuleKind.Sprite570BC0, result.State.Slots[result.State.Head!.Value].Kind);
        Assert.Equal((ushort)0, result.Draws[0].Phase);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(3, 102)]
    [InlineData(-1, 100)]
    [InlineData(-3, 98)]
    public void PlacementUsesBiasedDoubleTiesBeforeAddingCurrentTranslation(short local, short expected)
    {
        NativeBattleGraphicsModel model = new(0, 0) { GeometryWorldMatrix = new([2048, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 65535, -65536, 0) };
        Assert.Equal(new NativeBattleVector(expected, 1, -2), NativeBattlePoseMatrices.TransformModulePosition570CC0(model, new(local, 2, -2)));
    }

    [Fact]
    public void PlacementRequiresActualMatrixAndCallbackAndDoesNotInferThemFromTransformWords()
    {
        NativeBattleGraphicsModel model = new(0, 0) { Transform = new(new(0, 0, 0), new(1, 2, 3), 0, 0, 4096), Rotation12 = 0, Rotation14 = 0, Rotation16 = 0 };
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.TransformModulePosition570CC0(model, new(1, 2, 3)));
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendPositionResource(0, new(1, 2, 3), Resource(), 5);
        Assert.Throws<NotSupportedException>(() => state.Advance(new(0) { Resources = [Resource()] }));
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleState.Clear().AppendPositionResource(7, new(1, 2, 3), Resource(), 5));
    }

    private static NativeBattleModuleResource Resource()
    {
        byte[] bytes = new byte[18];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), ushort.MaxValue);
        return new(5, 0xC70000, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
