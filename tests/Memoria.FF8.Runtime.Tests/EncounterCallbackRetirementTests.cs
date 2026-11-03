namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterCallbackRetirementTests
{
    [Fact]
    public void RetiredAllocationCallbacksCannotReachAReusedPhysicalSlot()
    {
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        banks = Queue(banks, 4, "old-copy", 1);
        banks = Queue(banks, 3, "original", 2);
        banks = Queue(banks, 4, "old-copy", 3);
        banks = Queue(banks, 4, "replacement", 0);
        banks = banks.RemoveActorCallbacks(4, "old-copy");
        Assert.DoesNotContain(banks.BoundCommands, command => command.ActorId == "old-copy");
        NativeBattleCommandLocation first = banks.FirstEligible(_ => false)!;
        Assert.Equal("original", banks.Banks[first.Bank].LogicalCommands[2 * first.Slot]!.ActorId);
        banks = banks.Release(first);
        NativeBattleCommandLocation replacement = banks.FirstEligible(_ => false)!;
        BattleAttackCommand nextCommand = banks.Banks[replacement.Bank].LogicalCommands[2 * replacement.Slot]!;
        Assert.Equal("replacement", nextCommand.ActorId);
        Assert.Equal((ushort)0, nextCommand.NativeCallback!.Stage);
        Assert.Null(banks.Release(replacement).FirstEligible(_ => false));
    }

    [Fact]
    public void UnboundPhysicalCallbacksAreRemovedWithoutChangingOtherBanks()
    {
        NativeBattleCommandBanks banks = NativeBattleCommandBanks.Initialize();
        (banks, _) = banks.EnqueueCallback(0, 5, 2);
        (banks, NativeBattleCommandLocation ordinary) = banks.Allocate(2);
        banks = banks.WriteCommand(ordinary, 0, 5, 8, 0, 0, 1, []);
        NativeBattleCommandBank preserved = banks.Banks[2];
        banks = banks.RemoveActorCallbacks(5, "copy");
        Assert.Same(preserved, banks.Banks[2]);
        Assert.Equal(ordinary, banks.FirstEligible(_ => false));
        Assert.Equal(banks, banks.RemoveActorCallbacks(5, "copy"));
    }

    [Fact]
    public void HistoricalAllocationRemovalKeepsUnknownPhysicalAndNewGenerationCallbacks()
    {
        NativeBattleCommandBanks banks = Queue(NativeBattleCommandBanks.Initialize(), 4, "old-copy", 1);
        (banks, NativeBattleCommandLocation raw) = banks.EnqueueCallback(0, 4, 2);
        banks = Queue(banks, 4, "replacement", 0);
        banks = banks.RemoveAllocationCallbacks("old-copy");
        Assert.Equal(raw, banks.FirstEligible(_ => false));
        Assert.DoesNotContain(banks.BoundCommands, command => command.ActorId == "old-copy");
        Assert.Equal("replacement", Assert.Single(banks.BoundCommands).ActorId);
        Assert.Equal(banks, banks.RemoveAllocationCallbacks("old-copy"));
    }

    private static NativeBattleCommandBanks Queue(NativeBattleCommandBanks banks, byte actor, string id, ushort stage)
    {
        (banks, NativeBattleCommandLocation location) = banks.EnqueueCallback(0, actor, stage);
        return banks.BindCommand(location, 0, new(id, id, BattleAbility.EnemyAbility, 0) { NativeCallback = new(actor, stage) });
    }
}
