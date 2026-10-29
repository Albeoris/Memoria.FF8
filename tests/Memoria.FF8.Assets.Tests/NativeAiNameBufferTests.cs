using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAiNameBufferTests
{
    [Fact]
    public void Blitz_name_copies_the_actual_loaded_bytes_beyond_its_text_section()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(19, 7);
        Assert.Equal(Convert.FromHexString("47665F7065676C653B3B3B0000000003000C00500094003C"), ai.NameBuffers[1]);
        Assert.Equal("Charging...", ai.DisplayNames[1]);
        NativeBattleAiDefinition restored = System.Text.Json.JsonSerializer.Deserialize<NativeBattleAiDefinition>(System.Text.Json.JsonSerializer.Serialize(ai))!;
        Assert.Equal(ai.NameBuffers[1], restored.NameBuffers[1]);
        Assert.Equal(ai.DisplayNames[1], restored.DisplayNames[1]);
    }

    [Theory]
    [InlineData(24, 24)]
    [InlineData(23, 0)]
    public void Name_copy_uses_its_loaded_extent_without_padding_unknown_bytes(int available, int expected)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        byte[] bytes = new byte[40];
        bytes[4] = 0x47;
        System.Reflection.MethodInfo decode = typeof(BattleAssets).GetMethod("DecodeEnemyTextBank", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = ((string[] Texts, byte[][] NameBuffers, string[] DisplayNames))decode.Invoke(assets, [bytes, 0, 4, 8, 4 + available])!;
        Assert.Equal(expected, result.NameBuffers[0].Length);
        Assert.Equal(expected, result.NameBuffers[1].Length);
    }

    [Fact]
    public void Ifrit_message_prefix_preserves_the_native_buffer_without_decoding_a_cut_name_control_token()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);

        NativeBattleAiDefinition ai = assets.ReadEnemyAi(61, 6);

        Assert.Equal(24, ai.NameBuffers[5].Length);
        Assert.Equal((byte)0x0C, ai.NameBuffers[5][23]);
        Assert.Equal(FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetString(ai.NameBuffers[5].AsSpan(0, 23)), ai.DisplayNames[5]);
        Assert.Contains("{Name ", ai.Texts[5]);
        Assert.True(ai.Texts[5].Length > ai.DisplayNames[5].Length);
    }
}
