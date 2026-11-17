using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRingSequenceSchedulingTests
{
    private static readonly BattlePartyActorModel Party = new(new("party"));
    private static readonly BattleEnemyActorModel Enemy = new(new("enemy"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_completion_schedules_the_whole_ring_even_when_the_source_lacks_the_requested_sequence(bool missingSourceSequence)
    {
        var fixture = Fixture();
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(2, 0, 6, 0, missingSourceSequence ? (byte)0 : (byte)7, 0, 0xA1, 0xA1));
        NativeBattleGraphicsModel source = fixture.Graphics.ActorModels.Get(Party) with { Sequence = 1, ResourceFlags44 = 0x40, SequenceState = new() { Pending3 = 2, Pose = 0, PoseFrame114 = 0, PoseLimit115 = 0 } };
        source = source.WriteLinkedModel(Enemy);
        NativeBattleGraphicsModel companion = (fixture.Graphics.ActorModels.Get(Enemy) with { Flags0 = 2, ModelId4 = 98, ResourceFlags44 = 0x40, Sequence = 1, SequenceState = source.SequenceState! with { Pending3 = 0 } }).WriteLinkedModel(Party);
        NativeBattleGraphicsState graphics = fixture.Graphics with { ActorModels = fixture.Graphics.ActorModels.Set(Party, source).Set(Enemy, companion) };
        int requests = 0;
        NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Party, source, resource, new(null, null)
        {
            SchedulePendingSequence = (current, requested) =>
            {
                requests++;
                Assert.Equal((byte)2, requested);
                Assert.Equal((byte)0, current.SequenceState!.Callback42);
                graphics = Schedule(fixture.Session, graphics with { ActorModels = graphics.ActorModels.Set(Party, current) }, requested);
                return graphics.ActorModels.Get(Party);
            }
        });
        Assert.Equal(1, requests);
        Assert.Equal((byte)1, step.Model.Sequence);
        Assert.Equal((byte)2, step.Model.SequenceState!.Pending3);
        Assert.Equal((byte)1, step.Model.SequenceState.Callback42);
        Assert.Equal(0, step.Model.ResourceFlags44!.Value & 0x40);
        Assert.Equal(0, graphics.ActorModels.Get(Enemy).ResourceFlags44!.Value & 0x40);
        NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[graphics.Auxiliary.Head!.Value];
        Assert.Equal(NativeBattleAuxiliaryKind.ModelSequence505C70, task.Kind);
        graphics.Auxiliary.Advance(new()
        {
            ApplyModelRing = (_, model, requested) =>
            {
                foreach (BattleModelOwner member in NativeBattleModelRings.Walk(graphics.ActorModels, model))
                {
                    NativeBattleGraphicsResource selectedResource = member == Party ? resource : resource with { SequenceSection = resource.SequenceSection.SetItem(4, 7) };
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(member, NativeBattleSequenceSelections.SelectResource505BC0(graphics.ActorModels.Get(member), selectedResource, (int)requested)) };
                }
            }
        });
        Assert.Equal(missingSourceSequence ? (byte)1 : (byte)2, graphics.ActorModels.Get(Party).Sequence);
        Assert.Equal((byte)2, graphics.ActorModels.Get(Enemy).Sequence);
        Assert.Equal((ushort)0, graphics.ActorModels.Get(Enemy).SequenceState!.Offset46);
        Assert.Equal((byte)0, graphics.ActorModels.Get(Enemy).SequenceState!.Callback42);
    }

    [Fact]
    public void Pending_completion_without_a_ring_uses_immediate_selection_and_respects_status_admission()
    {
        var fixture = Fixture();
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(3, 0, 8, 0, 9, 0, 10, 0, 0xA1, 0xA1, 0xA1));
        foreach (uint status in new uint[] { 0, 0x18 })
        {
            NativeBattleGraphicsModel source = fixture.Graphics.ActorModels.Get(Party) with { Flags8 = status, Sequence = 1, ResourceFlags44 = 0x40, SequenceState = new() { Pending3 = 2, Pose = 0, PoseFrame114 = 0, PoseLimit115 = 0 } };
            NativeBattleSequenceStep step = NativeBattleSequenceMachine.Advance(Party, source, resource, new(null, null)
            {
                SchedulePendingSequence = (current, requested) => Schedule(fixture.Session, fixture.Graphics with { ActorModels = fixture.Graphics.ActorModels.Set(Party, current) }, requested).ActorModels.Get(Party)
            });
            Assert.Equal(status == 0 ? (byte)2 : (byte)1, step.Model.Sequence);
            Assert.Equal(status == 0 ? (byte)0 : (byte)2, step.Model.SequenceState!.Pending3);
            Assert.Equal((byte)1, step.Model.SequenceState.Callback42);
        }
    }

    [Fact]
    public void Null_ring_selects_current_resource_immediately_without_allocating_an_auxiliary_callback()
    {
        var fixture = Fixture();
        NativeBattleGraphicsState result = Schedule(fixture.Session, fixture.Graphics, 1);
        Assert.Equal((byte)1, result.ActorModels.Get(Party).Sequence);
        Assert.Equal((ushort)1, result.ActorModels.Get(Party).ResourceFlags44);
        Assert.Equal((ushort)0, result.ActorModels.Get(Party).SequenceState!.Offset46);
        Assert.Equal((byte)0, result.ActorModels.Get(Party).SequenceState!.Pending3);
        Assert.Equal((byte)29, result.ActorModels.Get(Party).SequenceState!.Pose);
        Assert.Null(result.Auxiliary.Head);
    }

    [Fact]
    public void Actual_ring_defers_selection_and_clears_ready40_on_each_loaded_member()
    {
        var fixture = Fixture();
        BattleActorModels models = fixture.Graphics.ActorModels;
        models = models.Set(Party, models.Get(Party).WriteLinkedModel(Enemy));
        models = models.Set(Enemy, (models.Get(Enemy) with { Flags0 = 2, ResourceFlags44 = 0x45 }).WriteLinkedModel(Party));
        NativeBattleGraphicsState result = Schedule(fixture.Session, fixture.Graphics with { ActorModels = models }, 0x1001);
        Assert.Equal((byte)2, result.ActorModels.Get(Party).Sequence);
        Assert.Equal((ushort)2, result.ActorModels.Get(Party).ResourceFlags44);
        Assert.Equal((ushort)5, result.ActorModels.Get(Enemy).ResourceFlags44);
        NativeBattleAuxiliaryTask task = result.Auxiliary.Slots[result.Auxiliary.Head!.Value];
        Assert.Equal(NativeBattleAuxiliaryKind.ModelSequence505C70, task.Kind);
        List<uint> requested = [];
        result.Auxiliary.Advance(new() { ApplyModelRing = (_, _, value) => requested.Add(value) });
        Assert.Equal(new uint[] { 1 }, requested);
    }

    [Theory]
    [InlineData(0, 0x18u, false)]
    [InlineData(1, 0x08u, false)]
    [InlineData(1, 0x10u, false)]
    [InlineData(1, 0x01u, true)]
    [InlineData(0x1001, 0x18u, true)]
    public void Requested_zero_status_bits_and_override_follow_native505C00(int requested, uint status, bool selected)
    {
        var fixture = Fixture();
        NativeBattleGraphicsState graphics = fixture.Graphics with { ActorModels = fixture.Graphics.ActorModels.Set(Party, fixture.Graphics.ActorModels.Get(Party) with { Flags8 = status }) };
        NativeBattleGraphicsState result = Schedule(fixture.Session, graphics, requested);
        Assert.Equal(selected ? (byte)1 : (byte)2, result.ActorModels.Get(Party).Sequence);
        Assert.Null(result.Auxiliary.Head);
    }

    [Fact]
    public void Unknown_ring_owner_is_not_treated_as_a_null_ring()
    {
        var fixture = Fixture();
        NativeBattleGraphicsState graphics = fixture.Graphics with { ActorModels = fixture.Graphics.ActorModels.Set(Party, fixture.Graphics.ActorModels.Get(Party) with { HasLinkedModelRing140 = null }) };
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Schedule(fixture.Session, graphics, 1));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture()
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(3, 0, 8, 0, 9, 0, 10, 0, 0xA1, 0xA1, 0xA1));
        NativeBattleGraphicsModel party = new NativeBattleGraphicsModel(3) { Flags8 = 1, ModelId4 = 3, ResourceFlags44 = 0x42, Sequence = 2, SequenceState = new() { Offset46 = 7, Pending3 = 2, Pose = 29 } }.WriteLinkedModel(null);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, Party, party), (3, Enemy, new(0))]);
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(null!, [new("party", 3, resource)], []));
        return (session, graphics);
    }

    private static NativeBattleGraphicsState Schedule(BattleSession session, NativeBattleGraphicsState graphics, int requested)
    {
        return (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod("ScheduleNativeRingSequence505C00", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session.Presentation!, [graphics, Party, requested])!;
    }
}
