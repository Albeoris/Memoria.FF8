using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class CatalogNativeMediaTests
{
    [Fact]
    public async Task DirectMusicPreviewRendersTheEntireAuthoredSegment()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using var resources = GameDataSourceFactory.Open(TestResources.GameDirectory);
        if (resources is not IGameFileSource files)
        {
            Assert.Fail("The installed game has no file source.");
            return;
        }

        string segment = files.ResolveFile("music/dmusic/001s-win.sgt");
        string bank = files.ResolveFile("music/dmusic/ff8.dls");
        MusicSegment music = new(await File.ReadAllBytesAsync(segment, token));
        double seconds = music.SecondsAt(music.Length) - music.SecondsAt(music.PlayStart);
        uint expected = checked((uint)Math.Ceiling(seconds * 44100) * 4);
        using MemoryStream wave = new();
        ModPreviewMediaInput? input = CatalogNativeMedia.WithNativeDecoder(new("Victory", segment) { InstrumentBank = bank });
        Assert.NotNull(input?.WriteWave);
        await input.WriteWave(wave, token);
        byte[] result = wave.GetBuffer();

        Assert.Equal(expected + 44L, wave.Length);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(40, 4)));
        Assert.Contains(result.AsSpan(44, checked((int)expected)).ToArray(), sample => sample != 0);
    }
}
