using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleFlashPresentationTests
{
    [Theory]
    [InlineData(281)]
    [InlineData(285)]
    public void Explicit_source_capture_retains_actual_module_child_birth_and_native_flash_bytes_state_and_lifetime(ushort effect)
    {
        NativeBattlePilotVisualIdentity source = new(effect, 0x1234, 2, 3, 0x5712) { ResourceGeneration = 4 };
        NativeBattleModuleState native = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, 128);
        string initialNative = JsonSerializer.Serialize(native);
        NativeBattleModuleState captured = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, 128, pilotIdentity: source);
        Assert.Equal(initialNative, JsonSerializer.Serialize(captured));
        NativeBattleModuleStep nativeFirst = native.Advance(new(0));
        string firstState = JsonSerializer.Serialize(nativeFirst.State);
        string firstDraws = JsonSerializer.Serialize(nativeFirst.Draws);
        NativeBattleModuleStep capturedFirst = captured.Advance(new(0));
        Assert.Equal(firstState, JsonSerializer.Serialize(capturedFirst.State));
        Assert.Equal(firstDraws, JsonSerializer.Serialize(capturedFirst.Draws));
        NativeBattleModuleDraw draw = Assert.Single(capturedFirst.Draws);
        NativeBattlePilotVisualIdentity child = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
        Assert.Equal(effect, child.Effect);
        Assert.Equal(NativeBattleModuleState.DescriptorAddress, child.Bank);
        Assert.Equal(0, child.Slot);
        Assert.True(child.Generation > 0);
        Assert.Equal(child, capturedFirst.State.Slots[0].PilotIdentity);
        NativeBattleModuleState copied = capturedFirst.State.WritePhysicalModuleBytes(NativeBattleModuleState.BankAddress + 12, capturedFirst.State.ReadPhysicalModuleBytes(NativeBattleModuleState.BankAddress + 12, 2));
        Assert.Equal(child, copied.Slots[0].PilotIdentity);
        Assert.Equal(firstState, JsonSerializer.Serialize(copied));
        NativeBattleModulePacketBatch nativeBatch = NativeBattleFlashPackets.Emit(Assert.Single(nativeFirst.Draws));
        string nativePackets = JsonSerializer.Serialize(nativeBatch);
        NativeBattleModulePacketBatch capturedBatch = NativeBattleFlashPackets.Emit(draw);
        Assert.Equal(nativePackets, JsonSerializer.Serialize(capturedBatch));
        NativeBattleGeometryPrimitive primitive = Assert.Single(capturedBatch.Primitives).Primitive;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(child, geometry.Identity);
        Assert.True(geometry.ScreenSpace);
        Assert.False(geometry.Continuous);
        Assert.Equal(8, geometry.ScreenScale);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, geometry.SourceDepths);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
        Assert.Same(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { D = System.Numerics.Vector3.Zero }, 0.5f));
        NativeBattleModuleStep nativeRelease = nativeFirst.State.Advance(new(0));
        NativeBattleModuleStep capturedRelease = capturedFirst.State.Advance(new(0));
        Assert.Equal(JsonSerializer.Serialize(nativeRelease), JsonSerializer.Serialize(capturedRelease));
        Assert.Empty(capturedRelease.Draws);
        Assert.Null(capturedRelease.State.Head);
        NativeBattleModuleState recycled = capturedRelease.State.AppendFlash(0, 1, 0, 128, pilotIdentity: source);
        Assert.NotEqual(child.Generation, recycled.Slots[0].PilotIdentity!.Value.Generation);
        NativeBattleModuleState defaultReuse = capturedRelease.State.AppendFlash(0, 1, 0, 128);
        Assert.Null(defaultReuse.Slots[0].PilotIdentity);
        Assert.Null(Assert.Single(defaultReuse.Advance(new(0)).Draws).PilotIdentity);
        NativeBattleModuleStep paused = captured.Advance(new(1));
        Assert.Empty(paused.Draws);
        Assert.Equal(JsonSerializer.Serialize(native.Advance(new(1)).State), JsonSerializer.Serialize(paused.State));
        Assert.Equal(child, paused.State.Slots[0].PilotIdentity);
    }
}
