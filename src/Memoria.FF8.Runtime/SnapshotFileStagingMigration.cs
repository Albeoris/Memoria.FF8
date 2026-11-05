using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Preserves observed staging bytes while initializing newly modeled loader storage.</summary>
internal static class SnapshotFileStagingMigration
{
    internal static void Apply(JsonObject saved)
    {
        CompleteHistoricalStaging(Find(saved, "NativeBattleMemory", "FileStaging"));
        CompleteHistoricalStaging(Find(saved, "Battle", "NativeFileStaging"));
        CompleteHistoricalStaging(Find(saved, "Battle", "Definition", "NativeRetainedMemory", "FileStaging"));
        foreach (string owner in new[] { nameof(GameState.EventState), nameof(GameState.DepartingFieldState) })
        {
            CompleteHistoricalStaging(Find(saved, owner, "Scene", "NativeEyeProcess", "Memory", "FileStaging"));
        }
    }

    private static JsonObject? Find(JsonObject saved, params string[] path)
    {
        JsonObject current = saved;
        foreach (string key in path)
        {
            if (current[key] is not JsonObject next)
            {
                return null;
            }
            current = next;
        }
        return current;
    }

    private static void CompleteHistoricalStaging(JsonObject? staging)
    {
        if (staging is null)
        {
            return;
        }
        if (!staging.ContainsKey("BaseAddress"))
        {
            // Historical offsets are relative to this address; the expanded cold image has a lower origin.
            staging["BaseAddress"] = NativeBattleFileStaging.Address;
        }
        if (!staging.ContainsKey("LoaderZeroRanges"))
        {
            NativeBattleFileStaging cold = NativeBattleFileStaging.FromColdProcess();
            if (staging["BaseAddress"] is not JsonValue origin || !origin.TryGetValue(out uint oldAddress) || oldAddress > NativeBattleFileStaging.Address || staging["KnownRanges"] is not JsonArray ranges)
            {
                throw new InvalidDataException("Saved staging requires its explicit origin and observed byte ranges before initializing loader storage.");
            }
            uint newAddress = Math.Min(oldAddress, cold.BaseAddress);
            uint shift = oldAddress - newAddress;
            List<(JsonObject Range, int Offset)> offsets = [];
            foreach (JsonNode? node in ranges)
            {
                if (node is not JsonObject range || range["Offset"] is not JsonValue offsetValue || !offsetValue.TryGetValue(out int offset) || offset < 0 || (ulong)(uint)offset + shift > int.MaxValue)
                {
                    throw new InvalidDataException("Saved staging byte offsets cannot be rebased without changing their absolute addresses.");
                }
                offsets.Add((range, (int)((uint)offset + shift)));
            }
            foreach ((JsonObject range, int offset) in offsets)
            {
                range["Offset"] = offset;
            }
            staging["BaseAddress"] = newAddress;
            // Missing modeled storage receives its initial value; observed ranges continue to override these zeros.
            staging["LoaderZeroRanges"] = InitialLoaderRanges(staging, cold);
        }
    }

    private static JsonArray InitialLoaderRanges(JsonObject staging, NativeBattleFileStaging cold)
    {
        List<NativeBattleLoaderZeroRegion> regions = cold.LoaderZeroRanges.ToList();
        if (staging["ResourcePointers"] is JsonObject pointers)
        {
            foreach ((string key, JsonNode? pointer) in pointers)
            {
                if (pointer is not JsonObject reference || reference["Cpu"] is not JsonObject || !uint.TryParse(key, out uint address))
                {
                    continue;
                }
                // Historical CPU references could own metadata without a numeric DWORD.
                // New cold zero regions cannot turn those unknown bytes into a contradictory
                // observed pointer. Explicit historical KnownRanges remain unchanged.
                List<NativeBattleLoaderZeroRegion> remaining = [];
                foreach (NativeBattleLoaderZeroRegion region in regions)
                {
                    ulong first = Math.Max((ulong)region.Address, address);
                    ulong last = Math.Min((ulong)region.Address + (uint)region.Length, (ulong)address + 4);
                    if (last <= first)
                    {
                        remaining.Add(region);
                        continue;
                    }
                    if (first > region.Address)
                    {
                        remaining.Add(new(region.Address, checked((int)(first - region.Address))));
                    }
                    ulong end = (ulong)region.Address + (uint)region.Length;
                    if (last < end)
                    {
                        remaining.Add(new(checked((uint)last), checked((int)(end - last))));
                    }
                }
                regions = remaining;
            }
        }
        return new JsonArray(regions.Select(region => (JsonNode)new JsonObject { ["Address"] = region.Address, ["Length"] = region.Length }).ToArray());
    }
}
