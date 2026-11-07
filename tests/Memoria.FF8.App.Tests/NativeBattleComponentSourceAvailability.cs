using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

internal sealed record NativeBattleComponentSourceAvailabilityEntry(ushort Effect, bool Ready, string? Provider, string? Method);

internal static class NativeBattleComponentSourceAvailability
{
    internal static readonly string[] FactoryTypes =
    [
        "NativeBattlePsychoBlastComponentSource",
        "NativeBattleLifecycleComponentSources",
        "NativeBattleActivatedComponentSources",
        "NativeBattleFinalCatalogComponentSources",
        "NativeBattleLimitComponentSources",
        "NativeBattleSpecializedComponentSources",
        "NativeBattleRemainingComponentSources",
        "NativeBattleExistingComponentSourceBridge"
    ];

    internal static NativeBattleComponentSourceAvailabilityEntry Get(ushort effect)
    {
        NativeBattleComponentSelectorEntry entry = NativeBattleComponentSelectorCatalog.Require(effect);
        foreach (string name in FactoryTypes)
        {
            Type? type = typeof(NativeBattleComponentSourceAvailability).Assembly.GetType("Memoria.FF8.App.Tests." + name);
            MethodInfo? supports = type?.GetMethod("Supports", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort)]);
            MethodInfo? prepare = type?.GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, [typeof(ushort), typeof(string), typeof(string)]);
            if (supports is not null && prepare is not null && (bool)supports.Invoke(null, [effect])!)
            {
                return new(effect, true, name, "Prepare");
            }
        }
        if (effect is not (79 or 83 or 283 or 289))
        {
            NativeBattleComponentSelector? selector = entry.ExistingSelectors.FirstOrDefault(item => item.SupportsQueuedSourceHandoff);
            if (selector is not null)
            {
                return new(effect, true, selector.TestClass, selector.TestMethod);
            }
        }
        return new(effect, false, null, null);
    }

    internal static NativeBattleComponentSourceAvailabilityEntry[] All() => NativeBattleComponentSelectorCatalog.Entries.Select(entry => Get(entry.Effect)).ToArray();
}

public sealed class NativeBattleComponentSourceAvailabilityTests
{
    [Fact]
    public void Source_readiness_inventory_distinguishes_registered_preparation_from_generic_execution()
    {
        NativeBattleComponentSourceAvailabilityEntry[] entries = NativeBattleComponentSourceAvailability.All();
        Assert.Equal(343, entries.Length);
        Assert.Equal(entries.Length, entries.Select(entry => entry.Effect).Distinct().Count());
        Assert.All(entries.Where(entry => entry.Ready), entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Provider));
            Assert.False(string.IsNullOrWhiteSpace(entry.Method));
        });
        Assert.Equal("NativeBattleRemainingComponentSources", NativeBattleComponentSourceAvailability.Get(308).Provider);
        Assert.Equal("NativeBattlePsychoBlastComponentSource", NativeBattleComponentSourceAvailability.Get(23).Provider);
        Assert.Equal("NativeBattleExistingComponentSourceBridge", NativeBattleComponentSourceAvailability.Get(54).Provider);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_SELECTOR_INVENTORY");
        if (!string.IsNullOrWhiteSpace(output))
        {
            if (!Path.IsPathFullyQualified(output))
            {
                throw new InvalidDataException("Component source inventory requires an absolute output path.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using FileStream stream = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, new { SchemaVersion = 1, Scope = "native-effect-component-source-readiness", CatalogCount = entries.Length, RegisteredCount = entries.Count(entry => entry.Ready), Entries = entries, Executed = false, OrdinaryAdmissionCertified = false });
        }
    }
}
