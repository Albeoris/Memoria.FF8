using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ExternalMusicInstalledTests
{
    [Theory]
    [InlineData("battle.sgt", false)]
    [InlineData("joriku2.sgt", false)]
    [InlineData("gargarde.sgt", true)]
    [InlineData("hikutei.sgt", true)]
    [InlineData("gafly.sgt", true)]
    [InlineData("julia.sgt", true)]
    [InlineData("jail.sgt", true)]
    public void OptionalExternalTrackDecodesWithItsConfiguredOffsetAndVorbisLoop(string filename, bool noIntro)
    {
        string archive = TestResources.FixturePath("junction-external-music");
        string tools = MediaToolsInstallation.ResolveDirectory();
        Assert.SkipUnless(File.Exists(Path.Combine(tools, "ffmpeg.exe")), "Configure MEMORIA_FF8_MEDIA_TOOLS for external audio decoding.");
        JunctionModPackage imported = JunctionModImporter.Open(archive);
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, archive) { JunctionMetadata = imported.Manifest };
        ModFile[] files = imported.Files.Select(file => new ModFile(package.Id, ModFormat.Junction, file.RelativePath, 1, () =>
        {
            using MemoryStream buffer = new();
            file.CopyTo(buffer);
            return buffer.ToArray();
        })).ToArray();
        ModCatalogSnapshot catalog = new(1, [package], files);
        Assert.Empty(ExternalMusicResolver.Validate(catalog));
        ExternalMusicSelection selected = Assert.IsType<ExternalMusicSelection>(new ExternalMusicResolver(catalog).Resolve(filename, noIntro));
        byte[] encoded = selected.File.ReadBytes();
        PcmAudio pcm = new(PcmAudioDecoder.Decode(tools, "pipe:0", encoded, 44100, 2), 44100, 2);
        ExternalMusicLoop loop = ExternalMusicLoop.Read(encoded, pcm);
        int offset = ExternalMusicPcm.OffsetBytes(pcm, selected.OffsetSeconds);
        Assert.InRange(offset, 0, loop.EndByte - 1);
        Assert.InRange(loop.StartByte, 0, loop.EndByte - 1);
        Assert.InRange(loop.EndByte, 1, pcm.Bytes.Length);
    }
}
