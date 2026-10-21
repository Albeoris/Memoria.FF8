using System.Reflection;
using System.Runtime.InteropServices;

namespace Memoria.FF8.App.Tests;

public sealed class FocusAudioTests
{
    [Fact]
    public void PausedRampsExcludeBackgroundTimeAndResumeFromTheirPriorGain()
    {
        ManualTimeProvider time = new();
        FieldAudioPresentation.Ramp ramp = new(0, 100, 1000, time);
        time.Advance(250);
        Assert.Equal(25, ramp.Value);
        ramp.Pause();
        time.Advance(5000);
        ramp.Pause();
        Assert.Equal(25, ramp.Value);
        Assert.False(ramp.Finished);
        ramp.Resume();
        time.Advance(250);
        ramp.Resume();
        Assert.Equal(50, ramp.Value);
        time.Advance(500);
        Assert.True(ramp.Finished);
        Assert.Equal(100, ramp.Value);
    }

    [Fact]
    public void FocusPausesBothMusicRampsWithoutLoadingGameResources()
    {
        ManualTimeProvider time = new();
        FieldAudioPresentation.Ramp midi = new(100, 0, 1000, time);
        FieldAudioPresentation.Ramp stream = new(0, 100, 1000, time);
        using FieldAudioPresentation output = new(0, "", "");
        SetField(output, "midiRamp", midi);
        SetField(output, "streamRamp", stream);
        time.Advance(250);
        output.SetFocusPaused(true);
        time.Advance(5000);
        output.SetFocusPaused(true);
        output.Update();
        Assert.Equal(75, midi.Value);
        Assert.Equal(25, stream.Value);
        Assert.Null(output.Error);
        output.SetFocusPaused(false);
        time.Advance(250);
        output.SetFocusPaused(false);
        Assert.Equal(50, midi.Value);
        Assert.Equal(50, stream.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectSoundPausePreservesCursorAndPlaybackMode(bool loop)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DirectSound is a Windows output device.");
        using DirectSoundOutput output = new(GetDesktopWindow());
        using DirectSoundOutput.Buffer buffer = output.Create(new byte[44100 * 2 * 5], 44100, 1);
        buffer.Play(loop);
        Assert.True(SpinWait.SpinUntil(() => Position(buffer) > 0, TimeSpan.FromSeconds(2)), "DirectSound did not advance its playback cursor.");
        buffer.Pause();
        uint position = Position(buffer);
        Assert.Equal(0u, Status(buffer) & 1);
        await Task.Delay(30, TestContext.Current.CancellationToken);
        buffer.Pause();
        Assert.Equal(position, Position(buffer));
        buffer.Resume();
        Assert.Equal(loop ? 5u : 1u, Status(buffer) & 5);
        buffer.Resume();
        buffer.Stop();
        buffer.Pause();
        buffer.Resume();
        Assert.Equal(0u, Status(buffer) & 1);
    }

    [Fact]
    public void DirectSoundFocusResumeDoesNotRestartACompletedBuffer()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DirectSound is a Windows output device.");
        using DirectSoundOutput output = new(GetDesktopWindow());
        using DirectSoundOutput.Buffer buffer = output.Create(new byte[882], 44100, 1);
        buffer.Play(false);
        Assert.True(SpinWait.SpinUntil(() => (Status(buffer) & 1) == 0, TimeSpan.FromSeconds(2)), "The short DirectSound buffer did not finish.");
        buffer.Pause();
        buffer.Resume();
        Assert.Equal(0u, Status(buffer) & 1);
    }

    private static void SetField(object target, string name, object value)
    {
        typeof(FieldAudioPresentation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private static nint Handle(DirectSoundOutput.Buffer buffer)
    {
        return (nint)typeof(DirectSoundOutput.Buffer).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(buffer)!;
    }

    private static uint Status(DirectSoundOutput.Buffer buffer)
    {
        nint handle = Handle(buffer);
        WindowsAudioCom.Check(WindowsAudioCom.Method<GetStatus>(handle, 9)(handle, out uint status));
        return status;
    }

    private static uint Position(DirectSoundOutput.Buffer buffer)
    {
        nint handle = Handle(buffer);
        WindowsAudioCom.Check(WindowsAudioCom.Method<GetPosition>(handle, 4)(handle, out uint position, out _));
        return position;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(int milliseconds)
        {
            timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetStatus(nint instance, out uint status);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetPosition(nint instance, out uint play, out uint write);

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
