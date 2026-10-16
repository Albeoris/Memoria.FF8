using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCommandSequenceTests
{
    [Fact]
    public void Executable_table_retains_the_original_thirty_nine_player_command_sequence_bytes()
    {
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);

        var sequences = assets.ReadNativeBattleCommandSequences();

        Assert.Equal(0x27, sequences.Length);
        Assert.Equal((byte)13, sequences[1]);
        Assert.Equal((byte)11, sequences[2]);
        Assert.Equal((byte)14, sequences[3]);
        Assert.Equal((byte)15, sequences[4]);
        Assert.Equal((byte)21, sequences[5]);
        Assert.Equal((byte)30, sequences[6]);
        Assert.Equal((byte)21, sequences[11]);
        Assert.Equal((byte)21, sequences[14]);
        Assert.Equal(sequences, assets.ReadNativeBattleCommandSequences());
    }
}
