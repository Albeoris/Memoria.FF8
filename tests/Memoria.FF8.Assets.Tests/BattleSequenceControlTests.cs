using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleSequenceControlTests
{
    [Fact]
    public void LinkOpcodeBytesInsideSoundAndExpressionOperandsDoNotPreventIndependentCloning()
    {
        BattleSequenceSet sequences = new([1, 0, 4, 0, 0xB5, 0x90, 2, 0x90, 0xC4, 0x90, 0x93, 0xA0, 3, 0xA2]);
        BattleSequence projection = sequences.Read(1);
        Assert.Equal(new[] { 3 }, projection.Animations);
        Assert.Equal(new BattleSequenceSound(0, 0x90, true, 0x9000), Assert.Single(projection.Sounds));
        Assert.False(projection.RequiresOriginalModelBindings);
        Assert.True(sequences.CanCloneIndependently());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(123)]
    public void ActualModelSequenceSelectorsPreventIndependentCloning(int selector)
    {
        BattleSequenceSet sequences = new([1, 0, 4, 0, 0x93, (byte)selector, 0x90, 0xA2]);
        Assert.True(sequences.Read(1).RequiresOriginalModelBindings);
        Assert.False(sequences.CanCloneIndependently());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(123)]
    public void SubsequenceJumpsCarryOriginalModelLinkRequirements(int selector)
    {
        BattleSequenceSet sequences = new([2, 0, 6, 0, 8, 0, 0xA7, 2, 0x90, (byte)selector, 0xA2]);
        Assert.True(sequences.Read(1).RequiresOriginalModelBindings);
        Assert.True(sequences.Read(2).RequiresOriginalModelBindings);
        Assert.False(sequences.CanCloneIndependently());
    }

    [Fact]
    public void ConditionalBranchesInspectLinkInstructionsBeyondTheFallthroughTerminator()
    {
        BattleSequenceSet conditional = new([1, 0, 4, 0, 0xE7, 3, 0xA2, 0x90, 123, 0xA2]);
        Assert.False(conditional.Read(1).RequiresOriginalModelBindings);
        Assert.True(conditional.Read(1).HasUnresolvedBranches);
        Assert.False(conditional.CanCloneIndependently());

        BattleSequenceSet branch = new([1, 0, 4, 0, 0xE7, 3, 0xA2, 0x91, 0x90, 0, 0xA2]);
        Assert.False(branch.Read(1).RequiresOriginalModelBindings);
        Assert.True(branch.CanCloneIndependently());
    }

    [Fact]
    public void UnknownInstructionsDeclineIndependentCloningWhileHarmlessSequenceCyclesRemainEligible()
    {
        BattleSequenceSet cycle = new([2, 0, 6, 0, 8, 0, 0xA7, 2, 0xA7, 1]);
        Assert.Throws<NotSupportedException>(() => cycle.Read(1));
        Assert.True(cycle.CanCloneIndependently());

        BattleSequenceSet unsupported = new([1, 0, 4, 0, 0xF4, 0xA2]);
        Assert.Throws<NotSupportedException>(() => unsupported.Read(1));
        Assert.False(unsupported.CanCloneIndependently());
    }

    [Theory]
    [InlineData(0xA3)]
    [InlineData(0xA9)]
    public void DonationInspectionContinuesPastProjectionOnlyTerminators(int opcode)
    {
        BattleSequenceSet sequences = new([1, 0, 4, 0, (byte)opcode, 0x90, 0, 0xA2]);
        Assert.False(sequences.Read(1).RequiresOriginalModelBindings);
        Assert.False(sequences.CanCloneIndependently());
    }

    [Fact]
    public void CorruptSequenceDataStillFailsDonorAdmissionExplicitly()
    {
        BattleSequenceSet corrupt = new([2, 0, 6, 0, 9, 0, 0x90, 0, 0xA2, 0x90]);
        Assert.Throws<InvalidDataException>(() => corrupt.CanCloneIndependently());
    }

    [Fact]
    public void LocalWordCounterBranchesEmitEachMessageOnceAndReachNativeAaCompletion()
    {
        //A local counter loop from original model61 sequence14, independent of actor identity or text contents.
        byte[] program = Convert.FromHexString("C100E500BBA000C300E57FC100CB7FEA0791031EE610C300E57FC11ECB7FEA06E60DE602A1C300C501E500E6DAAAA2");
        BattleSequenceSet sequences = new([1, 0, 4, 0, .. program]);
        NativeBattleSequenceTrack track = Assert.IsType<NativeBattleSequenceTrack>(sequences.ReadLocalControlTrack(1));
        Assert.Equal(30, track.CompletionFrame);
        NativeBattleSequenceMessage message = Assert.Single(track.Messages);
        Assert.Equal(new NativeBattleSequenceMessage(0, 3, 30), message);
        Assert.Equal(program, track.Program);
    }

    [Fact]
    public void NativeActorPredicatesAndUninitializedLocalsAreNotInvented()
    {
        BattleSequenceSet external = new([1, 0, 4, 0, 0xC3, 0x11, 0xAA, 0xA2]);
        BattleSequenceSet uninitialized = new([1, 0, 4, 0, 0xC3, 0, 0xAA, 0xA2]);
        Assert.Null(external.ReadLocalControlTrack(1));
        Assert.Null(uninitialized.ReadLocalControlTrack(1));
    }
}
