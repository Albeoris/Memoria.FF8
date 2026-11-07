using System.Reflection;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentSelectorCatalogTests
{
    [Fact]
    public void Every_nonempty_effect_has_an_explicit_prepared_source_execution_contract()
    {
        NativeBattleComponentSelectorEntry[] entries = NativeBattleComponentSelectorCatalog.Entries;
        Assert.Equal(343, entries.Length);
        Assert.Equal(Enumerable.Range(1, 345).Except([224, 225]).Select(effect => (ushort)effect), entries.Select(entry => entry.Effect));
        Assert.All(entries, entry =>
        {
            Assert.Equal(nameof(NativeBattleSelectedEffectPresentationTests), entry.QueuedSourceTestClass);
            Assert.Equal(nameof(NativeBattleSelectedEffectPresentationTests.Selected_queued_component_completes_and_restores_its_live_owners), entry.QueuedSourceTestMethod);
            Assert.Equal("MEMORIA_FF8_SELECTED_EFFECT", entry.QueuedSourceEnvironmentPrefix);
        });
        Assert.Contains(entries, entry => entry.RequiresExternalPreparedSource);
        Assert.Contains(entries, entry => entry.HasExistingSourceHandoff);
    }

    [Fact]
    public void Indexed_existing_selectors_resolve_to_the_actual_test_and_factory_methods()
    {
        Assembly assembly = typeof(NativeBattleComponentSelectorCatalog).Assembly;
        foreach (NativeBattleComponentSelector selector in NativeBattleComponentSelectorCatalog.Entries.SelectMany(entry => entry.ExistingSelectors))
        {
            Type type = assembly.GetType("Memoria.FF8.App.Tests." + selector.TestClass, true)!;
            Assert.Contains(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == selector.TestMethod);
            string[] factory = selector.SourceFactory.Split('.');
            Type factoryType = assembly.GetType("Memoria.FF8.App.Tests." + factory[0], true)!;
            Assert.Contains(factoryType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static), method => method.Name == factory[1]);
            Assert.StartsWith("MEMORIA_FF8_", selector.EnvironmentPrefix);
            Assert.NotEmpty(selector.SourceArguments);
        }
    }

    [Fact]
    public void Distinct_original_source_variants_are_not_collapsed_by_effect_identity()
    {
        NativeBattleComponentSelectorEntry entry = NativeBattleComponentSelectorCatalog.Require(182);
        Assert.Contains(entry.ExistingSelectors, selector => selector.EnvironmentPrefix == "MEMORIA_FF8_ADDITIONAL182_CLEAR");
        Assert.Contains(entry.ExistingSelectors, selector => selector.EnvironmentPrefix == "MEMORIA_FF8_ADDITIONAL182_SET");
        Assert.True(entry.HasExistingSourceHandoff);
    }

    [Fact]
    public void Specialized_dispatch_and_external_sources_are_not_misreported_as_shared_factory_handoffs()
    {
        NativeBattleComponentSelectorEntry specialized = NativeBattleComponentSelectorCatalog.Require(316);
        Assert.NotEmpty(specialized.ExistingSelectors);
        Assert.All(specialized.ExistingSelectors, selector => Assert.False(selector.SupportsQueuedSourceHandoff));
        Assert.True(specialized.RequiresExternalPreparedSource);
        NativeBattleComponentSelectorEntry external = NativeBattleComponentSelectorCatalog.Require(343);
        Assert.Empty(external.ExistingSelectors);
        Assert.True(external.RequiresExternalPreparedSource);
    }
}
