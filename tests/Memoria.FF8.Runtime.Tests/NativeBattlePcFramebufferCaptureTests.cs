using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcFramebufferCaptureTests
{
    [Fact]
    public void Capture_uses_padded_WORD_rows_and_current_viewport_then_freezes_the_texture()
    {
        const int pitch = 520 * 2;
        byte[] surface = new byte[pitch * 520];
        for (int y = 0; y < 520; y++)
        {
            for (int x = 0; x < 520; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(surface.AsSpan(y * pitch + x * 2, 2), unchecked((ushort)(y * 521 + x)));
            }
        }
        NativeBattlePcFramebufferCaptureState cold = NativeBattlePcFramebufferCaptureState.FromColdProcess();
        NativeBattlePcFramebufferCaptureState captured = cold.Capture56D530(surface, pitch, new(3, 7, 512, 512), true);
        ImmutableArray<ushort> pixels = captured.RequirePixels();
        Assert.Equal(unchecked((ushort)(7 * 521 + 3)), pixels[0]);
        Assert.Equal(unchecked((ushort)(7 * 521 + 513)), pixels[255]);
        Assert.Equal(unchecked((ushort)(517 * 521 + 3)), pixels[255 * 256]);
        Assert.Equal(unchecked((ushort)(517 * 521 + 513)), pixels[^1]);
        Array.Clear(surface);
        Assert.NotEqual((ushort)0, captured.RequirePixels()[0]);
        Assert.Null(cold.Pixels);
    }

    [Fact]
    public void Signed_fixed_point_steps_reverse_axes_and_DWORD_shift_overflow_is_preserved()
    {
        byte[] surface = new byte[256 * 256 * 2];
        for (int index = 0; index < 256 * 256; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(surface.AsSpan(index * 2, 2), unchecked((ushort)index));
        }
        NativeBattlePcFramebufferCaptureState cold = NativeBattlePcFramebufferCaptureState.FromColdProcess();
        ImmutableArray<ushort> reversed = cold.Capture56D530(surface, 512, new(255, 255, -256, -256), true).RequirePixels();
        Assert.Equal(ushort.MaxValue, reversed[0]);
        Assert.Equal((ushort)0, reversed[^1]);
        ImmutableArray<ushort> wrapped = cold.Capture56D530([0x34, 0x12], 2, new(65536, 65536, 65536, 65536), true).RequirePixels();
        Assert.All(wrapped, pixel => Assert.Equal((ushort)0x1234, pixel));
    }

    [Fact]
    public void Lock_failure_retains_texture_and_bounds_failure_does_not_publish_partial_capture()
    {
        NativeBattlePcFramebufferCaptureState cold = NativeBattlePcFramebufferCaptureState.FromColdProcess();
        Assert.Same(cold, cold.Capture56D530([], 0, null!, false));
        Assert.Throws<NotSupportedException>(() => cold.RequirePixels());
        NativeBattlePcFramebufferCaptureState retained = cold.Capture56D530([0xEF, 0xBE], 2, new(0, 0, 0, 0), true);
        Assert.Same(retained, retained.Capture56D530([], int.MinValue, null!, false));
        Assert.Throws<InvalidDataException>(() => retained.Capture56D530([0, 0], 2, new(0, 0, 256, 256), true));
        Assert.Equal((ushort)0xBEEF, retained.RequirePixels()[^1]);
        NativeBattlePcFramebufferCaptureState restored = JsonSerializer.Deserialize<NativeBattlePcFramebufferCaptureState>(JsonSerializer.Serialize(retained))!;
        Assert.Equal(retained.RequirePixels().ToArray(), restored.RequirePixels().ToArray());
    }

    [Fact]
    public void Driver_selection_prefers_inverse_source_alpha_and_preserves_explicit_overrides()
    {
        NativeBattlePcFramebufferCaptureProfile primary = NativeBattlePcFramebufferCaptureProfile.PrimaryRgba8();
        NativeBattlePcFramebufferCaptureMaterial material = primary.ResolveMaterial();
        Assert.Equal(new NativeBattlePcFramebufferBlend(5, 6), material.Blend);
        Assert.Equal((byte)128, material.ImageMaterialAlpha);
        Assert.Equal(2u, material.TextureMapBlend);
        Assert.False(material.TextureHasAlpha);
        Assert.Equal(new NativeBattlePcFramebufferBlend(5, 5), (primary with { DestinationBlendCapabilities = 0x10 }).ResolveMaterial().Blend);
        NativeBattlePcFramebufferCaptureMaterial overridden = (primary with { OverrideBlend = true, BlendOverride = new(2, 1), OverrideTextureMapBlend = true }).ResolveMaterial();
        Assert.Equal(new NativeBattlePcFramebufferBlend(2, 1), overridden.Blend);
        Assert.Equal(1u, overridden.TextureMapBlend);
        Assert.Throws<NotSupportedException>(() => (primary with { SourceBlendCapabilities = 0 }).ResolveMaterial());
        Assert.Throws<NotSupportedException>(() => (primary with { DestinationBlendCapabilities = 0 }).ResolveMaterial());
        Assert.Throws<NotSupportedException>(() => (primary with { LegacyCapabilities350 = 0 }).ResolveMaterial());
        Assert.Throws<InvalidDataException>(() => (primary with { OverrideBlend = true }).ResolveMaterial());
        Assert.Throws<NotSupportedException>(() => overridden.ModulateSourceArgb(0xFF112233, 0x55FFFFFF));
    }

    [Fact]
    public void Legacy_MODULATE_uses_diffuse_alpha_only_when_the_texture_has_no_alpha_channel()
    {
        NativeBattlePcFramebufferCaptureProfile primary = NativeBattlePcFramebufferCaptureProfile.PrimaryRgba8();
        Assert.Equal(0x55FFFFFFu, primary.ResolveMaterial().ModulateSourceArgb(0xFFFFFFFF, 0x55FFFFFF));
        NativeBattlePcFramebufferCaptureProfile alpha = primary with { Format = new(0x7C00, 0x03E0, 0x001F, 0x8000) };
        Assert.Equal(0xFFFFFFFFu, alpha.ResolveMaterial().ModulateSourceArgb(alpha.Format.DecodeArgb(0xFFFF), 0x55FFFFFF));
        Assert.Equal(0x00FFFFFFu, alpha.ResolveMaterial().ModulateSourceArgb(alpha.Format.DecodeArgb(0x7FFF), 0x55FFFFFF));
        Assert.Equal(0x55402010u, primary.ResolveMaterial().ModulateSourceArgb(0xFF804020, 0x55808080));
        Assert.Equal((ushort)0x7FFF, primary.Format.EncodeArgb418AC4(0xFFFFFFFF));
        Assert.Equal(0xFFFF0000u, primary.Format.DecodeArgb(0x7C00));
        byte[] encoded = NativeBattlePcFramebufferCaptureState.EncodeArgbSurface([0xFFFF0000, 0xFF00FF00, 0xFF0000FF], primary);
        Assert.Equal(new byte[] { 0, 0x7C, 0xE0, 3, 0x1F, 0 }, encoded);
        Assert.Throws<InvalidDataException>(() => new NativeBattlePcFramebufferFormat(0x7C00, 0x03E0, 0x001F, 0x4000).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattlePcFramebufferFormat(0x7400, 0x03E0, 0x001F, 0).Validate());
    }
}
