using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleTestSnapshots
{
    internal static string Json<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

    internal static T RestoreJson<T>(string json)
    {
        T? snapshot = JsonSerializer.Deserialize<T>(json);
        if (snapshot is null)
        {
            throw new InvalidDataException("The detached test snapshot is null.");
        }
        return NativeBattleStorageSnapshots.Restore(snapshot);
    }
}
