using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRingSequenceSchedulingTests
{
    [Fact]
    public void Null_ring_selects_current_resource_immediately_without_allocating_an_auxiliary_callback()
    {
        var fixture = Fixture();
        NativeBattleGraphicsState result = Schedule(fixture.Session, fixture.Graphics, 1);
        Assert.Equal((byte)1, result.Models[0].Sequence);
        Assert.Equal((ushort)1, result.Models[0].ResourceFlags44);
        Assert.Equal((ushort)0, result.Models[0].SequenceState!.Offset46);
        Assert.Equal((byte)0, result.Models[0].SequenceState!.Pending3);
        Assert.Equal((byte)29, result.Models[0].SequenceState!.Pose);
        Assert.Null(result.Auxiliary.Head);
    }

    [Fact]
    public void Actual_ring_defers_selection_and_clears_ready40_on_each_loaded_member()
    {
        var fixture = Fixture();
        ImmutableArray<NativeBattleGraphicsModel> models = fixture.Graphics.Models;
        models = models.SetItem(0, models[0].WriteLinkedModelIndex140(3));
        models = models.SetItem(3, (models[3] with { Flags0 = 2, ResourceFlags44 = 0x45 }).WriteLinkedModelIndex140(0));
        NativeBattleGraphicsState result = Schedule(fixture.Session, fixture.Graphics with { Models = models }, 0x1001);
        Assert.Equal((byte)2, result.Models[0].Sequence);
        Assert.Equal((ushort)2, result.Models[0].ResourceFlags44);
        Assert.Equal((ushort)5, result.Models[3].ResourceFlags44);
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
        NativeBattleGraphicsState graphics = fixture.Graphics with { Models = fixture.Graphics.Models.SetItem(0, fixture.Graphics.Models[0] with { Flags8 = status }) };
        NativeBattleGraphicsState result = Schedule(fixture.Session, graphics, requested);
        Assert.Equal(selected ? (byte)1 : (byte)2, result.Models[0].Sequence);
        Assert.Null(result.Auxiliary.Head);
    }

    [Fact]
    public void Unknown_ring_owner_is_not_treated_as_a_null_ring()
    {
        var fixture = Fixture();
        NativeBattleGraphicsState graphics = fixture.Graphics with { Models = fixture.Graphics.Models.SetItem(0, fixture.Graphics.Models[0] with { HasLinkedModelRing140 = null }) };
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
        BattleDefinition definition = (BattleDefinition)typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(3, 0, 8, 0, 9, 0, 10, 0, 0xA1, 0xA1, 0xA1));
        typeof(BattleDefinition).GetProperty(nameof(BattleDefinition.NativeGraphics))!.SetValue(definition, new NativeBattleGraphicsDefinition(null!, [new("party", 3, resource)], []));
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        models = models.SetItem(0, (models[0] with { Flags0 = 3, Flags8 = 1, ModelId4 = 3, ResourceFlags44 = 0x42, Sequence = 2, SequenceState = new() { Offset46 = 7, Pending3 = 2, Pose = 29 } }).WriteLinkedModelIndex140(null));
        return (session, new(models, ["party", null, null, "enemy", null, null, null], 0, 0));
    }

    private static NativeBattleGraphicsState Schedule(BattleSession session, NativeBattleGraphicsState graphics, int requested)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("ScheduleNativeRingSequence505C00", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, (byte)0, requested])!;
    }
}
