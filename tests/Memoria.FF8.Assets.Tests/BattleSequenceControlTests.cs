using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleSequenceControlTests
{
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
