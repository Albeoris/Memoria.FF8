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

    [Fact]
    public void StateCommandsPublishTheSameRawDrawEnvironmentAndPreserveItsOtherBytes()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        byte[] before = execution.State.EnvironmentBuffers!.CurrentGlobal!.Value.ToArray();
        execution.ExecuteSoftware(Command(0xE3, 700u | 256u << 10));
        execution.ExecuteSoftware(Command(0xE4, 827u | 383u << 10));
        execution.ExecuteSoftware(Command(0xE5, 700u | 256u << 11));
        byte[] current = execution.State.EnvironmentBuffers!.CurrentGlobal!.Value.ToArray();
        Assert.Equal((short)700, BinaryPrimitives.ReadInt16LittleEndian(current));
        Assert.Equal((ushort)128, BinaryPrimitives.ReadUInt16LittleEndian(current.AsSpan(4)));
        Assert.Equal((short)700, BinaryPrimitives.ReadInt16LittleEndian(current.AsSpan(8)));
        Assert.Equal(before.AsSpan(12).ToArray(), current.AsSpan(12).ToArray());
        Assert.Equal((short)704, BinaryPrimitives.ReadInt16LittleEndian(execution.State.EnvironmentBuffers.Buffer1.AsSpan()));
    }

    [Fact]
    public void InvalidationRetainsTheCachedWordsFromBeforePendingSoftwareWrites()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        execution.Cache = execution.Cache!.Preprocess462F90(0x11B, 0).Prepare464BD0().Cache;
        Assert.True(execution.Cache.Rows[91].Regions[0].CurrentPixels);
        execution.ExecuteSoftware(Triangle());
        execution.ExecuteSoftware(Command(0xE4, 831u | 383u << 10));
        NativeBattlePcTextureWindowRegion cached = execution.Cache!.Rows[91].Regions[0];
        Assert.False(cached.CurrentPixels);
        Assert.Equal((ushort)0, cached.RetainedPixels!.ReadWord(707, 259));
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(execution.Textures.ReadVram().AsSpan(2 * (259 * 1024 + 707))));
    }

    [Fact]
    public void ExecutedTextureVersionsSurviveLaterInvalidationAndRefreshInTheSameFrame()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        NativeBattleLogicalResolvedCommand first = Resolve();
        execution.ExecuteSoftware(Triangle());
        execution.ExecuteSoftware(Command(0xE4, 831u | 383u << 10));
        NativeBattleLogicalResolvedCommand second = Resolve();
        Assert.Equal(0, first.DrawMode.TexturePixelSnapshot);
        Assert.Equal(1, second.DrawMode.TexturePixelSnapshot);
        Assert.Equal((ushort)0, execution.TextureSnapshots[0].ReadWord(707, 259));
        Assert.Equal((ushort)31, execution.TextureSnapshots[1].ReadWord(707, 259));
        Assert.Equal(1, Resolve().DrawMode.TexturePixelSnapshot);
        Assert.Equal(2, execution.TextureSnapshots.Length);
        string json = JsonSerializer.Serialize(execution.TextureSnapshots);
        ImmutableArray<NativeBattlePcTextureWindowPixels> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattlePcTextureWindowPixels>>(json);
        Assert.Equal((ushort)0, restored[first.DrawMode.TexturePixelSnapshot!.Value].ReadWord(707, 259));
        Assert.Equal((ushort)31, restored[second.DrawMode.TexturePixelSnapshot!.Value].ReadWord(707, 259));
        NativeBattlePcTextureWindowRow row = execution.Cache!.Rows[91];
        NativeBattlePcTextureWindowRegion region = row.Regions[0] with { CurrentPixels = false, RetainedPixels = restored[1] };
        execution.Cache = execution.Cache with { Rows = execution.Cache.Rows.SetItem(91, row with { Regions = row.Regions.SetItem(0, region) }) };
        NativeBattleLogicalResolvedCommand coldIdentity = second with { DrawMode = second.DrawMode with { TextureSelection = second.DrawMode.TextureSelection! with { UsesRetainedPixelWords = true }, TexturePixelSnapshot = null } };
        Assert.Equal(1, execution.RetainTexturePixels(coldIdentity).DrawMode.TexturePixelSnapshot);
        Assert.Equal(2, execution.TextureSnapshots.Length);

        NativeBattleLogicalResolvedCommand Resolve()
        {
            execution.Cache = execution.Cache!.Preprocess462F90(0x11B, 0).Prepare464BD0().Cache;
            NativeBattlePcTextureWindowResolve selected = execution.Cache.Resolve465CE0(0x11B, 0, 0);
            execution.Cache = selected.Cache;
            NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x24, [0x24000000, 0x00020002, 0, 0x00020008, 0x011B0000, 0x00080002, 0], 1, [], null);
            return execution.RetainTexturePixels(new(command, new(0x11B) { TextureWindowBits = 0, TextureSelection = selected.Selection }, 1));
        }
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
    private static NativeBattleLogicalCommand Command(byte opcode, uint payload) => new(new("test", 0, 0), 1, opcode, [(uint)opcode << 24 | payload], 1, [], null);

    private static NativeSoftwarePrimitiveTable Table()
    {
        uint[] handlers = new uint[256];
        handlers[0x20] = 0x4667C0;
        handlers[0xE1] = 0x467100;
        handlers[0xE3] = 0x467120;
        handlers[0xE4] = 0x467130;
        handlers[0xE5] = 0x467140;
        return new(handlers.ToImmutableArray());
    }

    private sealed record Saved(NativeBattleSoftwarePrimitiveState State, NativeBattleEffectTextureState Textures, NativeBattlePcTextureWindowCache Cache);
}
