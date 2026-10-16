using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAiNameBufferTests
{
    [Fact]
    public void Ifrit_message_prefix_preserves_the_native_buffer_without_decoding_a_cut_name_control_token()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);

        NativeBattleAiDefinition ai = assets.ReadEnemyAi(61, 6);

        Assert.Equal(24, ai.NameBuffers[5].Length);
        Assert.Equal((byte)0x0C, ai.NameBuffers[5][23]);
        Assert.Equal(FF8Encoding.CreateEuropean().GetString(ai.NameBuffers[5].AsSpan(0, 23)), ai.DisplayNames[5]);
        Assert.Contains("{Name ", ai.Texts[5]);
        Assert.True(ai.Texts[5].Length > ai.DisplayNames[5].Length);
    }
}
