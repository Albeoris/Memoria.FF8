using System.Buffers.Binary;
using System.Diagnostics;

namespace Memoria.FF8.App.Tests;

public sealed class DirectMusicPlaybackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PauseAfterLoopOrCompletionAllowsResumeAndNextBattle(bool finite)
    {
        string directory = Path.Combine(TestResources.GameDirectory, "Data", "Music", "dmusic");
        string battle = Path.Combine(directory, "005s-battle.sgt");
        byte[] bytes = File.ReadAllBytes(battle);
        int header = bytes.AsSpan().IndexOf("segh"u8) + 8;
        Assert.True(header >= 8);
        int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(header + 4, 4));
        int loopEnd = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(header + 16, 4));
        Assert.True(loopEnd > 64 && length >= loopEnd);
        string source = battle;
        if (finite)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(header, 4), 0);
            source = TestResources.ArtifactFile("finite-segment.sgt");
            File.WriteAllBytes(source, bytes);
        }
        using MusicWorkerOutput output = new(0, directory);
        output.Play(source, -10000, finite ? length - 1024 : loopEnd - 64);
        WaitPlaying(output, true);
        // Cross the original end as well as the loop boundary before focus is lost.
        Thread.Sleep(2000);
        Assert.Equal(!finite, output.IsPlaying);
        for (int pause = 0; pause < 2; pause++)
        {
            output.Pause();
            output.Resume();
            Assert.Equal(!finite, output.IsPlaying);
            Assert.Null(output.Error);
        }
        output.Play(battle, -10000);
        WaitPlaying(output, true);
        Assert.Null(output.Error);
        output.Stop();
        WaitPlaying(output, false);
    }

    private static void WaitPlaying(MusicWorkerOutput output, bool expected)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        while (output.IsPlaying != expected && deadline.Elapsed < TimeSpan.FromSeconds(3))
        {
            Thread.Sleep(20);
        }
        Assert.Equal(expected, output.IsPlaying);
    }
}
