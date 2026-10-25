using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31StackTests
{
    [Fact]
    public void Target_service_observations_supply_current_physical_pointers_and_return_bytes()
    {
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.ObserveTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, 0x1D972C0, 0x196B068);
        Assert.Equal(new uint[] { 0x1D97300, 0x56C843, 0x2000030, 0x2000020, 0x50225A }, Dwords(stack, -0xFC));
        Assert.Equal(new uint[] { 0x2000020, 0x2000030, 0x2000000, 0x1D97300, 0x196B068 }, Dwords(stack, -0xE0));
        Assert.Equal(new uint[] { 0x50221A, 0x50221B, 0x502221, 0x45DEF5, 0x50222D, 0x50222E, 0x56C833, 0x56C83D, 0x56C83E, 0x502255 }, stack.Observations.Select(item => item.Instruction));
        Assert.All(stack.Observations, item => Assert.Equal(4, item.Length));
        NativeBattlePackedMatrix32 matrix = NativeBattleEffect31Stack.ReadMatrix(stack, 0x872020);
        Assert.Equal(0x50, BinaryPrimitives.ReadUInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)));
        Assert.All(matrix.Known.Take(20), known => Assert.True(known));
        Assert.All(matrix.Known.Skip(20), known => Assert.False(known));
        Assert.Throws<NotSupportedException>(() => matrix.ReadMatrix());
        NativeBattleStackBytes changed = NativeBattleEffect31Stack.ObserveTarget(stack, 0x2100000, 0x1D9735C, 0x196B098);
        Assert.Equal(0x1D9739Cu, changed.ReadUInt32(-0xFC));
        Assert.Equal(0x2100030u, changed.ReadUInt32(-0xF4));
        Assert.Equal(0x196B098u, changed.ReadUInt32(-0xD0));
    }

    [Theory]
    [InlineData(0x8721D0u)]
    [InlineData(0x8725B0u)]
    [InlineData(0x872B80u)]
    public void Shared_matrix_requires_bounds_and_the_current_world_copy(uint stateBody)
    {
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.Begin();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(stack, stateBody));
        NativeBattleModulePoolTask task = Task(0x871F60);
        NativeBattlePackedMatrix32 world = Matrix(1);
        stack = NativeBattleEffect31Stack.ObserveActor(stack, 0x26D10A8, task, 0x872020, 0x8720FD, world);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(stack, stateBody));
        Assert.All(stack.Observations, observation => Assert.Equal(4, observation.Length));
        Assert.Equal(16, stack.Known.Count(known => known));
        stack = NativeBattleEffect31Stack.ObserveBounds(stack, 0x2001000, 2);
        NativeBattlePackedMatrix32 matrix = NativeBattleEffect31Stack.ReadMatrix(stack, stateBody);
        Assert.Equal(0x2001028u, BinaryPrimitives.ReadUInt32LittleEndian(matrix.Bytes.AsSpan()));
        Assert.Equal(world.Bytes.Take(16), matrix.Bytes.Skip(4).Take(16));
        Assert.All(matrix.Known.Skip(20), known => Assert.False(known));
        Assert.Equal(20, stack.Known.Count(known => known));
    }

    [Theory]
    [InlineData(0x872020u, 0x872075u, 0x871F60u)]
    [InlineData(0x8729F0u, 0x872A3Fu, 0x872920u)]
    [InlineData(0x8721D0u, 0x87221Fu, 0x872170u)]
    [InlineData(0x8725B0u, 0x8725FFu, 0x872170u)]
    [InlineData(0x872B80u, 0x872BFBu, 0x872B10u)]
    public void Postmultiply_writes_nine_words_and_retains_the_physical_padding(uint stateBody, uint instruction, uint callback)
    {
        int matrixOffset = stateBody == 0x872020 ? -0xFC : stateBody == 0x8729F0 ? -0xE0 : -0xA4;
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.Begin().StoreLiteral(matrixOffset + 18, new byte[] { 0xA5, 0x5A }, 0x1000, "Actual retained padding");
        NativeBattlePackedMatrix32 rotated = Matrix(70);
        stack = NativeBattleEffect31Stack.ObserveActor(stack, 0x26D10A8, Task(callback), stateBody, instruction, rotated);
        NativeBattlePackedMatrix32 matrix = NativeBattleEffect31Stack.ReadMatrix(stack, stateBody);
        Assert.Equal(rotated.Bytes.Take(18), matrix.Bytes.Take(18));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, matrix.Bytes.Skip(18).Take(2));
        Assert.Equal(9, stack.Observations.Skip(1).Count());
        Assert.All(stack.Observations.Skip(1), observation => Assert.Equal(2, observation.Length));
        Assert.All(matrix.Known.Skip(20), known => Assert.False(known));
    }

    [Fact]
    public void A_partially_known_world_copy_stays_unknown_and_unknown_padding_is_retained()
    {
        NativeBattlePackedMatrix32 world = Matrix(20);
        bool[] known = world.Known.ToArray();
        known[3] = false;
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.ObserveBounds(NativeBattleEffect31Stack.Begin(), 0x2001000, 2);
        stack = NativeBattleEffect31Stack.ObserveActor(stack, 0x26D10A8, Task(0x871F60), 0x872020, 0x8720FD, world with { Known = known.ToImmutableArray() });
        Assert.False(stack.Known[-0x9D - stack.FirstOffset]);
        Assert.Equal(0, stack.Bytes[-0x9D - stack.FirstOffset]);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(stack, 0x872B80));
        NativeBattleStackBytes rotationOnly = NativeBattleEffect31Stack.ObserveActor(NativeBattleEffect31Stack.Begin(), 0x26D10A8, Task(0x872B10), 0x872B80, 0x872BFB, world);
        NativeBattlePackedMatrix32 matrix = NativeBattleEffect31Stack.ReadMatrix(rotationOnly, 0x872B80);
        Assert.False(matrix.Known[18]);
        Assert.False(matrix.Known[19]);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ObserveActor(rotationOnly, 0x26D10A8, Task(0x872B10), 0x872B80, 0x872BFB, world with { Known = known.ToImmutableArray() }));
    }

    [Fact]
    public void Delayed_initialization_and_later_context_store_form_the_second_wave_row()
    {
        NativeBattleModulePoolTask task = Task(0x872920).WriteDword(0x74, 0x1602658);
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.ObserveActor(NativeBattleEffect31Stack.Begin(), 0x26D1258, task, 0x872990, 0x8729D3, Matrix(0));
        Assert.Equal(new uint[] { 0xF1, 0, 0x26D1274, 0x1602658, 0x26D12EC }, Dwords(stack, -0xA4));
        stack = NativeBattleEffect31Stack.ObserveActor(stack, 0x26D15B8, Task(0x8731D0), 0x873240, 0x873326, Matrix(0));
        Assert.Equal(new uint[] { 0xF1, 0, 0x26D1274, 0x26D2DC0, 0x26D12EC }, Dwords(stack, -0xA4));
        Assert.Equal(0x873326u, stack.Observations[^1].Instruction);
        Assert.Equal(4, stack.Observations[^1].Length);
    }

    [Fact]
    public void Actual_draw_reestablishes_the_delayed_row_with_the_actual_walker_register()
    {
        NativeBattleStackBytes baseline = NativeBattleEffect31Stack.ObserveTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, 0x1D972C0, 0x196B068);
        NativeBattleStackBytes invalid = NativeBattleEffect31Stack.InvalidateCustom(baseline, 0x8712D0);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(invalid, 0x8729F0));
        Assert.Equal(20, invalid.Observations[^1].Length);
        Assert.Equal(NativeBattleStackOperation.UnknownRange, invalid.Observations[^1].Operation);
        NativeBattleStackBytes single = NativeBattleEffect31Stack.ObserveDraw(invalid, 0x2793DA4, 0x26D2990, 0x26D3A00);
        Assert.Equal(new uint[] { 0x2793DA4, 0x26D2990, 0x26D3A00, 0x86F4DD, 0x2793DA4 }, Dwords(single, -0xE0));
        NativeBattleStackBytes afterMulti = NativeBattleEffect31Stack.ObserveDraw(single, 0x2793CF0, 8, 0x26D3C30);
        Assert.Equal(8u, afterMulti.ReadUInt32(-0xDC));
        Assert.Equal(0x2793CF0u, afterMulti.ReadUInt32(-0xE0));
        Assert.Equal(18, NativeBattleEffect31Stack.ReadMatrix(afterMulti, 0x8729F0).Known.Take(18).Count(known => known));
        Assert.Equal(baseline.ReadUInt32(-0xFC), afterMulti.ReadUInt32(-0xFC));
    }

    [Fact]
    public void Snapshot_roundtrip_preserves_byte_knownness_widths_and_provenance()
    {
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.ObserveTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, 0x1D972C0, 0x196B068);
        stack = NativeBattleEffect31Stack.ObserveBounds(stack, 0x2001000, 2);
        stack = NativeBattleEffect31Stack.ObserveActor(stack, 0x26D10A8, Task(0x871F60), 0x872020, 0x8720FD, Matrix(5));
        stack = NativeBattleEffect31Stack.InvalidateCustom(stack, 0x86F500);
        string json = JsonSerializer.Serialize(stack);
        NativeBattleStackBytes restored = JsonSerializer.Deserialize<NativeBattleStackBytes>(json)!;
        NativeBattleEffect31Stack.Validate(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(NativeBattleEffect31Stack.ReadMatrix(stack, 0x872020).Bytes, NativeBattleEffect31Stack.ReadMatrix(restored, 0x872020).Bytes);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(restored, 0x8729F0));
        Assert.Equal(stack.Observations.Select(item => (item.Instruction, item.Offset, item.Length, item.Operation)), restored.Observations.Select(item => (item.Instruction, item.Offset, item.Length, item.Operation)));
    }

    [Fact]
    public void Unreached_one_bone_writer_and_other_callback_shapes_do_not_fabricate_bytes()
    {
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.Begin();
        Assert.Same(stack, NativeBattleEffect31Stack.ObserveBounds(stack, 0x2001000, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(stack, 0x8721D0));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ReadMatrix(stack, 0x1234));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect31Stack.Validate(NativeBattleStackBytes.Unknown("other", -0xFC, 0x6C)));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect31Stack.ObserveTarget(stack, 0, 0x1D972C0, 0x196B068));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Stack.ObserveActor(stack, 0x26D10A8, Task(0x872920), 0x872B80, 0x872BFB, Matrix(0)));
    }

    private static uint[] Dwords(NativeBattleStackBytes stack, int first)
    {
        return Enumerable.Range(0, 5).Select(index => stack.ReadUInt32(first + index * 4)).ToArray();
    }

    private static NativeBattlePackedMatrix32 Matrix(byte first)
    {
        return new(Enumerable.Range(0, 32).Select(index => unchecked((byte)(first + index))).ToImmutableArray(), Enumerable.Repeat(true, 32).ToImmutableArray());
    }

    private static NativeBattleModulePoolTask Task(uint callback)
    {
        return NativeBattleEffectTaskPool.Initialize(0x100, 0x200, 0x1B0, 1).Slots[0] with { Callback = callback };
    }
}
