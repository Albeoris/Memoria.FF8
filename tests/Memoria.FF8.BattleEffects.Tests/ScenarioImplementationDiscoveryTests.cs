using System.Reflection;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioImplementationDiscoveryTests
{
    [Fact]
    public void Direct_compiled_equality_links_all_shared_rows_without_invoking_the_target()
    {
        MethodInfo method = typeof(ScenarioImplementationDiscoveryTests).GetMethod(nameof(DispatchSynthetic), BindingFlags.Static | BindingFlags.NonPublic)!;
        ImplementationDiscoveryEvidence result = ScenarioImplementationDiscovery.Inspect(method, [new(1, 0x123456, 0), new(2, 0x123456, 0), new(3, 0x999999, 0)]);
        ImplementationRegistration registration = Assert.Single(result.Registrations);
        Assert.Equal(0x123456u, registration.Entry);
        Assert.EndsWith(".EnterSynthetic", registration.CalledMethod);
        Assert.Matches("^[a-f0-9]{64}$", registration.CalledMethodIlSha256);
        Assert.Equal("direct_equality_branch_observed", result.Bindings[0].Disposition);
        Assert.Equal("direct_equality_branch_observed", result.Bindings[1].Disposition);
        Assert.Equal("unknown", result.Bindings[2].Disposition);
        Assert.Empty(result.Bindings[2].Registrations);
    }

    [Fact]
    public void Actual_dispatcher_inventory_reports_evidence_and_preserves_unrecognized_paths()
    {
        ImplementationDiscoveryEvidence result = ScenarioImplementationDiscovery.Discover([new(226, 0x694520, 0), new(399, uint.MaxValue, 0)]);
        Assert.Equal("compiled_dispatch_registration_evidence_only", result.Scope);
        Assert.Contains(result.Bindings[0].Registrations, registration => registration.CalledMethod.EndsWith(".EnterNativeWhiteWind226", StringComparison.Ordinal));
        Assert.Equal("unknown", result.Bindings[1].Disposition);
        Assert.NotEmpty(result.OpaqueCalledMethods);
        Assert.Matches("^[a-f0-9]{64}$", result.DispatcherIlSha256);
        Assert.Matches("^[a-f0-9]{64}$", result.RuntimeAssemblySha256);
    }

    private static int DispatchSynthetic(NativeBattleEffectBinding binding)
    {
        if (binding.Entry == 0x123456)
        {
            return EnterSynthetic(binding);
        }
        return 0;
    }

    private static int EnterSynthetic(NativeBattleEffectBinding binding)
    {
        throw new InvalidOperationException("Discovery must not invoke registration targets.");
    }
}
