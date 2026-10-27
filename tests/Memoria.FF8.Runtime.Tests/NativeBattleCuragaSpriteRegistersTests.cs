using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSpriteRegistersTests
{
    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 1000, 160, 108);

    [Fact]
    public void Accepted_quad_publishes_actual_inputs_fifo_full_mac_and_once_only_depth_without_changing_packets()
    {
        var (memory, resource) = Fixture(1);
        INativeBattleCuragaSpriteGeometry geometry = memory.ProjectedSpriteGeometry(NativeBattleCuragaSpawnTests.Node, 0x1000000, resource, 0, Projection);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix, Projection, 0, 17, 2, geometry: geometry);
        NativeBattleGfGeometryBatch plain = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix, Projection, 0, 17, 2);
        Assert.Equal(SnapshotJson(plain), SnapshotJson(batch));
        NativeBattleEffectMemory state = memory.FlushStorage().Memory;
        NativeBattleGeometryRegisters gte = state.GeometryRegisters!;
        Assert.Equal(new uint?[] { 1000, 1000, 1000, 1000 }, gte.Sz);
        Assert.Equal(250u, gte.Otz);
        Assert.Equal(250u, gte.Mac0);
        Assert.Equal(0u, state.ScriptedColorRegisters!.Flags);
        Assert.Equal(1000, state.ScriptedColorRegisters.Mac[2]);
        Assert.Equal(0x1111, BinaryPrimitives.ReadUInt16LittleEndian(state.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Equal(0x2222, (ushort)(gte.V1[6]!.Value | gte.V1[7]!.Value << 8));
        Assert.Equal(0x3333, (ushort)(gte.V2[6]!.Value | gte.V2[7]!.Value << 8));
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Capture(state);
        Assert.Equal(0x4321, BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x76, 2)));
        Assert.DoesNotContain(true, context.ReadSnapshot(0x96, 2).Known);
        Assert.Equal(250u, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x3C, 4)));
    }

    [Fact]
    public void Rejected_triple_publishes_flags_and_three_inputs_but_never_fourth_or_depth_average()
    {
        var (memory, resource) = Fixture(1);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix with { Z = -1000 }, Projection, 0, 17, 2, geometry: memory.ProjectedSpriteGeometry(NativeBattleCuragaSpawnTests.Node, 0x1000000, resource, 0, Projection));
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
        NativeBattleEffectMemory state = memory.FlushStorage().Memory;
        Assert.NotEqual(0u, state.ScriptedColorRegisters!.Flags!.Value & 0x60000);
        Assert.Equal(77u, state.GeometryRegisters!.Otz);
        Assert.Equal(0x4444, BinaryPrimitives.ReadUInt16LittleEndian(state.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6)));
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Capture(state);
        Assert.Equal(state.ScriptedColorRegisters.Flags, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x40, 4)));
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x3C, 4)));
    }

    [Fact]
    public void Empty_frame_loads_source_matrix_and_retains_data_registers_with_unknown_rolled_padding()
    {
        var (memory, resource) = Fixture(0);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix, Projection, 123, 17, 2, geometry: memory.ProjectedSpriteGeometry(NativeBattleCuragaSpawnTests.Node, 0x1000000, resource, 0, Projection));
        Assert.Empty(batch.Attempts);
        NativeBattleEffectMemory state = memory.FlushStorage().Memory;
        Assert.Equal(0x1234, (ushort)(state.GeometryRegisters!.Rotation20[18]!.Value | state.GeometryRegisters.Rotation20[19]!.Value << 8));
        Assert.Equal(new int?[] { 0, 0, 1000 }, state.GeometryRegisters.Translation);
        Assert.Equal(0xABCDEu, state.ScriptedColorRegisters!.Flags);
        Assert.Equal(77u, state.GeometryRegisters.Otz);
        Assert.DoesNotContain(true, NativeBattleEffectNodeContextMemory.Capture(state).ReadSnapshot(0x96, 2).Known);
    }

    [Fact]
    public void Later_quad_preserves_first_accepted_avsz_and_leaves_fourth_projection_flags_current()
    {
        var (memory, resource) = Fixture(2);
        byte[] bytes = resource.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + 4 + 20 + 8), 3000);
        resource = bytes.ToImmutableArray();
        NativeBattleActionMatrix matrix = Matrix with { Rotation = [4096, 0, 0, 0, 4096, 0, 512, 0, 4096] };
        NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, matrix, Projection, 0, 17, 2, geometry: memory.ProjectedSpriteGeometry(NativeBattleCuragaSpawnTests.Node, 0x1000000, resource, 0, Projection));
        NativeBattleEffectMemory state = memory.FlushStorage().Memory;
        Assert.Equal(250u, state.GeometryRegisters!.Otz);
        Assert.NotEqual(0u, state.ScriptedColorRegisters!.Flags!.Value);
        Assert.NotEqual(250u, state.GeometryRegisters.Mac0);
    }

    [Fact]
    public void Unknown_quad_padding_and_vector_W_survive_projection_and_JSON()
    {
        var (memory, resource) = Fixture(1);
        NativeBattleEffectMemory initial = memory.CaptureProcessMemory();
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Capture(initial);
        foreach (int offset in new[] { 0x76, 0x4A, 0x52, 0x5A, 0x62 })
        {
            context = context.MarkUnknown(offset, 2);
        }
        memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(initial, context));
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix, Projection, 0, 17, 2, geometry: memory.ProjectedSpriteGeometry(NativeBattleCuragaSpawnTests.Node, 0x1000000, resource, 0, Projection));
        Assert.NotNull(Assert.Single(batch.Attempts).Primitive);
        NativeBattleEffectMemory state = memory.FlushStorage().Memory;
        Assert.All(state.ScriptedColorRegisters!.NormalInputKnown!.Value.Skip(6), known => Assert.False(known));
        Assert.All(state.GeometryRegisters!.V1.Skip(6), value => Assert.Null(value));
        Assert.All(state.GeometryRegisters.V2.Skip(6), value => Assert.Null(value));
        Assert.All(state.GeometryRegisters.Rotation20.Skip(18), value => Assert.Null(value));
        NativeBattleGeometryRegisterStep registers = new(state.GeometryRegisters, state.ScriptedColorRegisters);
        NativeBattleGeometryRegisterStep restored = RoundTrip(registers);
        Assert.Equal(SnapshotJson(registers), SnapshotJson(restored));
    }

    [Fact]
    public void Stateful_packet_path_consumes_observed_coordinates_depth_and_flags()
    {
        var (_, resource) = Fixture(1);
        FakeGeometry geometry = new();
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, Matrix, Projection, 0, 17, 2, geometry: geometry);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(1u, primitive.Words[2]);
        Assert.Equal(2u, primitive.Words[4]);
        Assert.Equal(3u, primitive.Words[6]);
        Assert.Equal(4u, primitive.Words[8]);
        Assert.Equal(new[] { 10, 20, 30, 40 }, primitive.VertexPayload4);
        Assert.Equal(117, primitive.Bucket);
        Assert.Equal(new[] { "begin", "triple", "fourth", "average" }, geometry.Calls);
    }

    private static (NativeBattleEffectNodeMemory Memory, ImmutableArray<byte> Resource) Fixture(int count)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleEffectMemory state = NativeBattleEffectMemory.FromColdProcess() with { Arena = fixture.Frame.Memory.Arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 77 }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Flags = 0xABCDE } };
        state = state with { ScriptedGraphics = state.ScriptedGraphics!.InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Capture(state);
        foreach ((int offset, ushort value) in new[] { (0x76, (ushort)0x4321), (0x4A, (ushort)0x4444), (0x52, (ushort)0x2222), (0x5A, (ushort)0x3333), (0x62, (ushort)0x1111) })
        {
            byte[] word = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(word, value);
            context = context.Write(offset, word);
        }
        state = NativeBattleEffectNodeContextMemory.Publish(state, context);
        NativeBattleEffectNodeMemory memory = new(state, fixture.Frame.Effect, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 0x25F8, NativeBattleCuragaSpawnTests.Node, 0x16F80, useSharedScratch: true);
        memory.Word(NativeBattleCuragaSpawnTests.Node + 0xAC + 18, 0x1234);
        byte[] resource = new byte[20 + count * 20];
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(16), (uint)count);
        for (int index = 0; index < count; index++)
        {
            resource[20 + index * 20] = 1;
            resource[21 + index * 20] = 128;
            resource[22 + index * 20] = 1;
            resource[23 + index * 20] = 0x2E;
        }
        return (memory, resource.ToImmutableArray());
    }

    private sealed class FakeGeometry : INativeBattleCuragaSpriteGeometry
    {
        public List<string> Calls { get; } = [];
        public void Begin(NativeBattleActionMatrix source, NativeBattleActionMatrix rolled) => Calls.Add("begin");
        public NativeBattleCuragaSpriteTriple ProjectQuad(ReadOnlySpan<byte> quad)
        {
            Calls.Add("triple");
            return new([new(1, 10, 0x60000), new(2, 20, 0x60000), new(3, 30, 0x60000)], 0);
        }
        public NativeBattleModuleProjectedVertex ProjectFourth()
        {
            Calls.Add("fourth");
            return new(4, 40, 0x60000);
        }
        public int AverageDepth()
        {
            Calls.Add("average");
            return 400;
        }
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
