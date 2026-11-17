using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedEncounterControlTests
{
    [Fact]
    public void Effect20_schedules_its_current_hosted_model_activation_and_rejects_missing_or_stale_owners()
    {
        (_, BattleSession child, NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleModelLifecycleServices services) = Fixture();
        BattleModelOwner retired = new BattleEnemyActorModel(new("retired"));
        AssertRejected(child, "QueueEffectPresentationActivation", graphics, retired);
        Field("hostedFrameServices").SetValue(child, new NativeBattleModelLifecycleServices(services.Facts.Remove(owner)));
        AssertRejected(child, "QueueEffectPresentationActivation", graphics, owner);
        Assert.Empty(graphics.Descriptors.Queue);

        Field("hostedFrameServices").SetValue(child, services);
        NativeBattleGraphicsState queued = Assert.IsType<NativeBattleGraphicsState>(Invoke(child, "QueueEffectPresentationActivation", graphics, owner));
        int slot = Assert.Single(queued.Descriptors.Queue);
        NativeBattleDescriptorPacket descriptor = queued.Descriptors.Packets[slot];
        Assert.Equal((ushort)113, descriptor.Command);
        Assert.Equal(0x487670u, descriptor.Argument);
        Assert.Equal(owner, Assert.IsType<NativeBattleDescriptorModelSubject.ExplicitModel>(descriptor.ModelSubject).Owner);
        Assert.Empty(services.ReadOrderedReceipts());

        NativeBattleGraphicsState completed = AdvanceDescriptors(child, queued);
        Assert.Equal(slot, Assert.Single(completed.Descriptors.Queue));
        Assert.Equal(byte.MaxValue, completed.Descriptors.Packets[slot].PriorityState);
        NativeBattleModelParticipantActivated receipt = Assert.IsType<NativeBattleModelParticipantActivated>(Assert.Single(services.ReadOrderedReceipts()));
        Assert.Equal(owner, receipt.Owner);
        Assert.Equal("reserve", receipt.ActorId);

        NativeBattleGraphicsState drained = AdvanceDescriptors(child, completed);
        Assert.Empty(drained.Descriptors.Queue);
        Assert.Same(receipt, Assert.Single(services.ReadOrderedReceipts()));
    }

    [Fact]
    public void Effect76_delivers_hosted_file_audio_and_its_original_actor_text_once_in_order()
    {
        (BattleSession logical, BattleSession child, NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleModelLifecycleServices services) = Fixture();
        AssertRejected(child, "QueueNativeEffect76Text", graphics, new NativeBattleEffect76Text4876F0(new BattleEnemyActorModel(new("retired")), 1, 60));
        _ = Invoke(child, "PublishNativeEffect76WaveRequest", new NativeBattleEffect76WaveRequest46CBB0(0, "field2.fs", 127));
        NativeBattleGraphicsState queued = Assert.IsType<NativeBattleGraphicsState>(Invoke(child, "QueueNativeEffect76Text", graphics, new NativeBattleEffect76Text4876F0(owner, 1, 60)));
        NativeBattleDescriptorPacket descriptor = queued.Descriptors.Packets[Assert.Single(queued.Descriptors.Queue)];
        Assert.Equal((ushort)8, descriptor.Command);
        Assert.Equal((byte)240, descriptor.PriorityState);
        Assert.Equal((short)60, descriptor.ScalarWord8);
        Assert.Collection(services.ReadOrderedReceipts(),
            next => Assert.IsType<NativeBattlePresentationFileAudioRequested>(next),
            next => Assert.Equal(owner, Assert.IsType<NativeBattleModelTurnAdvanceStopRequested>(next).Owner));

        NativeBattleGraphicsState stepped = AdvanceDescriptors(child, queued);
        AdvanceDescriptors(child, stepped);
        NativeBattlePresentationDescriptorText text = Assert.Single(services.ReadOrderedReceipts().OfType<NativeBattlePresentationDescriptorText>());
        Assert.Equal("original text", text.Message.Message);
        Assert.Equal("reserve", text.Message.ActorId);
        _ = Invoke(logical, "ApplyExpandedHostedFrameReceipts", services, null);
        Assert.Collection(logical.Events.Where(entry => entry.Kind is "NativeText" or "NativeFileAudio"),
            audio =>
            {
                NativeBattleFileAudioRequest request = Assert.IsType<NativeBattleFileAudioRequest>(audio.NativeFileAudio);
                Assert.Equal("field2.fs", request.ResourceName);
                Assert.Equal(127, request.Gain);
            },
            message =>
            {
                Assert.Equal("original text", message.Message);
                Assert.Equal(60, message.Amount);
                Assert.Equal(BattleTextIdentity.Enemy(77, 1), message.NativeTextIdentity);
            });
    }

    private static (BattleSession Logical, BattleSession Child, NativeBattleGraphicsState Graphics, BattleModelOwner Owner, NativeBattleModelLifecycleServices Services) Fixture()
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party" }, new() { Id = "reserve", Name = "Reserve", ModelId = 77 }],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess()
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        BattleSession child = new(definition, BattleAtbMode.TurnBased);
        (BattleModelRoster roster, NativeBattleModelAliases aliases) = logical.ReadBattlePresentationPacketBindings();
        BattleModelOwner owner = logical.ReadPresentationModelOwner("reserve");
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0)) with { EntryReady = true, EntryLifecycle = 4 };
        NativeBattleGraphicsResource original = new([0, 0]) { TextBank = ["", "original text"] };
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        Field("definition").SetValue(child, definition with { NativeGraphics = new(rules, [new("reserve", 93, original)], []) });
        Field("hostedBattlePresentation").SetValue(child, true);
        FieldInfo entry = Field("hostedEntryDescriptors");
        entry.SetValue(child, Activator.CreateInstance(entry.FieldType));
        NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(roster);
        Field("hostedFrameServices").SetValue(child, services);
        return (logical, child, graphics, owner, services);
    }

    private static NativeBattleGraphicsState AdvanceDescriptors(BattleSession child, NativeBattleGraphicsState graphics)
    {
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(child.Presentation!, graphics);
        return Assert.IsType<NativeBattleGraphicsState>(Invoke(child, "AdvanceNativeDescriptors", graphics));
    }

    private static object? Invoke(BattleSession owner, string method, params object?[] arguments)
    {
        return typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, arguments);
    }

    private static void AssertRejected(BattleSession child, string method, params object?[] arguments)
    {
        TargetInvocationException rejection = Assert.Throws<TargetInvocationException>(() => Invoke(child, method, arguments));
        Assert.IsType<InvalidDataException>(rejection.InnerException);
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
