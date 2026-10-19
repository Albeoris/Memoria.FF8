namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112TargetTests
{
    [Theory]
    [InlineData(399, 0)]
    [InlineData(1400, 1000)]
    public void TargetInitializationBindsTheInlineRowBeforeSequenceAndPreservesPackedActorFields(short scale, uint expectedRadius)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
        (targets, _) = targets.Append(0x6CBF90);
        NativeBattleEffectParameters parameters = new(0, 0, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, 0x2521960) { Pools = [root, targets] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Duel112 = new(true, parameters, null) };
        NativeBattleDuel112Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));
        NativeBattleGraphicsModel actor = new(0, 3) { Transform = new(new(1, 2, 3), new(10, -11, 12), -13, 14, 4096), Color40 = 0x11223344, Color44 = 0x55667788, Alpha7 = 0x99 };
        NativeBattleGraphicsModel target = new(3, 3) { Transform = new(new(0, 0, 0), new(20, 4, 12), 0, 0, scale) };
        List<string> calls = [];
        NativeBattleDuel112ActorServices services = new(index => index == 0 ? actor : target, (index, model) =>
        {
            Assert.Equal(0, index);
            actor = model;
        }, sequence =>
        {
            Assert.Equal(2, sequence);
            return 3;
        }, (current, index, row) =>
        {
            calls.Add("bind");
            Assert.Equal(0, index);
            Assert.Equal(0x20DFAC8u, row);
            Assert.Equal((ushort)0x1003, actor.Flags0);
            Assert.Equal((ushort)2, current.Effect.Pools[1].Slots[0].ReadWord(14));
            Assert.Equal(0x1D972C0u + 156u * 3, current.Effect.Pools[1].Slots[0].ReadDword(176));
            return current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, row, new(new(0, 0x123), null)) } };
        }, (current, index, sequence) =>
        {
            calls.Add("sequence");
            Assert.Equal(0, index);
            Assert.Equal((byte)1, sequence);
            actor = actor with { Sequence = sequence };
            return current;
        });
        NativeBattleDuel112Frame result = NativeBattleDuel112Target.Initialize6CBF90(frame, 0, 2, services);
        NativeBattleModulePoolTask task = result.Effect.Pools[1].Slots[0];
        Assert.Equal(new[] { "bind", "sequence" }, calls);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), task.ReadResourcePointer(16).Cpu);
        Assert.Equal(expectedRadius, task.ReadDword(172));
        Assert.Equal(0xFFF5000Au, task.ReadDword(60));
        Assert.Equal(0xFFF3000Cu, task.ReadDword(64));
        Assert.Equal(0x11223344u, task.ReadDword(48));
        Assert.Equal(0x99667788u, task.ReadDword(52));
        Assert.Equal((short)-1024, actor.Rotation14);
        Assert.Equal(new NativeBattleVector(0, -11, 1000), actor.Transform!.Position28);
        Assert.Equal((short)-13, actor.Transform.Vertical34);
        Assert.Equal((byte)1, actor.Sequence);
        Assert.Equal((ushort)0, task.ReadWord(12));
    }
}
