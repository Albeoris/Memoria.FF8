using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditionalActionServiceTests
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1234, 2)]
    public void UnsequencedServiceWaitsForSignedIoAndYieldsBeforeEntry(int io, byte phase)
    {
        var fixture = Fixture(0x50BC20);
        Set(fixture.Session, nameof(BattleSession.NativeEffectIoResult), io);
        var result = Poll(fixture.Session, fixture.Graphics, 1);
        Assert.Equal(phase, result.Phase);
        Assert.False(result.Complete);
        Assert.Same(fixture.Graphics.OriginalEffect, result.Graphics.OriginalEffect);
    }

    [Fact]
    public void UnsequencedCompletionWaitsForActualReturnedDescriptorAndThenClearsActive()
    {
        var fixture = Fixture(0x50BC20);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24CA828, 0x24CA818, 16, 1);
        NativeBattleOriginalEffectState pending = fixture.Graphics.OriginalEffect! with { ReturnedDescriptor = pool.DescriptorAddress, Pools = [pool] };
        Set(fixture.Session, nameof(BattleSession.NativeEffectMemory), fixture.Session.Presentation!.Memory.AssignReturnedTask(pending));
        var blocked = Poll(fixture.Session, fixture.Graphics with { OriginalEffect = pending }, 3);
        Assert.False(blocked.Complete);
        Assert.Equal(1, fixture.Session.Presentation!.Memory.Active);
        // Controlled post500900 poll boundary: the released descriptor clears the canonical process pointer.
        Set(fixture.Session, nameof(BattleSession.NativeEffectMemory), fixture.Session.Presentation!.Memory with { ReturnedTaskPointer1D96AAC = 0 });
        var done = Poll(fixture.Session, fixture.Graphics, 3);
        Assert.True(done.Complete);
        Assert.Equal(0u, done.Graphics.Flags & 0x10);
        Assert.Equal(0, fixture.Session.Presentation!.Memory.Active);
    }

    [Fact]
    public void IndependentSummonWaitsForEveryRetainedPartyOwnerBeforeStoppingSoundOrInvokingEntry()
    {
        var fixture = Fixture(0x50B830);
        Set(fixture.Session, nameof(BattleSession.NativeEffectIoResult), 0);
        BattleModelOwner source = fixture.Graphics.ActionGraphics!.ResolveActor(fixture.Graphics);
        BattleModelOwner other = new BattleForeignPartyModel(new("hidden-party"));
        NativeBattleActionGraphicsState context = fixture.Graphics.ActionGraphics with { HiddenPartyActors = BattleModelTargets.From([source, other]) };
        NativeBattleGraphicsState graphics = fixture.Graphics with
        {
            ActionGraphics = context,
            ActorModels = fixture.Graphics.ActorModels.Set(source, fixture.Graphics.ActorModels.Get(source) with { Flags0 = 4 })
        };
        var blocked = Poll(fixture.Session, graphics, 2);
        Assert.Equal((byte)2, blocked.Phase);
        Assert.False(blocked.Complete);
        Assert.DoesNotContain(fixture.Session.Events, item => item.NativeStopAllSounds);
        Assert.Equal(1, fixture.Session.Presentation!.Memory.Active);
    }

    [Fact]
    public void IndependentSummonRequiresCapturedPartyOwners()
    {
        var fixture = Fixture(0x50B830);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Poll(fixture.Session, fixture.Graphics, 2));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Null(fixture.Graphics.ActionGraphics!.HiddenPartyActors);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(uint callback)
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        Set(session, nameof(BattlePresentationProcess.NativeCamera), NativeBattleCameraState.FromColdProcess());
        Set(session, nameof(BattleSession.NativeEffectMemory), session.Presentation!.Memory with { Active = 1 });
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        BattleModelOwner source = packets.ModelAliases.DecodeLiteral(0);
        BattleModelOwner other = new BattleForeignPartyModel(new("hidden-party"));
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(source, 2, 0, 0, 65535, 1, null));
        (packets, _) = packets.Publish(0);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleActionGraphicsState context = new(header, new BattleRetainedModelReference.CanonicalOwner(source), new BattleContactCursor(0), BattleRetainedModelSelection.Empty, BattleRetainedModelSelection.Empty, null, null, 0, null, callback);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, source, new(0)), (2, other, new(0))], flags: 0x10) with
        {
            ActionGraphics = context,
            OriginalEffect = new(1, 0, new(1, 0x8D6A00, 0), new(source, 0, BattleModelTargets.Empty, header, 0, 0), 0)
        };
        return (session, graphics);
    }

    private static (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) Poll(BattleSession session, NativeBattleGraphicsState graphics, byte phase)
    {
        return ((NativeBattleGraphicsState, byte, bool))typeof(BattleSession).GetMethod("PollNativeActionGraphics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, phase])!;
    }

    private static void Set(BattleSession session, string property, object value)
    {
        typeof(BattleSession).GetProperty(property)!.SetValue(session, value);
    }
}
