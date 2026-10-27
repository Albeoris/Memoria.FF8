using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112BirthTests
{
    [Fact]
    public void OpeningBirthsUseNativeTaskBanksRandomOrderAndActorWord36ForSpriteY()
    {
        NativeBattleDuel112Fixture fixture = new(10);
        NativeBattleDuel112Frame frame = NativeBattleDuel112Births.Early6CBF90(fixture.Frame, 0, new(100, 200, 300), fixture.Actor.Transform!);
        Assert.Equal(12, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(8, frame.Effect.Pools[3].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(frame.Effect.Pools[3].Slots.Take(8), task =>
        {
            Assert.Equal(0x6CD750u, task.Callback);
            Assert.Equal((ushort)17, task.ReadWord(22));
            Assert.Equal((ushort)0, task.ReadWord(30));
            Assert.Equal((ushort)4096, task.ReadWord(34));
            Assert.Null(NativeBattleStorageSnapshots.Capture(task).UnknownPayloadRanges);
        });
        Assert.Equal(Draw(fixture.Frame.Random, 45), frame.Random);
    }

    [Fact]
    public void EarlyDebrisRetainsTheActuallyUnknownCopiedStackWordAcrossSnapshot()
    {
        NativeBattleDuel112Fixture fixture = new(12);
        NativeBattleDuel112Frame frame = NativeBattleDuel112Births.Early6CBF90(fixture.Frame, 0, new(11, 22, 33), fixture.Actor.Transform!);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel112Frame>(NativeBattleTestSnapshots.Json(frame))!;
        Assert.Equal(2, frame.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(frame.Effect.Pools[4].Slots.Take(2), task =>
        {
            Assert.Equal((ushort)10, task.ReadWord(16));
            Assert.Equal((ushort)20, task.ReadWord(18));
            Assert.Equal((ushort)30, task.ReadWord(20));
            Assert.Throws<NotSupportedException>(() => task.ReadWord(22));
            Assert.Equal((ushort)0, task.ReadWord(26));
        });
        Assert.Equal(Draw(fixture.Frame.Random, 20), frame.Random);
    }

    [Fact]
    public void ExhaustedBanksKeepOnlyTheRandomDrawsNativeMakesBeforeTryingAllocation()
    {
        NativeBattleDuel112Fixture opening = new(10);
        opening.Fill(2, 0x6CD5C0);
        opening.Fill(3, 0x6CD750);
        NativeBattleDuel112Frame frame = NativeBattleDuel112Births.Early6CBF90(opening.Frame, 0, new(1, 2, 3), opening.Actor.Transform!);
        Assert.Equal(Draw(opening.Frame.Random, 1), frame.Random);
        NativeBattleDuel112Fixture late = new(110);
        late.Fill(3, 0x6CD750);
        frame = NativeBattleDuel112Births.Late6CBF90(late.Frame, 0, 0);
        Assert.Equal(Draw(late.Frame.Random, 3), frame.Random);
    }

    [Fact]
    public void FinalDebrisUsesWordStoresSoItsClearedPaddingIsKnownAndDrawsOnlyForSuccessfulAllocations()
    {
        NativeBattleDuel112Fixture fixture = new(71);
        NativeBattleDuel112Frame frame = NativeBattleDuel112Births.Late6CBF90(fixture.Frame, 0, 700);
        Assert.Equal(16, frame.Effect.Pools[4].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(frame.Effect.Pools[4].Slots, task => Assert.Equal((ushort)0, task.ReadWord(22)));
        Assert.Equal(Draw(fixture.Frame.Random, 176), frame.Random);
        fixture.Fill(4, 0x6CD640);
        frame = NativeBattleDuel112Births.Late6CBF90(fixture.Frame, 0, 700);
        Assert.Equal(fixture.Frame.Random, frame.Random);
    }

    private static NativeCrtRandomState Draw(NativeCrtRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw(out _);
        }
        return state;
    }
}
