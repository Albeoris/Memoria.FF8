namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedTaskActorTests
{
    private static readonly BattlePartyActorModel Source = new(new("source"));
    private static readonly BattleEnemyActorModel Target = new(new("target"));

    [Fact]
    public void ActorPairWritesOriginalOperandsAndTargetOnlyLeavesSourceUntouched()
    {
        NativeBattleModelAliases aliases = CreateAliases();
        NativeBattleEffectTaskActors pair = new(Source, Target, aliases);
        NativeBattleModulePoolTask task = NewTask();
        task.WriteByte(0x2C, 0x77);

        Assert.Same(task, pair.WriteTargetTo(task));
        Assert.Equal((byte)0x77, task.ReadByte(0x2C));
        Assert.False(task.TryReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1, out _));
        Assert.Equal((byte)6, task.ReadByte(0x2D));
        Assert.Equal(Target, task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner);

        Assert.Same(task, pair.WriteTo(task));
        Assert.Equal(new byte[] { 2, 6 }, task.ReadBytes(0x2C, 2));
        Assert.Equal(Source, task.ReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1).Owner);
    }

    [Fact]
    public void UnaliasedTargetRetainsTypedIdentityAndLeavesItsOperandUnknown()
    {
        BattleEnemyActorModel[] extras = Enumerable.Range(0, 35).Select(index => new BattleEnemyActorModel(new($"extra-{index}"))).ToArray();
        NativeBattleModelAliases aliases = CreateAliases(extras);
        NativeBattleEffectTaskActors pair = new(Source, extras[^1], aliases);
        NativeBattleModulePoolTask task = pair.WriteTo(NewTask());

        Assert.Equal(37, aliases.Roster.Count);
        Assert.Equal(new[] { true, false }, task.ReadSnapshot(0x2C, 2).Known);
        Assert.Equal((byte)2, task.ReadByte(0x2C));
        Assert.Equal(extras[^1], task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner);
    }

    [Fact]
    public void ScalarAndSingleByteOverwriteAffectOnlyTheReachedField()
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectTaskActors(Source, Target, CreateAliases()).WriteTo(NewTask());
        task.WriteByte(0x2E, 0x1E);
        Assert.Equal(Source, task.ReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1).Owner);
        Assert.Equal(Target, task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner);

        task.WriteByte(0x2D, 6);
        Assert.Throws<InvalidDataException>(() => task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1));
        Assert.Equal(Source, task.ReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1).Owner);
        Assert.Equal(new byte[] { 2, 6, 0x1E }, task.ReadBytes(0x2C, 3));
    }

    [Fact]
    public void ChildAppendSelectsFromPacketsBeforeAllocationAndPreservesParentFields()
    {
        BattleEnemyActorModel extra = new(new("additional-target"));
        NativeBattleModelAliases aliases = CreateAliases([extra]);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(aliases.Roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
        (packets, _) = packets.Prepare(new(Source, 2, 1, 0, 1, 2, null));
        (packets, _) = packets.AppendContact(Contact(extra));
        (packets, _) = packets.Publish(1);
        (packets, BattleActionHeaderReference second) = packets.Prepare(new(Target, 2, 1, 0, 1, 2, null));
        (packets, _) = packets.AppendContact(Contact(Target));
        (packets, _) = packets.Publish(1);
        NativeBattleEffectParameters parameters = new(Target, 0, BattleModelTargets.From([Target]), second, 0, 0);

        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 2);
        NativeBattleEffectHierarchyAppend root = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x8D6B30, 100);
        NativeBattleModulePoolTask parent = root.Hierarchy.Read(root.Address);
        parent.WriteDword(0x0C, 0x1D99A78).WriteDword(0x1C, 0x12345678).WriteDword(0x20, 0x89ABCDEF).WriteByte(0x2A, 0xFF).WriteByte(0x2B, 0).WriteByte(0x2E, 7).WriteByte(0x2F, 8).WriteByte(0x28, 255);
        NativeBattleEffectHierarchyAppend appended = root.Hierarchy.Append8DC540(0, 0x8D6CC0, 88, root.Address, (address, group, contact) =>
        {
            Assert.Equal(0x1D99A78u, address);
            Assert.Equal((sbyte)-1, group);
            Assert.Equal((sbyte)0, contact);
            Assert.Equal((byte)255, parent.ReadByte(0x28));
            Assert.Null(parent.Next);
            Assert.Equal(0, pool.Slots[1].Flags & 1);
            NativeBattleEffectTaskActors actors = NativeBattleCureController.SelectActors(parameters, packets, address, group, contact);
            Assert.Equal(Source, actors.Actor);
            Assert.Equal(extra, actors.Target);
            Assert.Same(aliases, actors.Aliases);
            return actors;
        });

        NativeBattleModulePoolTask child = appended.Hierarchy.Read(appended.Address);
        Assert.Equal(0x8D6CC0u, child.Callback);
        Assert.Equal(root.Address, child.ReadDword(0x10));
        Assert.Equal(appended.Address, child.ReadDword(0x14));
        Assert.Equal(root.Address, child.ReadDword(0x18));
        Assert.Equal(0x12345678u, child.ReadDword(0x1C));
        Assert.Equal(0x89ABCDEFu, child.ReadDword(0x20));
        Assert.Equal(new byte[] { 0xFF, 0 }, child.ReadBytes(0x2A, 2));
        Assert.Equal((byte)2, child.ReadByte(0x2C));
        Assert.Equal(new[] { true, false }, child.ReadSnapshot(0x2C, 2).Known);
        Assert.Equal(Source, child.ReadExecutionReference<BattleExecutionReference.Model>(0x2C, 1).Owner);
        Assert.Equal(extra, child.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner);
        Assert.Equal(new byte[] { 7, 8 }, child.ReadBytes(0x2E, 2));
        Assert.Equal((byte)0, parent.ReadByte(0x28));
        Assert.Equal(1, parent.Next);
    }

    private static NativeBattleModulePoolTask NewTask() => NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1).Slots[0];

    private static NativeBattleModelAliases CreateAliases(IEnumerable<BattleModelOwner>? extras = null)
    {
        BattleModelRoster roster = new(new BattleModelOwner[] { Source, Target }.Concat(extras ?? []));
        return new(roster, [(NativeBattleModelSlot.Decode(2), Source), (NativeBattleModelSlot.Decode(6), Target)]);
    }

    private static NativeBattleContactPacket Contact(BattleModelOwner owner) => new(new(owner, 0, 0, 0, 0, 0, 0), null);
}
