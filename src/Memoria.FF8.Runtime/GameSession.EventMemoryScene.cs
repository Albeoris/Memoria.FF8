using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private Func<NativeEventSceneMemoryOwners>? eventMemorySceneOwners;

    /// <summary>Binds the active engine's canonical scene layout without copying scalar values or persisting a second memory owner.</summary>
    public void BindEventMemorySceneOwners(Func<NativeEventSceneMemoryOwners> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateEventMemoryOwners(State, State.EventMemory, provider());
        eventMemorySceneOwners = provider;
    }

    private void ValidateLiveEventMemoryOwners(GameState state, NativeEventMemory memory)
    {
        ValidateEventMemoryOwners(state, memory, eventMemorySceneOwners?.Invoke() ?? NativeEventSceneMemoryOwners.None);
    }

    private static NativeEventSceneMemoryOwners SavedEventMemorySceneOwners(GameState state)
    {
        NativeEventSceneMemoryOwners owners = NativeEventSceneMemoryOwners.None;
        if (state.EventState is JsonElement current)
        {
            owners |= CapturedEventMemorySceneOwners(state, current);
        }
        if (state.DepartingFieldState is JsonElement departing)
        {
            owners |= CapturedEventMemorySceneOwners(state, departing);
        }
        return owners;
    }

    private static NativeEventSceneMemoryOwners CapturedEventMemorySceneOwners(GameState state, JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Captured event memory requires its canonical Scene owner registrations.");
        }
        if (!snapshot.TryGetProperty("Scene", out JsonElement scene))
        {
            // Owner-only Runtime checkpoint components do not contain an executable Scene.
            return NativeEventSceneMemoryOwners.None;
        }
        NativeEventSceneMemoryOwners owners = NativeEventMemoryOwnership.SceneOwners(scene);
        if (!snapshot.TryGetProperty("EventMemory", out JsonElement memoryJson) || memoryJson.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidDataException("Captured executable Scene requires its explicit typed event memory.");
        }
        NativeEventMemory memory = memoryJson.Deserialize<NativeEventMemory>(JsonOptions) ?? throw new InvalidDataException("Captured executable Scene has empty event memory.");
        List<NativeEventMemoryRegion> regions = [.. NativeEventMemoryOwnership.RootRegions(state), .. NativeEventMemoryOwnership.SceneRegions(owners)];
        NativeEventMemoryOwnership.Validate(memory, regions);
        return owners;
    }
}
