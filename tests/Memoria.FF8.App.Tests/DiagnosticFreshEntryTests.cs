using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticFreshEntryTests
{
    private static DiagnosticFreshEntryDeclaration Declaration => new(DiagnosticFreshEntryDeclaration.SingleUse, [new("Data/battle.fs", new string('a', 64))]);
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, TailFrames = 2, FreshEntry = Declaration };
    private static DiagnosticDriverObservation Observation(double seconds, long frame, bool drained = true, bool terminal = false) => new(seconds, frame, frame, true, drained, terminal, drained ? [] : ["native-work"]);

    [Fact]
    public void Single_entry_drains_real_terminal_dispatch_and_tail_without_rearm()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        Assert.True(driver.Advance(Observation(0.1, 1)));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.Magic, 1, 2, 2));
        driver.Advance(Observation(0.2, 2, false, true));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.3, 3, true, true));
        driver.Advance(Observation(0.4, 4, false, true));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.5, 5, true, true));
        driver.Advance(Observation(0.6, 6, true, true));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(0.7, 7, true, true));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(1, driver.Execution);
        Assert.Single(driver.Dispatches);
        Assert.False(driver.Advance(Observation(0.8, 8)));
    }

    [Fact]
    public void Fresh_entry_prelude_is_rejected_before_preparation_and_default_charge_is_preserved()
    {
        DiagnosticBattleRequest request = new(982, 0, 0x10, 0, 0)
        {
            Driver = Recipe,
            Prelude = new(new(BattleInputKind.Action, ActorId: "party-1", TargetId: "enemy-0", Ability: BattleAbility.Attack, ActionId: 0), 0, 0, new string('a', 64), false)
        };
        Assert.True(request.InstantGuardianForceCharge);
        Assert.Throws<NotSupportedException>(request.Validate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_before_real_dispatch_remains_blocked(bool fresh)
    {
        DiagnosticBattleDriver driver = new(Recipe with { FreshEntry = fresh ? Declaration : null }, 0);
        driver.Advance(Observation(0, 0, true, true));
        Assert.Equal(DiagnosticDriverStage.Blocked, driver.Stage);
        Assert.Empty(driver.Dispatches);
    }

    [Theory]
    [InlineData("repair")]
    [InlineData("hold")]
    [InlineData("support")]
    [InlineData("unknown")]
    public void Single_entry_rejects_composed_or_unknown_protocols(string fault)
    {
        var recipe = fault switch
        {
            "repair" => Recipe with { Repairs = [new("party-0", 1)] },
            "hold" => Recipe with { TerminalHold = new("native-party-terminal", ["party-0"], 64) },
            "support" => Recipe with { SupportingEffects = [3] },
            _ => Recipe with { FreshEntry = Declaration with { Kind = "arbitrary-count" } }
        };
        Assert.Throws<InvalidDataException>(recipe.Validate);
    }

    [Theory]
    [InlineData("../battle.fs")]
    [InlineData("Data/../battle.fs")]
    [InlineData("/battle.fs")]
    [InlineData("C:/battle.fs")]
    [InlineData("Data//battle.fs")]
    public void Selected_source_paths_cannot_escape_or_alias(string path)
    {
        Assert.Throws<InvalidDataException>(() => new DiagnosticFreshEntrySourceFile(path, new string('a', 64)).Validate());
    }

    [Fact]
    public void Selected_source_receipt_hashes_actual_installed_bytes_and_actual_process_without_native_mutation()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-fresh-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] bytes = [1, 2, 3];
            File.WriteAllBytes(Path.Combine(root, "container.bin"), bytes);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var declaration = new DiagnosticFreshEntryDeclaration(DiagnosticFreshEntryDeclaration.SingleUse, [new("container.bin", hash)]);
            NativeBattleExecutionStack stack = new(0x200100, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('a', 64), 1, 2);
            string before = JsonSerializer.Serialize(stack);
            var receipt = DiagnosticFreshEntryHost.Capture(root, declaration, stack, Guid.NewGuid(), Guid.NewGuid());
            Assert.Equal(before, JsonSerializer.Serialize(stack));
            Assert.Equal(hash, Assert.Single(receipt.SourceFiles).Sha256);
            Assert.Equal(Environment.ProcessId, receipt.ProcessId);
            Assert.NotEqual(Guid.Empty, receipt.HostInstanceId);
            Assert.True(receipt.ProcessStartedUtcTicks > 0);
            Assert.Equal(64, receipt.ManagedHostSha256.Length);
            Assert.Equal(64, receipt.ManagedRuntimeSha256.Length);
            File.WriteAllBytes(Path.Combine(root, "container.bin"), [4]);
            Assert.Throws<InvalidDataException>(() => DiagnosticFreshEntryHost.Capture(root, declaration, stack, Guid.NewGuid(), Guid.NewGuid()));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
