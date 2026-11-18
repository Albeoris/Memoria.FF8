using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private Func<NativeEventSceneMemoryOwners>? eventMemorySceneOwners;
    private readonly CapturedEventSceneProjectionCache capturedEventSceneProjections = new();

    /// <summary>Binds the active engine's canonical scene layout without copying scalar values or persisting a second memory owner.</summary>
    public void BindEventMemorySceneOwners(Func<NativeEventSceneMemoryOwners> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ValidateEventMemoryOwners(State, State.EventMemory, provider(), capturedEventSceneProjections);
        eventMemorySceneOwners = provider;
    }

    private void ValidateLiveEventMemoryOwners(GameState state, NativeEventMemory memory)
    {
        ValidateEventMemoryOwners(state, memory, eventMemorySceneOwners?.Invoke() ?? NativeEventSceneMemoryOwners.None, capturedEventSceneProjections);
    }

    private static NativeEventSceneMemoryOwners SavedEventMemorySceneOwners(GameState state, CapturedEventSceneProjectionCache? cache = null)
    {
        cache?.ReleaseAbsentSnapshots(state);
        NativeEventSceneMemoryOwners owners = NativeEventSceneMemoryOwners.None;
        if (state.WorldEventState is JsonElement world)
        {
            if (world.ValueKind != JsonValueKind.Object || !world.TryGetProperty("Version", out JsonElement version) || !version.TryGetInt32(out int versionValue) || versionValue != 1 || !world.TryGetProperty("FieldId", out JsonElement field) || !field.TryGetInt32(out int fieldId) || fieldId < 0 || !world.TryGetProperty("Scene", out JsonElement scene) || scene.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("World event process requires its version, field identity and unallocated Scene.");
            }
            NativeEventSceneMemoryOwners worldOwners = CapturedEventMemorySceneOwners(state, world, cache, 0);
            if (state.EventState is null && state.DepartingFieldState is null)
            {
                owners |= worldOwners;
            }
        }
        if (state.EventState is JsonElement current)
        {
            owners |= CapturedEventMemorySceneOwners(state, current, cache, 1);
        }
        if (state.DepartingFieldState is JsonElement departing)
        {
            owners |= CapturedEventMemorySceneOwners(state, departing, cache, 2);
        }
        return owners;
    }

    private static NativeEventSceneMemoryOwners CapturedEventMemorySceneOwners(GameState state, JsonElement snapshot, CapturedEventSceneProjectionCache? cache, int slot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Captured event memory requires its canonical Scene owner registrations.");
        }
        if (!snapshot.TryGetProperty("Scene", out JsonElement scene))
        {
            // Owner-only Runtime checkpoint components do not contain an executable Scene.
            if (cache is not null)
            {
                cache.Entries[slot] = null;
            }
            return NativeEventSceneMemoryOwners.None;
        }
        CapturedEventSceneProjection? projection = cache?.Entries[slot];
        if (projection is null || !projection.Snapshot.Equals(snapshot))
        {
            NativeEventSceneMemoryOwners parsedOwners = NativeEventMemoryOwnership.SceneOwners(scene);
            if (!snapshot.TryGetProperty("EventMemory", out JsonElement memoryJson) || memoryJson.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidDataException("Captured executable Scene requires its explicit typed event memory.");
            }
            NativeEventMemory parsedMemory = memoryJson.Deserialize<NativeEventMemory>(JsonOptions) ?? throw new InvalidDataException("Captured executable Scene has empty event memory.");
            projection = new(snapshot, parsedOwners, parsedMemory);
        }
        // JsonElement equality identifies the same immutable document element; never reuse a validation result against a different root or live owner set.
        List<NativeEventMemoryRegion> regions = [.. NativeEventMemoryOwnership.RootRegions(state), .. NativeEventMemoryOwnership.SceneRegions(projection.Owners)];
        NativeEventMemoryOwnership.Validate(projection.Memory, regions);
        if (cache is not null)
        {
            cache.Entries[slot] = projection;
        }
        return projection.Owners;
    }

    private sealed record CapturedEventSceneProjection(JsonElement Snapshot, NativeEventSceneMemoryOwners Owners, NativeEventMemory Memory);

    private sealed class CapturedEventSceneProjectionCache
    {
        internal CapturedEventSceneProjection?[] Entries { get; } = new CapturedEventSceneProjection?[3];

        internal void ReleaseAbsentSnapshots(GameState state)
        {
            if (state.WorldEventState is null)
            {
                Entries[0] = null;
            }
            if (state.EventState is null)
            {
                Entries[1] = null;
            }
            if (state.DepartingFieldState is null)
            {
                Entries[2] = null;
            }
        }
    }
}
