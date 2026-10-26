using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSoundPositionPresentationTests
{
    [Fact]
    public void SilentLivePrimaryAndLoopReceiveExecutedPanTransitionAndImmediatePosition()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DirectSound output requires Windows.");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio sample is required.");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                GameSession session = GameSession.CreateColdProcess();
                EventEngine engine = new(session) { ResolveGlobalSoundResource = sample => GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory, sample)) };
                string tools = new MovieClock(session, directory).ToolsDirectory;
                using FieldAudioPresentation presentation = new(GetDesktopWindow(), directory, tools);
                presentation.Attach(engine);
                engine.LoadField(100, Program((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 69), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.EFFECTPLAY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SEPOSTRANS, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.SEPOS, 0), (Jsm.Opcode.RET, 8)), []);
                engine.Step();
                Assert.Null(presentation.Error);
                IDictionary voices = (IDictionary)typeof(FieldAudioPresentation).GetField("voices", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(presentation)!;
                object voice = voices[3]!;
                PcmAudioOutput.Buffer primary = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Primary")!.GetValue(voice)!;
                PcmAudioOutput.Buffer loop = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Loop")!.GetValue(voice)!;
                NativeMusicCatalog catalog = new(directory);
                Assert.Equal(catalog.SoundPan(0), ReadPan(primary));
                Assert.Equal(catalog.SoundPan(0), ReadPan(loop));
                engine.Step();
                Assert.Equal(60, engine.Scene.SoundPosition!.DurationMilliseconds);
                presentation.Update();
                Thread.Sleep(100);
                presentation.Update();
                Assert.Null(presentation.Error);
                Assert.Equal(catalog.SoundPan(127), ReadPan(primary));
                Assert.Equal(catalog.SoundPan(127), ReadPan(loop));
                engine.Step();
                Assert.Equal("SEPOS", engine.Scene.SoundPosition!.Opcode);
                presentation.Update();
                Assert.Null(presentation.Error);
                Assert.Equal(catalog.SoundPan(64), ReadPan(primary));
                Assert.Equal(catalog.SoundPan(64), ReadPan(loop));
                Assert.True(session.GlobalSoundProcess!.OriginalEffectIds[3] == 69 && session.GlobalSoundProcess.Rotation == 1);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Silent native pan output check timed out.");
        Assert.Null(failure);
    }

    private static int ReadPan(PcmAudioOutput.Buffer buffer)
    {
        return buffer.PanValue;
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
