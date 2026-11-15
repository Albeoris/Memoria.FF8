using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsRosterStateTests
{
    [Fact]
    public void OriginalGraphicsExposeOnlyBoundActorsAndRetainUnboundCellsSeparately()
    {
        var graphics = OriginalGraphics([(1, OriginalOwner(1), new(0)), (3, OriginalOwner(3), new(0))]);
        graphics.Validate();
        Assert.Equal(new[] { OriginalOwner(1), OriginalOwner(3) }, graphics.ActorModels.Owners);
        Assert.Equal(BattleSide.Allies, graphics.NativeBindings.DecodeLiteral(1, graphics.ActorModels).Side);
        Assert.Equal(BattleSide.Enemies, graphics.NativeBindings.DecodeLiteral(3, graphics.ActorModels).Side);
        Assert.Null(graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(2), graphics.ActorModels));
        Assert.Equal((ushort)0, ReadOriginalModelCell(graphics, 2).Flags0);
        Assert.Throws<InvalidDataException>(() => graphics.NativeBindings.DecodeLiteral(2, graphics.ActorModels));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(25)]
    public void ExpandedGraphicsKeepExplicitActorRolesAndOrder(int count)
    {
        NativeBattleGraphicsState graphics = ExpandedGraphics(count);
        graphics.Validate();
        BattleModelOwner[] owners = graphics.ActorModels.Owners.ToArray();
        Assert.Equal(count, owners.Length);
        Assert.Equal(count, graphics.ActorModels.Roster.Count);
        Assert.Equal(BattleSide.Enemies, owners[0].Side);
        Assert.Equal(BattleSide.Allies, owners[^1].Side);
        Assert.Null(graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(1), graphics.ActorModels));
        Assert.Throws<ArgumentException>(() => graphics.ActorModels.Add(new BattlePartyActorModel(new("enemy-0")), new(0)));
    }

    [Fact]
    public void GraphicsValidationRejectsRetiredOwnersStillBoundToOriginalSlots()
    {
        NativeBattleGraphicsState graphics = OriginalGraphics([(1, OriginalOwner(1), new(0))]);
        NativeBattleGraphicsState removed = graphics with { ActorModels = graphics.ActorModels.Remove(OriginalOwner(1)) };
        Assert.Throws<InvalidDataException>(removed.Validate);
    }

    [Fact]
    public void ExpandedLoadedModelsRequireResourceAndSequenceFacts()
    {
        NativeBattleGraphicsState graphics = ExpandedGraphics(8);
        BattleModelOwner owner = graphics.ActorModels.Owners.Last();
        NativeBattleGraphicsModel loaded = new(2) { Flags2 = 4, Flags8 = 0, ResourceFlags44 = 1, SequenceState = new() };
        BattleActorModels loadedModels = graphics.ActorModels.Set(owner, loaded);
        (graphics with { ActorModels = loadedModels }).Validate();
        Assert.Throws<InvalidDataException>((graphics with { ActorModels = loadedModels.Set(owner, loaded with { ResourceFlags44 = null }) }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActorModels = loadedModels.Set(owner, loaded with { SequenceState = new() { Locals = [] } }) }).Validate);
    }

    [Fact]
    public void ExpandedGraphicsRetainOriginalTransformFieldsWithoutCreatingActorsForHoles()
    {
        NativeBattleGraphicsState graphics = ExpandedGraphics(25);
        ImmutableArray<NativeBattleModelRetainedFields> retained = Enumerable.Range(0, 7).Select(slot =>
            NativeBattleModelRetainedFields.Capture(new(ushort.MaxValue) { Flags2 = (ushort)(0x8000 + slot), Rotation14 = (short)(200 + slot), Color44 = (uint)(100 + slot) })).ToImmutableArray();
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.FromColdProcess() with { ModelTransformRows = retained };
        graphics = graphics with { NativeBindings = NativeBattleModelBoundary.FromRetainedMemory(memory) };
        Assert.Equal(25, graphics.ActorModels.Roster.Count);
        for (int slot = 0; slot < 7; slot++)
        {
            NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, slot);
            Assert.Equal((ushort)0, model.Flags0);
            Assert.Equal((ushort)(0x8000 + slot), model.Flags2);
            Assert.Equal((short)(200 + slot), model.Rotation14);
            Assert.Equal((uint)(100 + slot), model.Color44);
            Assert.Null(ReadOriginalModelActorId(graphics, slot));
        }
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            Assert.Equal((ushort)0, entry.State.Flags0);
            Assert.Null(entry.State.Flags2);
            Assert.Null(entry.State.Rotation14);
            Assert.Null(entry.State.Color44);
            Assert.Null(entry.State.Color40);
            Assert.Null(entry.State.Transform);
            Assert.Null(entry.State.GeometryWorldMatrix);
            Assert.Null(entry.State.SequenceState);
            Assert.Equal(32, entry.State.StatusTimers.Length);
            Assert.All(entry.State.StatusTimers, value => Assert.Null(value));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleModelBoundary.FromRetainedMemory(memory with { ModelTransformRows = retained.RemoveAt(6) }));
    }

    private static NativeBattleGraphicsState ExpandedGraphics(int count)
    {
        BattleModelEntry<BattleModelOwner>[] entries = [.. Enumerable.Range(0, count - 1).Select(index => new BattleModelEntry<BattleModelOwner>(new BattleEnemyActorModel(new($"enemy-{index}")), new(0))), new(new BattlePartyActorModel(new("party")), new(0))];
        return new(new(entries), NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
    }
}
