using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveTextureWindowTests
{
    [Theory]
    [InlineData(0u, 0, 0, 256, 256)]
    [InlineData(0x41310u, 32, 64, 128, 64)]
    [InlineData(0xFFFFFFu, 248, 248, 8, 8)]
    [InlineData(0x400u, 8, 0, 248, 256)]
    public void Decode45BC60_preserves_zero_size_expansion_and_clamps_the_cropped_rectangle(uint bits, int x, int y, int width, int height)
    {
        Assert.Equal(new(x, y, width, height), NativeBattlePrimitiveTextureWindow.Decode45BC60(bits));
    }

    [Fact]
    public void Executed_E2_replaces_only_its_24_bit_retained_window_and_E1_preserves_it()
    {
        NativeBattlePrimitiveDrawMode before = new(123);
        Assert.Null(before.TextureWindowBits);
        NativeBattlePrimitiveDrawMode window = before.ExecuteWords(0xE2, [0xE2041310]);
        Assert.Equal((uint?)0x41310, window.TextureWindowBits);
        Assert.Equal((ushort?)123, window.TexturePage);
        NativeBattlePrimitiveDrawMode page = window.ExecuteWords(0xE1, [0xE1000620]);
        Assert.Equal((uint?)0x41310, page.TextureWindowBits);
        Assert.Equal((ushort?)0x620, page.TexturePage);
        Assert.Equal(window, JsonSerializer.Deserialize<NativeBattlePrimitiveDrawMode>(JsonSerializer.Serialize(window)));
        Assert.Throws<InvalidDataException>(() => before.ExecuteWords(0xE2, []));
        Assert.Throws<InvalidDataException>(() => before.ExecuteWords(0xE2, [0xE2000000, 0]));
    }

    [Fact]
    public void Only_explicit_cold_creation_knows_zero_and_arena_resets_preserve_the_live_window()
    {
        NativeBattlePrimitiveProcess cold = NativeBattlePrimitiveProcess.FromColdProcess();
        Assert.Equal((uint?)0, cold.TextureWindowBits);
        NativeBattlePrimitiveProcess live = cold with { TextureWindowBits = 0x41310 };
        Assert.Equal((uint?)0x41310, live.Initialize500400().Reset500900(0x80).TextureWindowBits);
        var old = JsonSerializer.SerializeToNode(cold)!.AsObject();
        Assert.True(old.Remove(nameof(NativeBattlePrimitiveProcess.TextureWindowBits)));
        Assert.Null(JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(old)!.TextureWindowBits);
        Assert.Throws<InvalidDataException>(() => (cold with { TextureWindowBits = 0x1000000 }).Validate());
    }
}
