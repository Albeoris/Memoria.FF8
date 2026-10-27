using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76FileAudioTests
{
    [Fact]
    public void Bufferless_file_request_retains_three_operands_and_does_not_mutate_other_sound_owners()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(process));
        NativeBattleFileAudioRequest request = NativeBattleFileAudioRequest.Create46CBB0(process, 0, "field2.fs", 127);
        Assert.Equal(0, request.Channel);
        Assert.Equal("field2.fs", request.ResourceName);
        Assert.Equal(127, request.Gain);
        Assert.Equal(0, request.Result);
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(process)));
        BattleEvent item = new(0, "NativeFileAudio", null, null, 0, string.Empty) { NativeFileAudio = request };
        BattleEvent restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<BattleEvent>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(item)))!);
        restored.NativeFileAudio!.Validate();
        Assert.Equal(request, restored.NativeFileAudio);
    }

    [Theory]
    [InlineData("C:\\media\\field2.fs")]
    [InlineData("../field2.fs")]
    [InlineData("")]
    public void Original_cd_root_is_not_invented_or_serialized_as_the_resource_name(string name)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleFileAudioRequest.Create46CBB0(NativeGlobalSoundProcess.FromBufferlessColdProcess(), 0, name, 127));
    }

    [Fact]
    public void Unsupported_channel_or_fabricated_playing_result_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleFileAudioRequest.Create46CBB0(NativeGlobalSoundProcess.FromBufferlessColdProcess(), 1, "field2.fs", 127));
        Assert.Throws<InvalidDataException>(() => new NativeBattleFileAudioRequest(0, "field2.fs", 127, 1, NativeGlobalSoundBackend.Bufferless).Validate());
    }
}
