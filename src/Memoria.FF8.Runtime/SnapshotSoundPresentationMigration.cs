using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Runtime;

/// <summary>Completes historical sound presentation without changing retained execution history.</summary>
internal static class SnapshotSoundPresentationMigration
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        JsonObject? current = saved["EventState"] as JsonObject;
        Complete(saved, current?["Scene"] as JsonObject, options);
        JsonSerializerOptions vmOptions = new(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        foreach (string name in new[] { "EventState", "DepartingFieldState", "WorldEventState" })
        {
            if (saved[name] is JsonObject vm)
            {
                Complete(vm, vm["Scene"] as JsonObject, vmOptions);
            }
        }
    }

    private static void Complete(JsonObject owner, JsonObject? scene, JsonSerializerOptions options)
    {
        if (owner["GlobalSoundProcess"] is not JsonObject sound)
        {
            return;
        }
        if (!sound.ContainsKey("MusicPlayback"))
        {
            NativeMusicPlaybackState playback = RestorePresentation(scene);
            sound["MusicPlayback"] = JsonSerializer.SerializeToNode(playback, options);
        }
        // Explicitly absent optional members must have the same shape in the root and its VM mirror.
        // Deserialization validates required members and preserves all recorded sound/SPU counters.
        NativeGlobalSoundProcess normalized = sound.Deserialize<NativeGlobalSoundProcess>(options) ?? throw new InvalidDataException("Empty historical sound process.");
        normalized.Validate();
        owner["GlobalSoundProcess"] = JsonSerializer.SerializeToNode(normalized, options);
    }

    private static NativeMusicPlaybackState RestorePresentation(JsonObject? scene)
    {
        if (scene?["MusicPlaying"] is not JsonValue playing || !playing.TryGetValue(out bool active))
        {
            return NativeMusicPlaybackState.Unknown();
        }
        NativeMusicPlaybackState defaults = NativeMusicPlaybackState.FromColdProcess() with { BattleFirstLoadB8B7D4 = null };
        if (!active)
        {
            return defaults;
        }
        int loaded = scene["LoadedMusic"]?.GetValue<int>() ?? -1;
        if (loaded < 0)
        {
            return NativeMusicPlaybackState.Unknown();
        }
        GameplaySaveSceneMusic music = new(loaded, true, scene["MusicVolume"]?.GetValue<int>() ?? 127, null, null, null, Channel(scene["MusicChannel108"] as JsonObject), Channel(scene["MusicChannel112"] as JsonObject), null);
        return GameplaySaveMusicPlayback.FromSceneDefaults(music)!.Restore(defaults);
    }

    private static GameplaySaveMusicChannel? Channel(JsonObject? channel)
    {
        return channel is null ? null : new(channel["ChannelId"]?.GetValue<int>() ?? -1, channel["MusicId"]?.GetValue<byte>() ?? 255, channel["ChoiceMask"]?.GetValue<uint>());
    }
}
