using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotPresentationSettingsTests
{
    [Fact]
    public void Absent_presentation_override_and_audible_output_preserve_host_defaults()
    {
        Assert.Null(PilotPresentationSettings.CreateUiSettings(false, null));
        JsonObject document = JsonSerializer.SerializeToNode(Manifest(null), RecordJson.Options)!.AsObject();
        document.Remove("presentation_v_sync");
        PilotManifest restored = document.Deserialize<PilotManifest>(RecordJson.Options)!;
        Assert.Null(restored.PresentationVSync);
        Assert.Null(PilotPresentationSettings.CreateUiSettings(restored.MuteOutput, restored.PresentationVSync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_vsync_profile_round_trips_its_exact_manifest_field_without_muting_audio(bool vSync)
    {
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(Manifest(vSync), RecordJson.Options);
        using JsonDocument manifest = JsonDocument.Parse(serialized);
        Assert.Equal(vSync, manifest.RootElement.GetProperty("presentation_v_sync").GetBoolean());
        PilotManifest restored = JsonSerializer.Deserialize<PilotManifest>(serialized, RecordJson.Options)!;
        Assert.Equal(vSync, restored.PresentationVSync);
        using JsonDocument settings = JsonDocument.Parse(PilotPresentationSettings.CreateUiSettings(restored.MuteOutput, restored.PresentationVSync)!);
        Assert.Equal(vSync, settings.RootElement.GetProperty("VSync").GetBoolean());
        Assert.Single(settings.RootElement.EnumerateObject());
        Assert.False(settings.RootElement.TryGetProperty("MusicVolume", out _));
        Assert.False(settings.RootElement.TryGetProperty("SoundVolume", out _));
        Assert.False(settings.RootElement.TryGetProperty("VoiceVolume", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void Muting_preserves_optional_vsync_and_both_fresh_entries_receive_identical_settings(bool? vSync)
    {
        byte[] first = PilotPresentationSettings.CreateUiSettings(true, vSync)!;
        byte[] second = PilotPresentationSettings.CreateUiSettings(true, vSync)!;
        Assert.Equal(first, second);
        using JsonDocument settings = JsonDocument.Parse(first);
        Assert.Equal(0, settings.RootElement.GetProperty("MusicVolume").GetInt32());
        Assert.Equal(0, settings.RootElement.GetProperty("SoundVolume").GetInt32());
        Assert.Equal(0, settings.RootElement.GetProperty("VoiceVolume").GetInt32());
        Assert.Equal(vSync.HasValue, settings.RootElement.TryGetProperty("VSync", out JsonElement property));
        if (vSync is bool expected)
        {
            Assert.Equal(expected, property.GetBoolean());
        }
    }

    private static PilotManifest Manifest(bool? vSync)
    {
        return new(1, "host", "work", "game", "output", new(1, 1, 1, 1, 1, 1, 1), []) { PresentationVSync = vSync };
    }
}
