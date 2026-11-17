using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

/// <summary>Controlled outer-finalization inputs; installed presentation tests establish natural module completion.</summary>
public sealed class NativeBattleGuardianInvocationFinalizationTests
{
    [Theory]
    [InlineData(116, 3, true)]
    [InlineData(203, 3, true)]
    [InlineData(203, 2, false)]
    [InlineData(203, 0, false)]
    public void Finalization_consumes_the_original_active_header_only_when_the_module_requests_it(ushort effect, byte parameter81, bool publish)
    {
        BattleSession owner = Create(effect, parameter81);
        NativeBattleActionPacketsState packets = owner.Presentation!.NativeActionPackets;
        NativeBattleEffectPresentationStep result = owner.Presentation!.CompleteEffectPresentationInvocation();
        Assert.Equal(publish ? 2 : 0, result.Contacts.Length);
        if (publish)
        {
            Assert.Equal(packets.ReadHeaderContacts(new BattleActionHeaderReference(0)), result.Contacts);
        }
        Assert.Equal(publish ? new uint[] { 0x87, 0x88 } : [], result.Sounds.Select(sound => sound.Sound));
        Assert.All(result.Sounds, sound =>
        {
            Assert.Equal(0u, sound.Flags);
            Assert.Equal((byte)128, sound.Pan);
            Assert.Null(sound.Anchor);
        });
        Assert.False(owner.Presentation!.HasPendingEffectPresentationEvents);
        Assert.Equal(0, owner.Presentation!.Memory.Active);
        Assert.Equal(0u, owner.Presentation!.Graphics.Flags & 0x10u);
        Assert.Equal(packets, owner.Presentation!.NativeActionPackets);
        NativeBattleEffectPresentationStep repeated = owner.Presentation!.CompleteEffectPresentationInvocation();
        Assert.Empty(repeated.Contacts);
        Assert.Empty(repeated.Sounds);
    }

    [Fact]
    public void Uninvoked_live_and_detached_native_processes_cannot_be_finalized()
    {
        BattleSession uninvoked = Create(203, 3);
        SetProperty(uninvoked, nameof(BattlePresentationProcess.EffectPresentationInvoked), false);
        Assert.Throws<InvalidOperationException>(() => uninvoked.Presentation!.CompleteEffectPresentationInvocation());
        BattleSession live = Create(203, 3);
        NativeBattleHostedTestFixture.SetProperty(live.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), live.Presentation!.Memory with { ReturnedTaskPointer1D96AAC = 1 });
        Assert.Throws<InvalidOperationException>(() => live.Presentation!.CompleteEffectPresentationInvocation());
        BattleSession detached = Create(203, 3);
        NativeBattleHostedTestFixture.SetProperty(detached.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), detached.Presentation!.Memory with { DetachedReturnedTask = detached.Presentation!.Graphics.OriginalEffect });
        Assert.Throws<InvalidOperationException>(() => detached.Presentation!.CompleteEffectPresentationInvocation());
        Assert.All(new[] { uninvoked, live, detached }, owner => Assert.False(owner.Presentation!.HasPendingEffectPresentationEvents));
    }

    [Fact]
    public void Outstanding_callbacks_and_unconsumed_output_block_finalization_without_publishing_contacts()
    {
        BattleSession callbacks = Create(203, 3);
        NativeBattleGraphicsState graphics = callbacks.Presentation!.Graphics;
        SetProperty(callbacks, nameof(BattlePresentationProcess.NativeGraphics), graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, 96) });
        Assert.Throws<InvalidOperationException>(() => callbacks.Presentation!.CompleteEffectPresentationInvocation());
        Assert.False(callbacks.Presentation!.HasPendingEffectPresentationEvents);
        BattleSession pending = Create(203, 3);
        List<NativeBattleModuleSound> sounds = (List<NativeBattleModuleSound>)Field("presentationSounds").GetValue(pending)!;
        sounds.Add(new(9, 0, null));
        Assert.Throws<InvalidOperationException>(() => pending.Presentation!.CompleteEffectPresentationInvocation());
        Assert.Single(sounds);
        Assert.Equal(1, pending.Presentation!.Memory.Active);
    }

    [Fact]
    public void Other_outer_services_cannot_reinterpret_the_module_parameter_as_guardian_completion()
    {
        BattleSession owner = Create(203, 3, 2);
        Assert.Throws<InvalidOperationException>(() => owner.Presentation!.CompleteEffectPresentationInvocation());
        Assert.False(owner.Presentation!.HasPendingEffectPresentationEvents);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(70)]
    public void Guardian_headers_routed_to_ordinary_casting_have_no_late_parameter81_contact_contract(ushort action)
    {
        BattleSession owner = Create(203, 3, action: action);
        NativeBattleEffectPresentationStep result = owner.Presentation!.CompleteEffectPresentationInvocation();
        Assert.Empty(result.Contacts);
        Assert.Empty(result.Sounds);
        Assert.Equal(0, owner.Presentation!.Memory.Active);
    }

    private static BattleSession Create(ushort effect, byte parameter81, byte command = 254, ushort action = 73)
    {
        BattleSession owner = new(new BattleDefinition
        {
            Name = "Guardian invocation finalization",
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "source", Name = "Source", IsPlayer = true },
                new() { Id = "target", Name = "Target" }
            ]
        }, BattleAtbMode.TurnBased);
        BattleModelOwner source = new BattlePartyActorModel(new("source"));
        BattleModelOwner target = new BattleEnemyActorModel(new("target"));
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, source, new(0)), (3, target, new(0))], 0x10);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(graphics.ActorModels.Roster, owner.Presentation!.NativeActionPackets.ExportRetained(), graphics.NativeBindings.CaptureAliases(graphics.ActorModels));
        (packets, _) = packets.Prepare(new(source, command, 14, -1, action, effect, null));
        (packets, _) = packets.AppendContact(new(new(source, 5, 1, 0, 0, 7, 0), null));
        (packets, _) = packets.AppendContact(new(new(target, 5, 1, 0, 0, 11, 0), null));
        (packets, _) = packets.Publish(2);
        Field("nativeActionPackets").SetValue(owner, packets.ActivateHeader(new BattleActionHeaderReference(0)));
        Field("presentationForeignActors").SetValue(owner, new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, 1, 0), new(source, 0, BattleModelTargets.From([source, target]), new BattleActionHeaderReference(0), parameter81, 0), 0);
        graphics = graphics with { OriginalEffect = selected };
        SetProperty(owner, nameof(BattlePresentationProcess.NativeGraphics), graphics);
        NativeBattleHostedTestFixture.SetProperty(owner.Presentation!, nameof(BattlePresentationProcess.NativeEffectMemory), owner.Presentation!.Memory with { ReturnedTaskPointer1D96AAC = 0, Active = 1 });
        SetProperty(owner, nameof(BattlePresentationProcess.EffectPresentationInvoked), true);
        return owner;
    }

    private static FieldInfo Field(string name)
    {
        return typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static void SetProperty(BattleSession owner, string name, object value)
    {
        typeof(BattleSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(owner, value);
    }
}
