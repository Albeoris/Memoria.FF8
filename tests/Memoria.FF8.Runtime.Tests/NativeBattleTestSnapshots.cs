using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleTestSnapshots
{
    internal static T Independent<T>(T value) => NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(value));

    internal static string Json<T>(T value, JsonSerializerOptions? options = null) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value), options);

    internal static string Json(object? value, Type type, JsonSerializerOptions? options = null) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value), type, options);

    internal static T RestoreJson<T>(string json, JsonSerializerOptions? options = null)
    {
        T? snapshot = JsonSerializer.Deserialize<T>(json, options);
        if (snapshot is null)
        {
            throw new InvalidDataException("The detached test snapshot is null.");
        }
        T restored = NativeBattleStorageSnapshots.Restore(snapshot);
        Type type = restored!.GetType();
        if (type.Name.EndsWith("Frame", StringComparison.Ordinal) && type.GetMethod("<Clone>$") is not null)
        {
            System.Reflection.PropertyInfo? memoryProperty = type.GetProperty("Memory");
            System.Reflection.PropertyInfo? effectProperty = type.GetProperty("Effect");
            if (memoryProperty?.PropertyType == typeof(NativeBattleEffectMemory) && effectProperty?.PropertyType == typeof(NativeBattleOriginalEffectState) && memoryProperty.SetMethod is not null && memoryProperty.GetValue(restored) is NativeBattleEffectMemory { Arena: not null } memory && effectProperty.GetValue(restored) is NativeBattleOriginalEffectState effect)
            {
                // Isolated test frames have no persisted root storage graph; bind their canonical physical banks explicitly.
                memoryProperty.SetValue(restored, NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools));
            }
        }
        return restored;
    }
}
