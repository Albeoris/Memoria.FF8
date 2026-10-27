using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSoftwarePrimitiveExecutionTests
{
    [Fact]
    public void RepeatedNormalTexturePreparationRetainsUnchangedOwnersAndPublishesChangedPointers()
    {
        NativeBattleSoftwarePrimitiveExecution source = Capture();
        NativeBattleSoftwarePrimitiveExecution execution = new(source.State with { ViewportInputs = new(640, 480, 0, 0) }, source.Textures, Table(), true, source.Cache!);
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x24, [0x24000000, 0, 0xFFFF0000, 0, 0x011C0000, 0, 0], 1, [], null);
        execution.ExecuteNormal(command, new(0x11C), 0);
        NativeBattleSoftwarePrimitiveState retained = execution.State;
        NativeBattlePrimitiveDrawMode mode = new(0x11C);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            execution.ExecuteNormal(command, mode, 0);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(retained, execution.State);
        Assert.True(allocated < 40000, $"Unchanged normal texture preparation allocated {allocated} bytes.");
        NativeBattleLogicalCommand changed = command with { Words = command.Words.SetItem(2, 0x12340000) };
        execution.ExecuteNormal(changed, mode, 0);
        Assert.NotSame(retained, execution.State);
        Assert.Equal<ushort?>(0x1234, execution.State.Texture.EncodedClut);
        Assert.Equal<ushort?>(0x7FFF, retained.Texture.EncodedClut);
        NativeSoftwareTextureState incomplete = execution.State.Texture with { PalettePointerIsNull = null };
        NativeSoftwareTextureState repaired = NativeSoftwareTexturePreparation.PrepareClut45B920(incomplete, 0x9234, 0);
        Assert.NotSame(incomplete, repaired);
        Assert.False(repaired.PalettePointerIsNull);
        Assert.Same(repaired, NativeSoftwareTexturePreparation.PrepareClut45B920(repaired, 0x1234, 0));
        NativeSoftwareTextureState wrongOffset = repaired with { PaletteByteOffset = 1 };
        Assert.Equal(repaired, NativeSoftwareTexturePreparation.PrepareClut45B920(wrongOffset, 0x1234, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSoftwareTexturePreparation.PrepareClut45B920(repaired, 0x1234, -1));
    }

    [Fact]
    public void Debug_uses_the_existing_environment_owner_until_successful_batch_cleanup()
    {
        NativeBattleSoftwarePrimitiveExecution source = Capture();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(source.State.Environment, 0, 0);
        NativeBattleSoftwarePrimitiveExecution execution = new(source.State with { Environment = environment }, source.Textures, Table(), true, source.Cache!);
        Assert.Equal(1u, execution.LiveDebug);
        execution.ExecuteNormal(Command(0xE1, 0xB4), new(0xB4), 0);
        execution.ExecuteSoftware(Triangle());
        execution.ExecuteSoftware(Triangle());
        Assert.Equal(1u, execution.LiveDebug);
        execution.EndBatch();
        Assert.Equal(0u, execution.LiveDebug);
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(execution.Textures.ReadVram().AsSpan(2 * (259 * 1024 + 707))));
    }

    [Fact]
    public void Unknown_consumed_debug_state_is_not_replaced_with_an_ordinary_default()
    {
        NativeBattleSoftwarePrimitiveExecution source = Capture();
        NativeBattleSoftwarePrimitiveExecution execution = new(source.State with { Environment = source.State.Environment with { Debug = null } }, source.Textures, Table(), true, source.Cache!);
        Assert.Throws<NotSupportedException>(() => execution.LiveDebug);
    }

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
        Saved restored = JsonSerializer.Deserialize<Saved>(NativeBattleTestSnapshots.Json(source))!;
        NativeBattleSoftwarePrimitiveExecution next = new(restored.State, restored.Textures, Table(), true, restored.Cache);
        first.ExecuteSoftware(Triangle());
        next.ExecuteSoftware(Triangle());
        first.EndBatch();
        next.EndBatch();
        Assert.Equal(NativeBattleTestSnapshots.Json(first.State), NativeBattleTestSnapshots.Json(next.State));
        Assert.Equal(first.Textures.ReadVram(), next.Textures.ReadVram());
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Cache), NativeBattleTestSnapshots.Json(next.Cache));
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
    public void NormalDirectColorPreparationRetainsTheUnusedClutPointerAcrossJsonRestore()
    {
        NativeBattleSoftwarePrimitiveExecution original = Capture();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(original.State.Environment, 0, 0);
        NativeBattleSoftwarePrimitiveState state = NativeBattleTestSnapshots.RestoreJson<NativeBattleSoftwarePrimitiveState>(NativeBattleTestSnapshots.Json(original.State with { Environment = environment, ViewportInputs = new(640, 480, 0, 0) }))!;
        NativeBattleSoftwarePrimitiveExecution execution = new(state, original.Textures, Table(), true, original.Cache!);
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x24, [0x24000000, 0, 0xFFFF0000, 0, 0x011C0000, 0, 0], 1, [], null);
        execution.ExecuteNormal(command, new(0x11C), 1);
        Assert.Equal<int?>(2, execution.State.Texture.Depth);
        Assert.Equal<ushort?>(0x7FFF, execution.State.Texture.EncodedClut);
        Assert.Equal<int?>(1048544, execution.State.Texture.PaletteByteOffset);
        Assert.False(execution.State.Texture.PalettePointerIsNull);
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
        string json = NativeBattleTestSnapshots.Json(execution.TextureSnapshots);
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

    [Fact]
    public void ModeThreePolygonsRetainTheirExecutedViewportAcrossLaterEnvironmentChangesAndJsonRestore()
    {
        NativeBattleSoftwarePrimitiveExecution original = Capture();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(new(576, 256, 895, 487, 576, 256, 0, 0, 0, null));
        NativeBattleSoftwarePrimitiveState state = original.State with { Environment = environment, ViewportInputs = new(640, 480, 3, -7) };
        state = NativeBattleTestSnapshots.RestoreJson<NativeBattleSoftwarePrimitiveState>(NativeBattleTestSnapshots.Json(state))!;
        NativeBattleSoftwarePrimitiveExecution execution = new(state, original.Textures, Table(), true, original.Cache!);
        NativeBattleLogicalCommand triangle = Triangle();
        execution.ExecuteNormal(triangle, new(0), 1);
        NativeBattleLogicalResolvedCommand resolved = execution.RetainTexturePixels(new(triangle, new(0), 1));
        Assert.Equal(state.ViewportInputs, resolved.DrawMode.ViewportInputs);
        Assert.Equal(NativeSoftwareNormalViewport.Calculate45B6F0(environment, state.ViewportInputs!), resolved.DrawMode.NormalViewport);
        NativeBattleLogicalResolvedCommand repeated = execution.RetainTexturePixels(new(triangle, new(0), 1));
        Assert.Same(resolved.DrawMode.NormalViewport, repeated.DrawMode.NormalViewport);
        execution.ExecuteNormal(Command(0xE3, 0), new(0), 1);
        NativeBattleLogicalResolvedCommand changed = execution.RetainTexturePixels(new(triangle, new(0), 1));
        Assert.NotSame(resolved.DrawMode.NormalViewport, changed.DrawMode.NormalViewport);
        Assert.Equal(NativeSoftwareNormalViewport.Calculate45B6F0(execution.State.Environment, state.ViewportInputs!), changed.DrawMode.NormalViewport);
        Assert.Equal(2f, resolved.DrawMode.NormalViewport!.ScaleX);
        Assert.Equal(2.5f, resolved.DrawMode.NormalViewport.NormalOffsetX);
        Assert.Equal(-7.5f, resolved.DrawMode.NormalViewport.NormalOffsetY);
        Assert.Equal(NativeBattleTestSnapshots.Json(resolved), NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalResolvedCommand>(NativeBattleTestSnapshots.Json(resolved))));
    }

    [Fact]
    public void NondefaultNormalGeometryRequiresExplicitViewportAndUnprovedTileRemainsUnsupported()
    {
        NativeBattleSoftwarePrimitiveExecution original = Capture();
        NativeBattleLogicalCommand triangle = Triangle();
        Assert.Throws<NotSupportedException>(() => original.ExecuteNormal(triangle, new(0), 1));
        NativeBattleSoftwarePrimitiveExecution execution = new(original.State with { ViewportInputs = new(640, 480, 0, 0) }, original.Textures, Table(), true, original.Cache!);
        NativeBattleLogicalCommand tile = new(new("test", 0, 0), 1, 0x60, [0x600000FF, 0, 0x00010001], 1, [], null);
        Assert.Throws<NotSupportedException>(() => execution.ExecuteNormal(tile, new(0), 1));
    }

    [Fact]
    public void PreparationBoundaryPublishesSoftwarePixelsBeforeNormalEnvironmentInvalidatesPreparedResources()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Capture();
        execution.Cache = execution.Cache!.Preprocess462F90(0x11B, 0).Prepare464BD0().Cache;
        execution.ExecuteSoftware(Triangle());
        execution.BeginPass(1);
        execution.Cache = execution.Cache.Prepare464BD0().Cache;
        execution.ExecuteNormal(Command(0xE4, 831u | 383u << 10), new(0), 1);
        NativeBattlePcTextureWindowRegion cached = execution.Cache.Rows[91].Regions[0];
        Assert.False(cached.CurrentPixels);
        Assert.Equal((ushort)31, cached.RetainedPixels!.ReadWord(707, 259));
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(execution.Textures.ReadVram().AsSpan(2 * (259 * 1024 + 707))));
        Assert.Equal(704 << 16, execution.State.Environment.Derived!.DrawOffsetX);
        Assert.Equal<uint?>(0, execution.State.Environment.Debug);
    }

    private static NativeBattleSoftwarePrimitiveExecution Capture()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().Initialize500400() with { ViewportInputs = null };
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
