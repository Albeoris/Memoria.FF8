using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8TargetSetupTests
{
    [Theory]
    [InlineData('X')]
    [InlineData('Y')]
    [InlineData('Z')]
    public void Axis_rotation_preserves_input_translation_padding_and_physical_released_bytes(char axis)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770() with { X = 101, Y = 202, Z = 303 };
        NativeBattlePackedMatrix32 input = NativeBattlePackedMatrix32.FromMatrix(identity, 0xBEEF);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Translation = [11, 22, 33], Mac0 = 123, Otz = 456 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = geometry, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, 36).ToArray());
        NativeBattleStatefulMatrixRotationStep result = NativeBattleStatefulMatrixRotation.Rotate(memory, staging, input, axis, 1024);
        short[] expected = axis switch
        {
            'X' => [4096, 0, 0, 0, 0, -4096, 0, 4096, 0],
            'Y' => [0, 0, 4096, 0, 4096, 0, -4096, 0, 0],
            _ => [0, -4096, 0, 4096, 0, 0, 0, 0, 4096]
        };
        Assert.Equal(expected, result.Matrix.ReadMatrix().Rotation.ToArray());
        Assert.Equal(input.Bytes.AsSpan().Slice(18).ToArray(), result.Matrix.Bytes.AsSpan().Slice(18).ToArray());
        Assert.Equal(input.Bytes.AsSpan().Slice(0, 20).ToArray(), result.Memory.GeometryRegisters!.Rotation20.Select(value => value!.Value).ToArray());
        Assert.Equal(new int?[] { 11, 22, 33 }, result.Memory.GeometryRegisters.Translation.ToArray());
        Assert.Equal(123u, result.Memory.GeometryRegisters.Mac0);
        Assert.Equal(456u, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 14), result.Staging.Read(NativeBattleTemporaryMemory.Address + 18, 14));
        Assert.Equal((short)4096, BinaryPrimitives.ReadInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 32, 2)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Address + 34, 2)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Rotation_preserves_byte_partial_padding_and_unknown_temporary_middle()
    {
        NativeBattlePackedMatrix32 input = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0xBEEF);
        input = input with { Known = input.Known.SetItem(19, false) };
        NativeBattleStatefulMatrixRotationStep result = NativeBattleStatefulMatrixRotation.Rotate(NativeBattleEffectMemory.FromColdProcess(), NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), input, 'Y', 0);
        Assert.Equal((byte)0xEF, result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.True(result.Matrix.Known[18]);
        Assert.False(result.Matrix.Known[19]);
        var temporary = NativeBattleTemporaryMemory.ReadView(result.Staging, NativeBattleTemporaryMemory.Address, 36).ReadSnapshot(0, 36);
        Assert.All(temporary.Known.Skip(18).Take(14), known => Assert.False(known));
        Assert.All(temporary.Known.Take(18), known => Assert.True(known));
        Assert.All(temporary.Known.Skip(32), known => Assert.True(known));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatefulMatrixRotation.Rotate(result.Memory, result.Staging, input with { Known = input.Known.SetItem(0, false) }, 'Y', 0));
    }

    [Fact]
    public void Target_setup_uses_actual_position_WORDs_clears_shared_state_and_publishes_global_light_without_resetting_GTE_translation()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame before = fixture.Frame with { State = fixture.Frame.State.Write(NativeBattleDoom8State.SharedAddress, Enumerable.Repeat((byte)0xCC, 84).ToArray()), Memory = fixture.Frame.Memory with { EffectLight1D99AB0 = Enumerable.Repeat((byte)0xC3, 32).ToImmutableArray() } };
        NativeBattleDoom8Frame after = NativeBattleDoom8TargetSetup.Initialize8C52C0(before, fixture.Address, actor =>
        {
            Assert.Equal(3, actor);
            return new(actor, 0) { Transform = new(new(900, 800, 700), new(0, -300, -100), -1234, 0, 4096) };
        });
        Assert.Equal(new byte[8], after.State.Read(NativeBattleDoom8State.SharedAddress, 8));
        Assert.Equal(unchecked((ushort)-300), after.State.SharedWord(10));
        Assert.Equal(unchecked((ushort)-100), after.State.SharedWord(12));
        Assert.Equal(unchecked((ushort)-1234), after.State.SharedWord(14));
        Assert.Equal((ushort)0, after.State.SharedWord(0x4A));
        Assert.Equal((ushort)0, after.State.SharedWord(0x40));
        NativeBattlePackedMatrix32 light = after.Memory.CaptureEffectLight1D99AB0();
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation.ToArray(), light.ReadMatrix().Rotation.ToArray());
        Assert.Equal(0, light.ReadMatrix().X);
        Assert.Equal(0, light.ReadMatrix().Y);
        Assert.Equal(0, light.ReadMatrix().Z);
        Assert.Equal((byte)0xC3, light.Bytes[18]);
        Assert.Equal((byte)0xC3, light.Bytes[19]);
        Assert.All(light.Known, known => Assert.True(known));
        Assert.Equal(before.Memory.GeometryRegisters!.Translation.ToArray(), after.Memory.GeometryRegisters!.Translation.ToArray());
        Assert.Equal(SnapshotJson(after), SnapshotJson(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(SnapshotJson(after)))));
    }

    [Fact]
    public void Unknown_light_padding_stays_unknown_after_real_identity_and_rotation_writes()
    {
        var fixture = Fixture();
        NativeBattleDoom8Frame after = NativeBattleDoom8TargetSetup.Initialize8C52C0(fixture.Frame, fixture.Address, actor => new(actor, 0) { Transform = new(new(0, 0, 0), new(100, 0, 0), 0, 0, 4096) });
        NativeBattlePackedMatrix32 light = after.Memory.CaptureEffectLight1D99AB0();
        Assert.False(light.Known[18]);
        Assert.False(light.Known[19]);
        Assert.All(light.Known.Take(18), known => Assert.True(known));
        Assert.All(light.Known.Skip(20), known => Assert.True(known));
        Assert.NotEqual(SnapshotJson(light.ReadMatrix().Rotation), SnapshotJson(NativeBattleCuragaMatrices.Identity8DD770().Rotation));
        Assert.Null(after.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(1u, after.Random.Seed);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8TargetSetup.Initialize8C52C0(fixture.Frame with { Staging = null }, fixture.Address, actor => new(actor, 0) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096) }));
    }

    [Fact]
    public void Legacy_full_light_bytes_remain_known_while_malformed_knownness_fails()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { EffectLight1D99AB0 = new byte[32].ToImmutableArray() };
        Assert.All(memory.CaptureEffectLight1D99AB0().Known, known => Assert.True(known));
        Assert.All((memory with { EffectLight1D99AB0 = null }).CaptureEffectLight1D99AB0().Known, known => Assert.False(known));
        Assert.Throws<InvalidDataException>(() => (memory with { EffectLightKnown1D99AB0 = [] }).CaptureEffectLight1D99AB0());
        Assert.Throws<InvalidDataException>(() => (memory with { EffectLight1D99AB0 = null, EffectLightKnown1D99AB0 = new bool[32].ToImmutableArray() }).CaptureEffectLight1D99AB0());
    }

    private static (NativeBattleDoom8Frame Frame, uint Address) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x8C4B30, 100);
        NativeBattleEffectHierarchyAppend target = root.Hierarchy.Append8DC540(1, 0x8C4D90, 88, root.Address, (_, _, _) => new(0, 3));
        NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = target.Hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { EffectLight1D99AB0 = null, GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Translation = [11, 22, 33] } };
        NativeBattleDoom8Frame frame = new(memory, effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray())) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return (frame, target.Address);
    }

    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
}
