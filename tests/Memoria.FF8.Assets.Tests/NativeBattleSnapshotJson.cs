using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

/// <summary>Explicit persistence boundaries for tests of live native storage.</summary>
internal static class NativeBattleSnapshotJson
{
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value), options);
    }

    public static T Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        T value = JsonSerializer.Deserialize<T>(json, options)!;
        // Session restoration owns its graph boundary; low-level controller frames need live owners here.
        if (value is BattleSnapshot)
        {
            return value;
        }
        return NativeBattleStorageSnapshots.Restore(value);
    }

    public static void Serialize<T>(Stream stream, T value, JsonSerializerOptions? options = null)
    {
        JsonSerializer.Serialize(stream, NativeBattleStorageSnapshots.Capture(value), options);
    }
}
