using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticInvocationReleaseTests
{
    internal static BattleDiagnosticInvocationRelease Receipt(long callback = 3, long timestamp = 300)
    {
        BattleDiagnosticEffectDispatch dispatch = new(1, 1, "party-0", BattleAbility.Magic, 1, 2, 2, 2) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        byte[] header = new byte[20];
        header[1] = 2;
        header[4] = 1;
        header[6] = 2;
        byte[] packet = new byte[12];
        packet[0] = 7;
        packet[1] = 128;
        packet[2] = 10;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x47DDA0);
        return new(Guid.NewGuid(), callback, 2, timestamp, dispatch, new(header.ToImmutableArray(), 0, 0, null), new(packet.ToImmutableArray()), 0x47DDA0, 0, 0, 7, 0x47DDA0, true, true, true, true, new(1, 0, 0, 0, false, false, 0, false, 0, 0, false, 0, false, false, false, false) { ObservedOriginalEffect = true, SelectedReturnedDescriptor = 0, OriginalPools = [] }, []);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("lifecycle")]
    [InlineData("pool")]
    [InlineData("post-action")]
    [InlineData("ack")]
    [InlineData("generation")]
    [InlineData("serial")]
    [InlineData("packet")]
    [InlineData("packet-bytes")]
    [InlineData("packet-unknown")]
    [InlineData("header-bytes")]
    [InlineData("task")]
    [InlineData("contact")]
    [InlineData("pointer")]
    [InlineData("detached")]
    [InlineData("resource")]
    [InlineData("unknown-files")]
    [InlineData("action")]
    [InlineData("early")]
    public void Native_release_requires_exact_completion_and_conservative_empty_owners(string fault)
    {
        BattleDiagnosticInvocationRelease baseline = Receipt();
        DiagnosticInvocationReleaseValidation.Validate(baseline, baseline.Dispatch, baseline.LifecycleId);
        var invalid = fault switch
        {
            "owner" => baseline with { EventActorIndex = 1 },
            "lifecycle" => baseline with { LifecycleId = Guid.NewGuid() },
            "pool" => baseline with { Owners = baseline.Owners with { OriginalPools = [new(1, 2, 12, 1, true, true)] } },
            "post-action" => baseline with { NativeEventCompleted = false },
            "ack" => baseline with { PresentationAcknowledged = false },
            "generation" => baseline with { HeaderCapturedBeforeReset = false },
            "serial" => baseline with { Dispatch = baseline.Dispatch with { Serial = 2 } },
            "packet" => baseline with { CompletionPacketSerial = 8 },
            "packet-unknown" => baseline with { CompletionPacket = baseline.CompletionPacket! with { UnknownByteMask = 1 } },
            "packet-bytes" => baseline with { CompletionPacket = baseline.CompletionPacket! with { Bytes = baseline.CompletionPacket!.Bytes.SetItem(2, 104) } },
            "header-bytes" => baseline with { Header = baseline.Header with { Payload = baseline.Header.Payload.SetItem(4, 3) } },
            "task" => baseline with { Owners = baseline.Owners with { NonPumpDescriptorTasks = 1 } },
            "contact" => baseline with { Owners = baseline.Owners with { PendingContacts = 1 } },
            "pointer" => baseline with { Owners = baseline.Owners with { ReturnedTaskPointer = 1 } },
            "detached" => baseline with { Owners = baseline.Owners with { DetachedReturnedTask = true } },
            "resource" => baseline with { Owners = baseline.Owners with { ReplacementResource = true } },
            "unknown-files" => baseline with { Owners = baseline.Owners with { Files = null } },
            "action" => baseline with { Owners = baseline.Owners with { ActionOrCommand = true } },
            _ => baseline with { Callback = 1 }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    [Fact]
    public void Fresh_driver_requires_release_and_retains_actual_global_blockers_during_outside_gameplay()
    {
        var recipe = new DiagnosticBattleDriverRecipe(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, FreshEntry = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("Data/battle.fs", new string('a', 64))]) };
        DiagnosticBattleDriver driver = new(recipe, 0);
        DiagnosticDriverObservation Observation(double seconds, long frame, bool drained) => new(seconds, frame, frame, true, drained, false, drained ? [] : ["action-or-command"]);
        driver.Advance(Observation(0, 0, true));
        Assert.True(driver.Advance(Observation(.1, 1, true)));
        BattleDiagnosticInvocationRelease receipt = Receipt();
        driver.ObserveDispatch(receipt.Dispatch);
        driver.Advance(Observation(.2, 2, true));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.ObserveInvocationRelease(receipt);
        driver.Advance(Observation(.3, 3, false));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        Assert.Equal(new[] { "action-or-command" }, driver.LastDrainBlockers);
        driver.ObserveDispatch(new(2, 4, "party-0", BattleAbility.Attack, 0, 9, 9));
        driver.Advance(Observation(.4, 4, false));
        driver.Advance(Observation(.5, 5, false));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Single(driver.Dispatches);
    }
}
