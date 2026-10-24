using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSoundGainRequestTests
{
    [Theory]
    [InlineData(120, 0, 1200, 0)]
    [InlineData(260, 255, 0, 127)]
    [InlineData(261, 128, 50, 0)]
    [InlineData(-1, -1, 2550, 127)]
    public void Direct_call_narrows_duration_and_gain_without_field_wrapper_doubling(int duration, int gain, int milliseconds, byte expectedGain)
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleSoundGainRequest request = NativeBattleSoundGainRequest.Create46BEC0(process, duration, gain);
        request.Validate();
        Assert.Equal(milliseconds, request.DurationMilliseconds);
        Assert.Equal(expectedGain, request.Gain);
        Assert.Equal(1, request.FirstVoice);
        Assert.Equal(29, request.VoiceCount);
        Assert.True(process.IsColdProcess);
    }

    [Fact]
    public void Event_roundtrip_preserves_request_and_rejects_changed_voice_range_or_duration()
    {
        NativeBattleSoundGainRequest request = NativeBattleSoundGainRequest.Create46BEC0(NativeGlobalSoundProcess.FromBufferlessColdProcess(), 120, 0);
        BattleEvent entry = new(802, "NativeSoundGain", null, null, 0, string.Empty) { NativeSoundGain = request };
        BattleEvent restored = JsonSerializer.Deserialize<BattleEvent>(JsonSerializer.Serialize(entry))!;
        Assert.Equal(request, restored.NativeSoundGain);
        restored.NativeSoundGain!.Validate();
        Assert.Throws<InvalidDataException>(() => (request with { FirstVoice = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { VoiceCount = 32 }).Validate());
        Assert.Throws<InvalidDataException>(() => (request with { DurationMilliseconds = 120 }).Validate());
    }
}
