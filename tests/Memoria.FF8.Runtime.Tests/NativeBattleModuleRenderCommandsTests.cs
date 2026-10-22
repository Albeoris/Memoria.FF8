namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleRenderCommandsTests
{
    [Fact]
    public void Translation_clear_writes_only_three_control_DWORDs_and_keeps_unknown_rotation_and_data()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Translation = [111, -222, 333], Sxy = [null, 0x12345678, null], Mac0 = 0xABCDEF01 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = geometry, ScriptedColorRegisters = null };
        NativeBattleEffectMemory result = NativeBattleModuleRenderCommands.ClearTranslation704070(memory);
        Assert.Equal(new int?[] { 0, 0, 0 }, result.GeometryRegisters!.Translation);
        Assert.Equal(geometry.Rotation20, result.GeometryRegisters.Rotation20);
        Assert.Equal(geometry.Sxy, result.GeometryRegisters.Sxy);
        Assert.Equal(geometry.Mac0, result.GeometryRegisters.Mac0);
        Assert.Equal(geometry.V1, result.GeometryRegisters.V1);
        Assert.Equal(geometry.V2, result.GeometryRegisters.V2);
        Assert.Null(result.ScriptedColorRegisters);
        Assert.Same(memory.ScriptedLighting, result.ScriptedLighting);
        Assert.Same(memory.Arena, result.Arena);
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleRenderCommands.ClearTranslation704070(memory with { GeometryRegisters = null }));
    }

    [Theory]
    [InlineData(0, 292)]
    [InlineData(13, 4096)]
    [InlineData(14, 4096)]
    [InlineData(35, 4096)]
    [InlineData(36, 3804)]
    [InlineData(48, 293)]
    [InlineData(49, 0)]
    public void Original_300_envelope_uses_exact_rise_hold_fall_boundaries_and_signed_IDIV(int age, int expected)
    {
        Assert.Equal(expected, NativeBattleModuleRenderCommands.Factor700150(age, 14, 22, 14));
    }

    [Fact]
    public void Native_zero_divisors_and_IDIV_overflow_are_not_substituted_with_a_family_default()
    {
        Assert.Throws<DivideByZeroException>(() => NativeBattleModuleRenderCommands.Factor700150(-1, 0, 0, 14));
        Assert.Throws<DivideByZeroException>(() => NativeBattleModuleRenderCommands.Factor700150(0, 0, 0, 0));
        Assert.Throws<OverflowException>(() => NativeBattleModuleRenderCommands.Factor700150(-524289, -1, 0, 1));
        Assert.Equal(0, NativeBattleModuleRenderCommands.Factor700150(0, int.MinValue, int.MinValue, 1));
        Assert.Equal(0, NativeBattleRinoa89FadeDrawing.Factor6FC450(0, 0, 0, 0));
    }

    [Fact]
    public void Packet_writer_keeps_RGB_low_byte_wrap_full24_stores_and_native_fast_bucket()
    {
        NativeBattleGfGeometryBatch result = NativeBattleModuleRenderCommands.Emit700150(50, 64, 64, 0, 14, 22, 14);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Attempts);
        Assert.Equal(24, result.AllocationBytes);
        Assert.Equal(new uint[] { 0x05000000, 0x2A00FBFB, 0, 320, 0x00D80000, 0x00D80140 }, packet.Primitive!.Words);
        Assert.Equal(8, packet.Primitive.Bucket);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packet.Primitive.VertexPayload4);
        Assert.All(packet.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }
}
