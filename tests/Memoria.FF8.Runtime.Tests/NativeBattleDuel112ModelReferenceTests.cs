namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112ModelReferenceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TargetInitializationWritesTypedOwnerBeforeCallbacksAndTrajectoryUsesIt(bool originalAlias)
    {
        BattlePartyActorModel source = new(new("source"));
        BattleEnemyActorModel recipient = new(new("recipient"));
        NativeBattleGraphicsModel actor = new(3)
        {
            Transform = new(new(0, 0, 0), new(10, -11, 12), -13, 14, 4096),
            Color40 = 0x11223344,
            Color44 = 0x55667788,
            Alpha7 = 0x99
        };
        NativeBattleGraphicsModel target = new(3) { Transform = new(new(0, 0, 0), new(20, 4, 12), 0, 0, 1400) };
        NativeBattleModelBoundary boundary = new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
        BattleActorModels models = new(Enumerable.Range(0, 35).Select(index => new BattleModelEntry<BattleModelOwner>(new BattleEnemyActorModel(new($"extra-{index}")), new(0))));
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), source, models);
        if (originalAlias)
        {
            (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), recipient, models);
            models = models.Set(recipient, target);
        }
        else
        {
            models = models.Add(recipient, target);
        }
        models = models.Set(source, actor);

        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(0x2521950, 0x20DFAB8, 0xF48, 1);
        targets.Append(0x6CBF90);
        NativeBattleEffectParameters parameters = new(source, 0, BattleModelTargets.From([recipient]), new(0), 0, 0);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), parameters, root.DescriptorAddress) { Pools = [root, targets] };
        NativeBattleDuel112Frame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, NativeBattleFileStaging.Unknown(), new(1));
        List<string> calls = [];
        NativeBattleDuel112ActorServices services = new(owner => models.Get(owner), (owner, model) => models = models.Set(owner, model), group =>
        {
            Assert.Equal(2, group);
            Assert.Equal((ushort)0x1003, models.Get(source).Flags0);
            return recipient;
        }, (current, owner, row) =>
        {
            Assert.Equal(source, owner);
            Assert.Equal(0x20DFAC8u, row);
            Assert.Equal(recipient, current.Effect.Pools[1].Slots[0].ReadExecutionReference<BattleExecutionReference.Model>(176, 4).Owner);
            Assert.Equal((ushort)2, current.Effect.Pools[1].Slots[0].ReadWord(14));
            calls.Add("bind");
            return current;
        }, (current, owner, sequence) =>
        {
            Assert.Equal(source, owner);
            Assert.Equal((byte)1, sequence);
            calls.Add("sequence");
            return current;
        }, boundary.CaptureAliases(models));

        frame = NativeBattleDuel112Target.Initialize6CBF90(frame, 0, 2, services);
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[0];
        Assert.Equal(new[] { "bind", "sequence" }, calls);
        Assert.All(task.ReadSnapshot(176, 4).Known, known => Assert.Equal(originalAlias, known));
        if (originalAlias)
        {
            Assert.Equal(0x1D97494u, task.ReadDword(176));
        }
        task.WriteWord(12, 45);
        frame = NativeBattleDuel112Target.Trajectory6CBF90(frame, 0, services, owner =>
        {
            Assert.Equal(recipient, owner);
            return new(100, 200, 300, 4);
        });
        Assert.Equal((ushort)4, frame.Effect.Pools[1].Slots[0].ReadWord(74));

        // The numeric address is no substitute for the model reference once overwritten.
        task.WriteDword(176, 0x1D97494);
        Assert.Throws<InvalidDataException>(() => NativeBattleDuel112Target.Trajectory6CBF90(frame, 0, services, _ => throw new InvalidOperationException("No target lookup is allowed after overwriting its reference.")));
    }
}
