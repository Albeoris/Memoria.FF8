namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112ActorTests
{
    [Theory]
    [InlineData(-32768, -9999)]
    [InlineData(-1, -256)]
    [InlineData(0, 0)]
    [InlineData(3, 768)]
    [InlineData(4, 1024)]
    [InlineData(148, 1024)]
    [InlineData(149, 768)]
    [InlineData(152, 0)]
    [InlineData(153, -256)]
    [InlineData(32767, -9999)]
    public void StageIntensityUsesSignedWordAndTheActualFourFrameRamps(short phase, int expected)
    {
        Assert.Equal(expected, NativeBattleDuel112Target.StageIntensity6CBF90(phase));
    }

    [Fact]
    public void TrajectoryDividesTheFullDwordProductBeforeItsWordStoreAndReusesTheSavedAnchor()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
        (target, _) = target.Append(0x6CBF90);
        target = target with { Slots = target.Slots.SetItem(0, target.Slots[0].WriteWord(12, 45).WriteDword(176, 0x1D972C0 + 156 * 3)) };
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, target] };
        NativeBattleDuel112Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Duel112 = new(true, parameters, null) }, effect, NativeBattleFileStaging.Unknown(), new(1));
        NativeBattleGraphicsModel actor = new(0, 3) { Rotation14 = 1024, Transform = new(new(0, 0, 0), new(1, 2, 3), 93, 17, 4096) };
        NativeBattleGraphicsModel enemy = new(3, 3) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 1000) };
        NativeBattleDuel112ActorServices services = new(index => index == 0 ? actor : enemy, (_, model) => actor = model, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException());
        frame = NativeBattleDuel112Target.Trajectory6CBF90(frame, 0, services, index =>
        {
            Assert.Equal(3, index);
            return new(10, 20, 30, 77);
        });
        Assert.Equal(new NativeBattleVector(10202, -8515, 30), actor.Transform!.Position28);
        Assert.Equal((short)93, actor.Transform.Vertical34);
        Assert.Equal((ushort)2010, frame.Effect.Pools[1].Slots[0].ReadWord(68));
        Assert.Equal((ushort)77, frame.Effect.Pools[1].Slots[0].ReadWord(74));
        target = frame.Effect.Pools[1];
        target = target with { Slots = target.Slots.SetItem(0, target.Slots[0].WriteWord(12, 49)) };
        frame = NativeBattleDuel112Target.Trajectory6CBF90(frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, target) } }, 0, services, _ => throw new InvalidOperationException("The anchor is loaded only at phase45."));
        Assert.Equal(new NativeBattleVector(2010, -323, 30), actor.Transform.Position28);
        Assert.Equal((ushort)49, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }
}
