using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89SceneDrawingTests
{
    [Theory]
    [InlineData(0, 0, 1, 7, 4096)]
    [InlineData(1, 0, 1, 7, 3511)]
    [InlineData(7, 0, 1, 7, 0)]
    [InlineData(0, 30, 1, 31, 136)]
    [InlineData(29, 30, 1, 31, 4096)]
    [InlineData(30, 30, 1, 31, 4096)]
    [InlineData(61, 30, 1, 31, 0)]
    public void Fade_uses_original_signed_division_and_branch_boundaries(int age, int fadeIn, int hold, int fadeOut, int expected)
    {
        Assert.Equal(expected, NativeBattleRinoa89FadeDrawing.Factor6FC450(age, fadeIn, hold, fadeOut));
    }

    [Fact]
    public void Fade_emits_actual_fullscreen_F4_with_semitransparent_command_and_bucket_eight()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 128, 64, 2048);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(24, batch.AllocationBytes);
        Assert.Equal(8, primitive.Bucket);
        Assert.Equal(new uint[] { 0x05000000, 0x2A20407F, 0, 320, 216u << 16, 320 | 216u << 16 }, primitive.Words);
    }

    [Fact]
    public void Star_colour_retains_native_low_byte_wrap_after_separately_rounded_RGB_products()
    {
        Assert.Equal(0x00563412u, NativeBattleRinoa89StarDrawing.BlendColor6FCD50(0xAA563412, 0xFFFFFFFF, 0));
        Assert.Equal(0u, NativeBattleRinoa89StarDrawing.BlendColor6FCD50(0xFFFFFFFF, 0xFFFFFFFF, 128));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Native_45C8E0_producers_insert_four_zero_payload_DWORDs_into_the_actual_COMMON_bank(int producer)
    {
        NativeBattleGfGeometryBatch batch;
        if (producer == 0)
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(24228 + 6, BitConverter.GetBytes((ushort)16));
            batch = NativeBattleRinoa89ShardDrawing.Draw6FCFD0(arena);
        }
        else if (producer == 1)
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(31748 + 4, BitConverter.GetBytes((short)4096));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(0x14AF498, new byte[128]));
            NativeBattleRinoa89Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, null!, staging, NativeCrtRandomState.FromColdProcess());
            NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
            batch = NativeBattleRinoa89StarDrawing.Draw6FCD50(frame, identity, new(identity, 256, 160, 108), 0, 0, 0, 0, 65536).Packets;
        }
        else
        {
            batch = NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 255, 255, 4096);
        }
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleCommonPacketWrite result = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), batch, 1);
        NativeBattleLogicalProxy proxy = Assert.Single(result.Ordering.Proxies);
        Assert.Equal(1, proxy.Flags20);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, proxy.VertexPayload4);
        Assert.Equal(process.ByteCursor + batch.AllocationBytes, result.Process.ByteCursor);
        Assert.Equal(batch.Attempts.Count(attempt => attempt.Primitive is not null), result.Ordering.Proxies.Length);
    }
}
