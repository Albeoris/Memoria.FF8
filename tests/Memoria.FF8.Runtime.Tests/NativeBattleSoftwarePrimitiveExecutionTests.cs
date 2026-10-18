using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSoftwarePrimitiveExecutionTests
{
    [Fact]
    public void CaptureWritesTheExistingTextureImageAndRetainsUnrelatedUploadedWords()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        execution.ExecuteSoftware(Triangle());
        execution.EndBatch();
        byte[] image = execution.Textures.ReadVram();
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (259 * 1024 + 707))));
        Assert.Equal((ushort)0x7FFF, BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(20)));
        Assert.Equal<uint?>(0, execution.State.Environment.Debug);
        Assert.Equal(704 << 16, execution.State.Environment.Derived!.DrawOffsetX);
    }

    [Fact]
    public void IndependentJsonRestoreContinuesTheRetainedRasterAndImage()
    {
        NativeBattleSoftwarePrimitiveExecution first = Capture();
        first.ExecuteSoftware(Triangle());
        first.EndBatch();
        Saved source = new(first.State, first.Textures, first.Cache!);
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(source))!;
        NativeBattleSoftwarePrimitiveExecution next = new(restored.State, restored.Textures, Table(), true, restored.Cache);
        first.ExecuteSoftware(Triangle());
        next.ExecuteSoftware(Triangle());
        first.EndBatch();
        next.EndBatch();
        Assert.Equal(JsonSerializer.Serialize(first.State), JsonSerializer.Serialize(next.State));
        Assert.Equal(first.Textures.ReadVram(), next.Textures.ReadVram());
        Assert.Equal(JsonSerializer.Serialize(first.Cache), JsonSerializer.Serialize(next.Cache));
    }

    [Fact]
    public void NormalAndSoftwarePageCommandsShareTheSamePointerCache()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        NativeBattleLogicalCommand e1 = new(new("test", 0, 0), 1, 0xE1, [0xE10000B4], 1, [], null);
        execution.ExecuteNormal(e1, new(0xB4), 0);
        NativeSoftwareTextureState retained = execution.State.Texture;
        execution.ExecuteSoftware(e1);
        Assert.Same(retained, execution.State.Texture);
        Assert.Equal((ushort)0xB4, execution.SynchronizeDrawMode(new(0)).TexturePage);
        Assert.False(execution.State.Texture.TexturePointerIsNull);
    }

    private static NativeBattleSoftwarePrimitiveExecution Capture()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().Initialize500400();
        byte[] record = state.EnvironmentBuffers!.Buffer1.ToArray();
        Word(0, 704);
        Word(2, 256);
        Word(4, 128);
        Word(6, 128);
        Word(8, 704);
        Word(10, 256);
        state = state with { EnvironmentBuffers = state.EnvironmentBuffers.ReplaceBuffer(1, record.ToImmutableArray()) };
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(10, 0, 1, 1, [0xFF, 0x7F]);
        NativeBattleSoftwarePrimitiveExecution execution = new(state, textures, Table(), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        execution.ApplyEnvironment45B660(1);
        return execution;

        void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(offset), value);
    }

    private static NativeBattleLogicalCommand Triangle() => new(new("test", 0, 0), 1, 0x20, [0x200000FF, 0x00020002, 0x00020008, 0x00080002], 1, [], null);

    private static NativeSoftwarePrimitiveTable Table()
    {
        uint[] handlers = new uint[256];
        handlers[0x20] = 0x4667C0;
        handlers[0xE1] = 0x467100;
        return new(handlers.ToImmutableArray());
    }

    private sealed record Saved(NativeBattleSoftwarePrimitiveState State, NativeBattleEffectTextureState Textures, NativeBattlePcTextureWindowCache Cache);
}
