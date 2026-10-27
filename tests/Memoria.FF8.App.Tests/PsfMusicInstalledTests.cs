using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class PsfMusicInstalledTests
{
    [Fact]
    public async Task OptionalDeviceConsumesTheStreamAndRespectsPause()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_PSF_DEVICE_SMOKE") == "1", "Enable the optional audio-device smoke check explicitly.");
        string archive = TestResources.FixturePath("junction-psf-music");
        JunctionModPackage imported = JunctionModImporter.Open(archive);
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, archive) { JunctionMetadata = imported.Manifest };
        ModFile[] files = imported.Files.Select(file => new ModFile(package.Id, ModFormat.Junction, file.RelativePath, 1, () =>
        {
            using Stream input = file.OpenRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            return bytes.ToArray();
        })).ToArray();
        ModFile selected = files.First(file => Path.GetExtension(file.RelativePath).Equals(".minipsf", StringComparison.OrdinalIgnoreCase));
        ExternalMusicSelection selection = Assert.IsType<ExternalMusicSelection>(new ExternalMusicResolver(new(1, [package], files)).Resolve("000-" + Path.GetFileNameWithoutExtension(selected.RelativePath) + ".sgt"));
        using PsfPcmStream stream = await Open(selection);
        using PcmStreamSource source = await PcmStreamSource.CreateAsync(stream, 0, TestContext.Current.CancellationToken, amplitude: 3.2);
        source.Volume(-2000);
        using SdlAudioOutput device = new();
        device.Mixer.Add(source);
        try
        {
            await Task.Delay(250, TestContext.Current.CancellationToken);
            device.ThrowIfFailed();
            source.ThrowIfFailed();
            Assert.True(source.PositionFrames > 0);
            source.Pause(true);
            await Task.Delay(20, TestContext.Current.CancellationToken);
            long paused = source.PositionFrames;
            await Task.Delay(50, TestContext.Current.CancellationToken);
            Assert.Equal(paused, source.PositionFrames);
            source.Pause(false);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.True(source.PositionFrames > paused);
            device.ThrowIfFailed();
        }
        finally
        {
            device.Mixer.Remove(source);
        }
    }

    [Fact]
    public async Task OptionalPackageStreamsEveryTrackAndPlaysThroughAnIsolatedHost()
    {
        string archive = TestResources.FixturePath("junction-psf-music");
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "memoria-psf-package-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string copy = Path.Combine(mods, "Junction", "music.iroj");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        byte[] hash = SHA256.HashData(File.ReadAllBytes(archive));
        File.Copy(archive, copy);
        List<object> evidence = [];
        try
        {
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "application"), watch: false, modsDirectory: mods);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Empty(host.Mods.Diagnostics);
            ExternalMusicResolver resolver = new(host.Mods.Catalog);
            ModFile[] tracks = host.Mods.Catalog.Files.Where(file => Path.GetExtension(file.RelativePath).Equals(".minipsf", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file.RelativePath).Equals(".psf", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.NotEmpty(tracks);
            foreach (ModFile file in tracks)
            {
                string name = Path.GetFileNameWithoutExtension(file.RelativePath);
                ExternalMusicSelection selection = Assert.IsType<ExternalMusicSelection>(resolver.Resolve("000-" + name + ".sgt"));
                Assert.Equal(file.RelativePath, selection.File.RelativePath);
                using PsfPcmStream stream = await Open(selection);
                byte[] pcm = new byte[4096 * 4];
                bool nonzero = false;
                long frames = 0;
                while (!nonzero && frames < 44100 * 15)
                {
                    int count = await stream.ReadAsync(pcm, TestContext.Current.CancellationToken);
                    Assert.True(count > 0);
                    frames += count / 4;
                    nonzero = pcm.AsSpan(0, count).ContainsAnyExcept((byte)0);
                }
                Assert.True(nonzero, "Selected PSF produced no nonzero samples within the bounded initialization check: " + file.RelativePath);
                evidence.Add(new { file.RelativePath, Frames = frames, PcmSha256 = Convert.ToHexString(SHA256.HashData(pcm)), stream.Metadata.Length, stream.Metadata.Fade });
            }

            foreach ((string filename, bool noIntro) in new (string, bool)[] { ("joriku2.sgt", false), ("joriku.sgt", true), ("gargarde.sgt", true), ("hikutei.sgt", true), ("gafly.sgt", true), ("julia.sgt", true), ("jail.sgt", true) })
            {
                ExternalMusicSelection? selection = resolver.Resolve(filename, noIntro);
                if (selection is null)
                {
                    continue;
                }
                using PsfPcmStream stream = await Open(selection);
                using PcmStreamSource source = await PcmStreamSource.CreateAsync(stream, selection.OffsetSeconds, TestContext.Current.CancellationToken);
                Assert.Equal((long)(selection.OffsetSeconds * 44100), source.PositionFrames);
                Assert.True(source.HasBufferedFrame);
                source.ThrowIfFailed();
                evidence.Add(new { Filename = filename, NoIntro = noIntro, selection.File.RelativePath, selection.OffsetSeconds, source.PositionFrames });
            }

            NativeMusicCatalog native = new(host.Resources);
            int trackId = Enumerable.Range(0, 117).First(id =>
            {
                try
                {
                    return resolver.Resolve(native.MusicFileResolved(id)) is not null;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
            });
            PcmMixer mixer = new();
            int devices = 0;
            using FieldAudioPresentation output = new(game.GameDirectory, "", () => host.Mods.Catalog)
            {
                OutputFactory = () =>
                {
                    devices++;
                    return new PcmAudioOutput(mixer);
                }
            };
            output.PlayMusic(trackId, 0, 127, -1, identityResolved: true);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (devices == 0)
            {
                output.Update();
                Assert.Null(output.Error);
                await Task.Delay(5, timeout.Token);
            }
            Assert.Equal(trackId, output.PrimaryMusicId);
            short[] block = new short[4410 * 2];
            bool audible = false;
            for (int index = 0; index < 150 && !audible; index++)
            {
                mixer.Render(block);
                audible = block.Any(sample => sample != 0);
                output.Update();
                Assert.Null(output.Error);
                await Task.Delay(5, timeout.Token);
            }
            Assert.True(audible);
            output.SetFocusPaused(true);
            mixer.Render(block);
            Assert.All(block, sample => Assert.Equal(0, sample));
            output.SetFocusPaused(false);
            output.StopAll();
            mixer.Render(block);
            Assert.All(block, sample => Assert.Equal(0, sample));
            Assert.Null(output.Error);

            const uint concertMask = 0x7FFFFE;
            if (NativeConcertSelection.Tracks(concertMask).All(filename => resolver.Resolve(filename) is not null))
            {
                NativeMusicPlayRequest concert = new(1, 634, 0, 0, 0, 0, "CHOICEMUSIC", true, 43, null, null, null, null, null, 0, GlobalSoundBackendStatus.Muted, ChoiceMask: concertMask)
                {
                    Playback = new(0, 43, false, true, true, null, null)
                };
                output.Execute(concert);
                audible = false;
                for (int index = 0; index < 300 && !audible; index++)
                {
                    output.Update();
                    Assert.Null(output.Error);
                    mixer.Render(block);
                    audible = block.Any(sample => sample != 0);
                    await Task.Delay(5, TestContext.Current.CancellationToken);
                }
                Assert.True(audible);
                Assert.Equal(43, output.PrimaryMusicId);
                evidence.Add(new { ConcertMask = concertMask, Stems = NativeConcertSelection.Tracks(concertMask).Count, Nonzero = audible });
                output.StopAll();
                mixer.Render(block);
                Assert.All(block, sample => Assert.Equal(0, sample));
                Assert.Null(output.Error);
            }
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(archive)));
            Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(copy)));
            string? destination = Environment.GetEnvironmentVariable("MEMORIA_FF8_PSF_VALIDATION_OUTPUT");
            if (!string.IsNullOrWhiteSpace(destination))
            {
                File.WriteAllText(destination, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Task<PsfPcmStream> Open(ExternalMusicSelection selection)
    {
        Assert.NotNull(selection.BiosFile);
        return PsfDecoder.OpenAsync(selection.File.RelativePath, selection.File.ReadBytes(), (requester, library) => selection.ResolveDependency(requester, library).ReadBytes(), selection.BiosFile.ReadBytes(), repeat: true, TestContext.Current.CancellationToken);
    }
}
