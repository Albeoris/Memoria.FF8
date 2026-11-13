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
        NativeBattleActionPacketsState packets = owner.NativeActionPackets;
        NativeBattleEffectPresentationStep result = owner.CompleteEffectPresentationInvocation();
        Assert.Equal(publish ? 2 : 0, result.Contacts.Length);
        if (publish)
        {
            Assert.Equal(packets.ReadHeaderContacts506BA0(0), result.Contacts);
        }
        Assert.Equal(publish ? new uint[] { 0x87, 0x88 } : [], result.Sounds.Select(sound => sound.Sound));
        Assert.All(result.Sounds, sound =>
        {
            Assert.Equal(0u, sound.Flags);
            Assert.Equal((byte)128, sound.Pan);
            Assert.Null(sound.Anchor);
        });
        Assert.False(owner.HasPendingEffectPresentationEvents);
        Assert.Equal(0, owner.NativeEffectMemory!.Active);
        Assert.Equal(0u, owner.NativeGraphics!.Flags & 0x10u);
        Assert.Equal(packets, owner.NativeActionPackets);
        NativeBattleEffectPresentationStep repeated = owner.CompleteEffectPresentationInvocation();
        Assert.Empty(repeated.Contacts);
        Assert.Empty(repeated.Sounds);
    }

    [Fact]
    public void Uninvoked_live_and_detached_native_processes_cannot_be_finalized()
    {
        BattleSession uninvoked = Create(203, 3);
        SetProperty(uninvoked, nameof(BattleSession.EffectPresentationInvoked), false);
        Assert.Throws<InvalidOperationException>(() => uninvoked.CompleteEffectPresentationInvocation());
        BattleSession live = Create(203, 3);
        SetProperty(live, nameof(BattleSession.NativeEffectMemory), live.NativeEffectMemory! with { ReturnedTaskPointer1D96AAC = 1 });
        Assert.Throws<InvalidOperationException>(() => live.CompleteEffectPresentationInvocation());
        BattleSession detached = Create(203, 3);
        SetProperty(detached, nameof(BattleSession.NativeEffectMemory), detached.NativeEffectMemory! with { DetachedReturnedTask = detached.NativeGraphics!.OriginalEffect });
        Assert.Throws<InvalidOperationException>(() => detached.CompleteEffectPresentationInvocation());
        Assert.All(new[] { uninvoked, live, detached }, owner => Assert.False(owner.HasPendingEffectPresentationEvents));
    }

    [Fact]
    public void Outstanding_callbacks_and_unconsumed_output_block_finalization_without_publishing_contacts()
    {
        BattleSession callbacks = Create(203, 3);
        NativeBattleGraphicsState graphics = callbacks.NativeGraphics!;
        SetProperty(callbacks, nameof(BattleSession.NativeGraphics), graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 1, 96) });
        Assert.Throws<InvalidOperationException>(() => callbacks.CompleteEffectPresentationInvocation());
        Assert.False(callbacks.HasPendingEffectPresentationEvents);
        BattleSession pending = Create(203, 3);
        List<NativeBattleModuleSound> sounds = (List<NativeBattleModuleSound>)Field("presentationSounds").GetValue(pending)!;
        sounds.Add(new(9, 0, null));
        Assert.Throws<InvalidOperationException>(() => pending.CompleteEffectPresentationInvocation());
        Assert.Single(sounds);
        Assert.Equal(1, pending.NativeEffectMemory!.Active);
    }

    [Fact]
    public void Other_outer_services_cannot_reinterpret_the_module_parameter_as_guardian_completion()
    {
        BattleSession owner = Create(203, 3, 2);
        Assert.Throws<InvalidOperationException>(() => owner.CompleteEffectPresentationInvocation());
        Assert.False(owner.HasPendingEffectPresentationEvents);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(70)]
    public void Guardian_headers_routed_to_ordinary_casting_have_no_late_parameter81_contact_contract(ushort action)
    {
        BattleSession owner = Create(203, 3);
        NativeBattleActionPacketsState packets = owner.NativeActionPackets;
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        header = header with { Payload = header.Payload.SetItem(4, unchecked((byte)action)).SetItem(5, unchecked((byte)(action >> 8))) };
        Field("nativeActionPackets").SetValue(owner, packets with { Headers = packets.Headers.SetItem(0, header) });
        NativeBattleEffectPresentationStep result = owner.CompleteEffectPresentationInvocation();
        Assert.Empty(result.Contacts);
        Assert.Empty(result.Sounds);
        Assert.Equal(0, owner.NativeEffectMemory!.Active);
    }

    private static BattleSession Create(ushort effect, byte parameter81, byte command = 254)
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
        NativeBattleActionPacketsState packets = owner.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, command, 14, -1, 73, effect, null));
        (packets, _) = packets.AppendContact(new(new(0, 5, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(1, 5, 1, 0, 0, 11, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        packets = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(packets))!;
        Field("nativeActionPackets").SetValue(owner, packets.ActivateHeader(0));
        Field("presentationForeignActors").SetValue(owner, new Dictionary<int, NativeBattleForeignActor>());
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, 1, 0), new(0, 0, 3, 0, parameter81, 0), 0);
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), ["source", null, null, null, null, null, null], 0x10, 0) { OriginalEffect = selected };
        SetProperty(owner, nameof(BattleSession.NativeGraphics), graphics);
        SetProperty(owner, nameof(BattleSession.NativeEffectMemory), owner.NativeEffectMemory! with { ReturnedTaskPointer1D96AAC = 0, Active = 1 });
        SetProperty(owner, nameof(BattleSession.EffectPresentationInvoked), true);
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
