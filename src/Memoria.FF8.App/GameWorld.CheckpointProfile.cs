using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeFieldRuntimeProfile? SelectFieldRuntimeProfile(JsonElement? checkpoint)
    {
        if (checkpoint is not { } saved || !saved.TryGetProperty("Scene", out JsonElement scene) || !scene.TryGetProperty("NativeFieldInitialization", out JsonElement initialization) || initialization.ValueKind == JsonValueKind.Null)
        {
            return assets.NativeRuntimeProfile;
        }
        if (!initialization.TryGetProperty("ExecutableHash", out JsonElement image) || image.ValueKind != JsonValueKind.String || NativeFieldRuntimeProfile.ForImage(image.GetString()) is not { } profile)
        {
            throw new InvalidDataException("A field execution checkpoint requires its supported saved address-domain identity.");
        }
        // Source addresses and allocation receipts retain their saved domain. Selecting
        // different installed resources must not reinterpret them as another guest layout.
        return profile;
    }
}
