using System.Collections.Immutable;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Runtime;

internal enum SnapshotLifetime
{
    Process,
    BattleResource
}

internal enum SnapshotAuthority
{
    Retained,
    Derived
}

internal enum SnapshotReconstruction
{
    None,
    NativeModelLoadOperations
}

/// <summary>Declares a member's lifetime independently of whether it is authoritative saved state.</summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SnapshotOwnershipAttribute(SnapshotLifetime lifetime, SnapshotAuthority authority, SnapshotReconstruction reconstruction) : Attribute
{
    internal SnapshotLifetime Lifetime { get; } = lifetime;
    internal SnapshotAuthority Authority { get; } = authority;
    internal SnapshotReconstruction Reconstruction { get; } = reconstruction;
}

internal static class SnapshotOwnership
{
    /// <summary>Discards inactive managed resource metadata; physical shared memory and dormant pointers remain authoritative.</summary>
    internal static GameState Prepare(GameState snapshot)
    {
        if (snapshot.Battle is not null || snapshot.NativeBattleMemory?.ModelCpuResources is not { } resources)
        {
            return snapshot;
        }
        ImmutableArray<NativeBattleModelCpuResourceRow>.Builder? rows = null;
        for (int index = 0; index < resources.Rows.Length; index++)
        {
            NativeBattleModelCpuResourceRow row = resources.Rows[index];
            if (row.Lengths.Count == 0)
            {
                continue;
            }
            rows ??= resources.Rows.ToBuilder();
            rows[index] = row with { Lengths = ImmutableDictionary<int, int>.Empty };
        }
        if (rows is null)
        {
            return snapshot;
        }
        return snapshot with { NativeBattleMemory = snapshot.NativeBattleMemory with { ModelCpuResources = resources with { Rows = rows.ToImmutable() } } };
    }

    internal static void ConfigureSerialization(JsonTypeInfo type)
    {
        foreach (JsonPropertyInfo property in type.Properties)
        {
            SnapshotOwnershipAttribute? ownership = property.AttributeProvider?.GetCustomAttributes(typeof(SnapshotOwnershipAttribute), true).OfType<SnapshotOwnershipAttribute>().SingleOrDefault();
            if (ownership is null || ownership.Authority == SnapshotAuthority.Retained)
            {
                continue;
            }
            if (type.Type != typeof(NativeBattleModelCpuResourceRow) || property.Name != nameof(NativeBattleModelCpuResourceRow.Lengths) || ownership.Lifetime != SnapshotLifetime.BattleResource || ownership.Reconstruction != SnapshotReconstruction.NativeModelLoadOperations)
            {
                throw new NotSupportedException($"Snapshot ownership has no reconstruction contract for {type.Type.Name}.{property.Name}.");
            }
            // Live battles retain these extents. Inactive rows obtain fresh extents from actual model load operations.
            property.ShouldSerialize = (_, value) => value is not ImmutableDictionary<int, int> { Count: 0 };
        }
    }
}
