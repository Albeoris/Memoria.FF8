using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldAudioPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FieldDepartureDisposesPrimaryAndPendingOrPlayingLoop(bool startLoop)
    {
        string directory = InstalledGame();
        using FieldAudioPresentation presentation = new(GetDesktopWindow(), directory, "");
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory, 2253))!;
        Assert.True(resource.LoopPolicy.LoopStartPcmByte > 0);
        presentation.Execute(new GlobalSoundRequest(10103, 1, 1, 1, 128, 64, 0, 100, resource));
        Assert.Null(presentation.Error);
        Assert.Equal(1, presentation.SoundVoiceCount);
        var voices = (System.Collections.IDictionary)typeof(FieldAudioPresentation).GetField("voices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(presentation)!;
        object voice = voices[1]!;
        DirectSoundOutput.Buffer primary = (DirectSoundOutput.Buffer)voice.GetType().GetProperty("Primary")!.GetValue(voice)!;
        DirectSoundOutput.Buffer loop = (DirectSoundOutput.Buffer)voice.GetType().GetProperty("Loop")!.GetValue(voice)!;
        if (startLoop)
        {
            Thread.Sleep(resource.LoopPolicy.LoopTransitionDelayMilliseconds!.Value + 30);
            presentation.Update();
            Assert.True((bool)voice.GetType().GetProperty("LoopStarted")!.GetValue(voice)!);
        }
        presentation.Execute(new NativeFieldSoundDeparture(141, [1]));
        presentation.Update();
        Assert.Null(presentation.Error);
        Assert.Equal(0, presentation.SoundVoiceCount);
        var handle = typeof(DirectSoundOutput.Buffer).GetField("handle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal((nint)0, (nint)handle.GetValue(primary)!);
        Assert.Equal((nint)0, (nint)handle.GetValue(loop)!);
    }

    [Fact]
    public void ConcertSelectionRefusesOrdinaryTrackRenderingBeforeOpeningDevices()
    {
        FieldAudioPresentation.RequireOrdinaryMusicOutput(null);
        Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.RequireOrdinaryMusicOutput(4080));
    }

    [Fact]
    public void ManualLaunchEnablesAudioWhileExplicitMuteAndControlLaunchDisableIt()
    {
        Assert.True(LaunchOptions.Parse([]).AudioEnabled);
        Assert.True(new LaunchOptions { StatePath = "resume.json" }.AudioEnabled);
        Assert.False(LaunchOptions.Parse(["--mute-audio"]).AudioEnabled);
        Assert.False(LaunchOptions.Parse(["--control-dir", Path.Combine(Path.GetTempPath(), "Memoria-test-audio-controls")]).AudioEnabled);
    }

    [Fact]
    public void ExecutedAudioNotificationsPreserveOrderAndDoNotEnterCheckpointState()
    {
        byte[] program = Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 69), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.EFFECTPLAY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SESTOP, 0), (Jsm.Opcode.RET, 0));
        var firstSession = GameSession.CreateColdProcess();
        var secondSession = GameSession.CreateColdProcess();
        var first = new EventEngine(firstSession);
        var second = new EventEngine(secondSession);
        GlobalSoundResource Resource(int sample) => new(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(0, 0, 0, 22050, 16, 1, null));
        first.ResolveGlobalSoundResource = Resource;
        second.ResolveGlobalSoundResource = Resource;
        List<object> commands = [];
        first.AudioCommandExecuted += commands.Add;
        first.LoadField(100, program, []);
        second.LoadField(100, program, []);
        first.Step();
        second.Step();
        Assert.Collection(commands, request => Assert.IsType<GlobalSoundRequest>(request), request => Assert.IsType<NativeSoundStop>(request));
        Assert.True(JsonElement.DeepEquals(first.Capture(), second.Capture()));
        first.Restore(first.Capture());
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public void InstalledCatalogAndAmbientPcmUseOriginalAssets()
    {
        string directory = InstalledGame();
        var catalog = new NativeMusicCatalog(directory);
        Assert.Equal("026s-garden.sgt", Path.GetFileName(catalog.MusicFile(26, 180)));
        Assert.Equal("038-soyo.wav", Path.GetFileName(catalog.MusicFile(38, 180)));
        Assert.Equal("017s-joriku.sgt", Path.GetFileName(catalog.MusicFile(17, 339)));
        Assert.NotEqual(catalog.MusicFile(17, 339), catalog.MusicFile(17, 120));
        Assert.Equal(-10000, catalog.SoundVolume(0));
        Assert.Equal(0, catalog.SoundVolume(127));
        Assert.Equal(-10000, catalog.SoundPan(0));
        Assert.Equal(10000, catalog.SoundPan(127));
        Assert.Equal(0, catalog.MusicVolume(127));
        PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, 69);
        Assert.Equal(22050, pcm.SampleRate);
        Assert.Equal(1, pcm.Channels);
        Assert.True(pcm.Bytes.Length >= 130622);
        Assert.Equal(0, pcm.Bytes.Length % 2);
        Assert.Contains(pcm.Bytes, value => value != 0);
        GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(directory, 69);
        byte[] compressed = new byte[checked((int)descriptor.CompressedLength)];
        using (FileStream data = File.OpenRead(Path.Combine(directory, "Data", "Sound", "audio.dat")))
        {
            data.Position = descriptor.CompressedOffset;
            data.ReadExactly(compressed);
        }
        byte[] reference = PcmAudioDecoder.Decode(Tools(), "pipe:0", PcmAudioDecoder.CreateWave(descriptor.WaveFormat, compressed), pcm.SampleRate, pcm.Channels);
        Assert.Equal(reference.Length, pcm.Bytes.Length);
        Assert.Equal(-17, BinaryPrimitives.ReadInt16LittleEndian(reference.AsSpan(74, 2)));
        Assert.Equal(-18, BinaryPrimitives.ReadInt16LittleEndian(pcm.Bytes.AsSpan(74, 2)));
        Assert.Equal(131560, pcm.Bytes.Length);
        Assert.Equal("10F80F98050FC68D96164DDE3AA309FFF12D5D06CF378D0D29E0BDA1DE44414D", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pcm.Bytes)));
    }

    [Fact]
    public void WindowsDevicesAcceptOriginalMusicAndPcmWithSilentGain()
    {
        string directory = InstalledGame();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                nint window = GetDesktopWindow();
                using var sound = new DirectSoundOutput(window);
                PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, 69);
                using DirectSoundOutput.Buffer buffer = sound.Create(pcm.Bytes, pcm.SampleRate, pcm.Channels);
                buffer.Volume(-10000);
                buffer.Pan(0);
                buffer.Play(false);
                buffer.Stop();
                using var music = new DirectMusicOutput(window, Path.Combine(directory, "Data", "Music", "dmusic"));
                music.Play(Path.Combine(directory, "Data", "Music", "dmusic", "026s-garden.sgt"), -10000);
                var deadline = Stopwatch.StartNew();
                while (!music.IsPlaying && deadline.ElapsedMilliseconds < 3000)
                {
                    Thread.Sleep(20);
                }
                Assert.True(music.IsPlaying, "DirectMusic did not start the original Garden segment.");
                music.Stop();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Silent device smoke did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string InstalledGame()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio corpus is required.");
        return directory;
    }

    private static string Tools()
    {
        var session = new GameSession(new GameState());
        return new MovieClock(session, TestResources.GameDirectory).ToolsDirectory;
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), encoded);
        }
        return bytes;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
