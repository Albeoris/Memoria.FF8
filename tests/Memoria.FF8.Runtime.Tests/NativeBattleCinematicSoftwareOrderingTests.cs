using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicSoftwareOrderingTests
{
    [Fact]
    public void PrivatePassIgnoresHardwareModesAndNormalTablesAndSkipsNullSoftwareEntries()
    {
        Probe context = new();
        ImmutableArray<NativeBattleLogicalCommand> commands = [Command(0xE3, [0xE3000000]), Command(1, [0x01000000]), Command(0x80, [0x80000000, 0, 0, 0])];
        NativeBattleCinematicSoftwareOrdering.Execute45CEE0(3, () => commands, context);
        Assert.Equal(new byte[] { 0xE3, 0x80 }, context.Executed);
        Assert.Equal(1, context.Begins);
        Assert.Equal(1, context.Flushes);
        Assert.Equal(0, context.Ends);
    }

    [Theory]
    [InlineData(16384)]
    [InlineData(16385)]
    public void ExhaustedProxyCursorDoesNotReadCommandsOrResetFlags(int cursor)
    {
        Probe context = new();
        NativeBattleCinematicSoftwareOrdering.Execute45CEE0(cursor, () => throw new InvalidOperationException("Capacity rejection precedes traversal."), context);
        Assert.Equal(0, context.Begins);
        Assert.Equal(0, context.Flushes);
        Assert.Empty(context.Executed);
    }

    [Fact]
    public void EmptyOrderingStillPublishesEntryResetWithoutClearingBatchDebug()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        state = state with { Environment = state.Environment with { Debug = 1 }, DispatchFlags1CA8880 = null };
        NativeBattleSoftwarePrimitiveExecution execution = Execution(state);
        NativeBattleCinematicSoftwareOrdering.Execute45CEE0(0, () => [], execution);
        Assert.Equal(0u, execution.LiveDispatchFlags);
        Assert.Equal(1u, execution.LiveDebug);
        Assert.Same(state.Environment, execution.State.Environment);
    }

    [Fact]
    public void SoftwarePixelsFlushIntoCanonicalVramWhileDebugAndOtherOwnersRemainRetained()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        state = state with { Environment = state.Environment with { Debug = 1 }, DispatchFlags1CA8880 = 0xFFFFFF };
        NativeBattleSoftwarePrimitiveExecution execution = Execution(state);
        ImmutableArray<NativeBattleLogicalCommand> commands = [Command(2, [0x020000FF, 0x00000000, 0x00010001]), Command(0x80, [0x80000000, 0, 1, 0x00010001])];
        NativeBattleCinematicSoftwareOrdering.Execute45CEE0(2, () => commands, execution);
        byte[] pixels = execution.Textures.ReadVram();
        Assert.Equal((byte)31, pixels[0]);
        Assert.Equal((byte)31, pixels[2]);
        Assert.Equal(1u, execution.LiveDebug);
        Assert.Equal(0u, execution.LiveDispatchFlags);
        Assert.Same(state.Texture, execution.State.Texture);
        Assert.Same(state.Raster, execution.State.Raster);
        Assert.Equal(pixels, NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(execution.Textures))!.ReadVram());
    }

    [Fact]
    public void WarmOmittedFlagsRemainUnknownUntilAuthenticEntryProducer()
    {
        NativeBattleSoftwarePrimitiveState missing = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer() with { DispatchFlags1CA8880 = null };
        string json = NativeBattleTestSnapshots.Json(missing);
        Assert.DoesNotContain("DispatchFlags1CA8880", json);
        NativeBattleSoftwarePrimitiveState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleSoftwarePrimitiveState>(json)!;
        Assert.Null(restored.DispatchFlags1CA8880);
        NativeBattleSoftwarePrimitiveExecution execution = Execution(restored);
        Assert.Throws<NotSupportedException>(() => execution.LiveDispatchFlags);
        NativeBattleCinematicSoftwareOrdering.Execute45CEE0(0, () => [], execution);
        Assert.Equal(0u, execution.LiveDispatchFlags);
        Assert.Equal(0u, NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().DispatchFlags1CA8880);
    }

    private static NativeBattleLogicalCommand Command(byte opcode, ImmutableArray<uint> words) => new(new("test", 0, 0), 1, opcode, words, 1, [], null);

    private static NativeBattleSoftwarePrimitiveExecution Execution(NativeBattleSoftwarePrimitiveState state)
    {
        uint[] handlers = new uint[256];
        handlers[2] = 0x466EF0;
        handlers[0x80] = 0x4670D0;
        return new(state, NativeBattleEffectTextureState.FromColdProcess(), new(handlers.ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
    }

    private sealed class Probe : INativeBattleLogicalExecutionContext
    {
        public readonly List<byte> Executed = [];
        public int Begins;
        public int Flushes;
        public int Ends;
        public uint LiveMode => throw new InvalidOperationException("Private ordering does not inspect ordinary draw modes.");
        public uint LiveDebug => throw new InvalidOperationException("Private ordering does not inspect ordinary debug delivery.");
        public bool HardwareEnabled => throw new InvalidOperationException("Private ordering does not inspect hardware capability.");
        public NativeSoftwarePrimitiveTable SoftwareTable { get; } = Table();
        public NativeBattlePcTextureWindowCache? Cache { get; set; }
        public void ExecuteSoftware(NativeBattleLogicalCommand command) => Executed.Add(command.Opcode);
        public void ExecuteNormal(NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode, byte pass) => throw new InvalidOperationException("Private ordering never delivers a normal handler.");
        public NativeBattlePrimitiveDrawMode SynchronizeDrawMode(NativeBattlePrimitiveDrawMode mode) => throw new InvalidOperationException("Private ordering does not synchronize ordinary draw modes.");
        public void BeginOrdering() => Begins++;
        public void FlushSoftwareImage() => Flushes++;
        public void EndBatch() => Ends++;

        private static NativeSoftwarePrimitiveTable Table()
        {
            uint[] handlers = new uint[256];
            handlers[0xE3] = 0x467120;
            handlers[0x80] = 0x4670D0;
            return new(handlers.ToImmutableArray());
        }
    }
}
