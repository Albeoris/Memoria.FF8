using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelRendererGateTests
{
    [Fact]
    public void Authentic_cold_nonzero_gate_bypasses_query_without_software_owner()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess() with { Software = null };
        var entered = process.Enter5099D0(false);
        Assert.True(entered.Draw);
        Assert.Same(process, entered.Process);
        Assert.Equal(1u, entered.Process.RendererGateB8B9F8);
        Assert.Equal(new NativeBattleModelRendererRegisters(0, 0, 0, 0, 0), entered.Process.ModelRendererRegisters);
    }

    [Fact]
    public void Reset_with_no_delivery_skips_once_then_bypasses_query_after_JSON_restore()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Reset5099C0();
        var first = process.Enter5099D0(false);
        Assert.False(first.Draw);
        Assert.Equal(1u, first.Process.RendererGateB8B9F8);
        Assert.Same(process.ModelRendererRegisters, first.Process.ModelRendererRegisters);
        NativeBattlePrimitiveProcess restored = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(first.Process))!;
        Assert.True((restored with { Software = null }).Enter5099D0(false).Draw);
    }

    [Theory]
    [InlineData(false, 0u, false)]
    [InlineData(true, 0u, true)]
    [InlineData(false, 1u, true)]
    [InlineData(false, 0x80000000u, true)]
    [InlineData(true, 0x80000000u, true)]
    public void Reset_query_uses_full_canonical_debug_DWORD_and_configured_hardware(bool hardware, uint debug, bool draw)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Reset5099C0();
        process = process with { Software = process.Software! with { Environment = process.Software!.Environment with { Debug = debug } } };
        var result = process.Enter5099D0(hardware);
        Assert.Equal(draw, result.Draw);
        Assert.Equal(1u, result.Process.RendererGateB8B9F8);
        Assert.Same(process.Software, result.Process.Software);
    }

    [Fact]
    public void Unknown_import_gate_and_debug_are_not_replaced_with_cold_values()
    {
        NativeBattlePrimitiveProcess process = new(null, null, null, 0, null);
        Assert.Null(process.RendererGateB8B9F8);
        Assert.Null(process.ModelRendererRegisters);
        Assert.Throws<NotSupportedException>(() => process.Enter5099D0(true));
        Assert.Throws<NotSupportedException>(() => process.Reset5099C0().Enter5099D0(true));
        NativeBattlePrimitiveProcess known = NativeBattlePrimitiveProcess.FromColdProcess() with { RendererGateB8B9F8 = uint.MaxValue, Software = null };
        Assert.True(known.Enter5099D0(false).Draw);
    }

    [Fact]
    public void Debug_OR_and_completed_batch_share_one_owner_without_resetting_renderer_gate()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess();
        process = process with { Software = process.Software! with { Environment = process.Software!.Environment with { Debug = 0x80000000, OriginX = 12, OriginY = 34 } } };
        process = process.SetDebug45B5D0();
        Assert.Equal(0x80000001u, process.Software!.Environment.Debug);
        Assert.Equal(12, process.Software.Environment.OriginX);
        Assert.Equal(34, process.Software.Environment.OriginY);
        process = process with { Software = process.Software with { Environment = NativeSoftwareDrawingEnvironment.EndBatch45D080(process.Software.Environment) } };
        Assert.Equal(0u, process.Software.Environment.Debug);
        Assert.Equal(1u, process.RendererGateB8B9F8);
        Assert.False(process.Reset5099C0().Enter5099D0(false).Draw);
    }

    [Fact]
    public void Far_color_uses_unchecked_full_DWORD_shifts_and_model_BYTE_extraction()
    {
        NativeBattlePrimitiveProcess process = new(null, null, null, 0, null);
        process = process.PublishFarColor45DDA0(uint.MaxValue, 0x10000001, 0x87654321);
        Assert.Equal(new NativeBattleModelRendererRegisters(0xFFFFFFF0, 0x10, 0x76543210, null, null), process.ModelRendererRegisters);
        process = process.PublishModelColor(0xABFF8001);
        Assert.Equal(new NativeBattleModelRendererRegisters(16, 2048, 4080, null, null), process.ModelRendererRegisters);
        Assert.Null(process.RendererGateB8B9F8);
    }

    [Fact]
    public void Cursor_publication_preserves_far_controls_and_can_explicitly_mark_DTO_source_unknown()
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().PublishModelColor(0x123456).Publish50FDF0(0x20F1234, 0x20E4567);
        Assert.Equal(0x20F1234u, process.ModelRendererRegisters!.PacketCursor);
        Assert.Equal(0x20E4567u, process.ModelRendererRegisters.SourceCursor);
        uint? red = process.ModelRendererRegisters.FarRed;
        process = process.Publish50FDF0(0x100, null);
        Assert.Equal(0x100u, process.ModelRendererRegisters!.PacketCursor);
        Assert.Null(process.ModelRendererRegisters.SourceCursor);
        Assert.Equal(red, process.ModelRendererRegisters.FarRed);
    }
}
