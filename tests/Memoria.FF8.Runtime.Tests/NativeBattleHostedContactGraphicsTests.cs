using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactGraphicsTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleGraphicsResource HitResource = new([5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 0, 0xA0, 0, 0xA1]) { AnimationFrames = [2] };
    private static readonly NativeBattleContactHalf NoSecondary = new(byte.MaxValue, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void Hosted_contacts_apply_original_model_reactions_in_order_without_a_logical_actor_lookup()
    {
        var (session, graphics) = Fixture(expanded: true);
        BattleCombatant target = session.Combatants.Single(actor => actor.Id == "ally-3");
        target.NativeStatus1 = 0x880000;
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(graphics.PresentationRoster!);
        NativeBattleContactPacket first = new(new(7, 5, 0, 0, 4, 123, 0x8), new(14, 5, 0, 0, 0, 23, 0x800000));
        NativeBattleContactPacket second = new(new(15, 5, 0, 0, 0, 234, 0), NoSecondary);
        NativeBattleContactPacket third = new(new(24, 5, 0, 0, 0, 345, 0x880000), new(14, 5, 0, 0, 0, 45, 0));
        graphics = graphics with
        {
            Models = graphics.Models.SetItem(15, graphics.Models[15] with { ResourceFlags44 = 0x40 }).SetItem(24, graphics.Models[24] with { Flags0 = 0 }),
            PendingContacts = [first, second, third]
        };
        var before = session.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeCombatantFlags, actor.Atb, actor.IsBusy)).ToArray();
        BattleStock[] items = session.Items.ToArray();
        string[] rngFields = ["random", "nativeCrtRandom", "nativeGlobalRandom", "nativeGlobalRandomCalls"];
        object?[] rng = rngFields.Select(name => Field(name).GetValue(session)).ToArray();
        ((Dictionary<string, BattleCombatant>)Field("byId").GetValue(session)!).Clear();

        NativeBattleGraphicsState result = Consume(session, graphics, services);

        Assert.Empty(result.PendingContacts);
        Assert.Equal(new[] { first, second, third }, Contacts(session));
        Assert.Equal((uint?)0x200024, result.Models[7].Flags8);
        Assert.Equal((uint?)4, result.Models[14].Flags8);
        Assert.Equal((uint?)0, result.Models[15].Flags8);
        Assert.Equal((byte)5, result.Models[15].Sequence);
        Assert.Equal((ushort?)1, result.Models[15].ResourceFlags44);
        Assert.Equal(graphics.Models[24], result.Models[24]);
        Assert.Equal(4, result.Effects.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Equal((byte)7, Assert.Single(result.Effects.Slots.Where(task => task.Kind == NativeBattleEffectKind.StatusReset506860)).ModelIndex);
        NativeBattleEffectsStep stepped = result.Effects.Advance(result.Models, row => new((short)(120 + row), 100), result.PresentationRoster);
        Assert.Equal(new[] { 123, 126, 115 }, stepped.State.TextDraws.Select(draw => (int)draw.X));
        Assert.Equal(new byte[] { 58, 59, 60, 0 }, stepped.State.TextDraws[0].Glyphs);
        Assert.Equal(new byte[] { 58, 59, 0 }, stepped.State.TextDraws[1].Glyphs);
        Assert.Equal(new byte[] { 57, 58, 59, 0 }, stepped.State.TextDraws[2].Glyphs);
        Assert.Equal(before, session.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeCombatantFlags, actor.Atb, actor.IsBusy)).ToArray());
        Assert.Equal(items, session.Items);
        Assert.Equal(rng, rngFields.Select(name => Field(name).GetValue(session)).ToArray());
        Assert.Null(Field("nativePartyPresentation").GetValue(session));
        Assert.Same(graphics.UiServices, result.UiServices);
        Assert.Empty(session.Events);
    }

    [Theory]
    [InlineData("outside-primary")]
    [InlineData("hole-secondary")]
    [InlineData("missing-status")]
    public void Invalid_late_contacts_reject_before_any_earlier_graphics_or_output(string failure)
    {
        var (session, graphics) = Fixture(expanded: true, hole: true);
        NativeBattleModelLifecycleServices services = session.CaptureExpandedModelLifecycleServices(graphics.PresentationRoster!);
        NativeBattleContactPacket first = new(new(7, 5, 0, 0, 4, 123, 0), NoSecondary);
        NativeBattleContactHalf later = new(24, 5, 0, 0, 0, 30, 0);
        NativeBattleContactPacket last = failure switch
        {
            "outside-primary" => new(later with { ActorIndex = 25 }, NoSecondary),
            "hole-secondary" => new(later, later with { ActorIndex = 14 }),
            _ => new(later, NoSecondary)
        };
        if (failure == "missing-status")
        {
            services = new(services.Facts.SetItem(24, services.Facts[24]! with { RawBattleStatuses = null }));
        }
        graphics = graphics with
        {
            PendingContacts = [first, last],
            Flags = 0x10000000,
            // An immediate party petrify would need the real status palette owner.
            Models = graphics.Models.SetItem(7, graphics.Models[7] with { ResourceFlags44 = 0x40 })
        };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
        var models = graphics.Models;
        var effects = graphics.Effects;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => Consume(session, graphics, services));

        Assert.IsType<InvalidDataException>(invocation.InnerException);
        Assert.Empty(Contacts(session));
        Assert.Same(graphics, session.NativeGraphics);
        Assert.Equal(models, session.NativeGraphics!.Models);
        Assert.Same(effects, session.NativeGraphics.Effects);
        Assert.Equal(0x10000000u, session.NativeGraphics.Flags);
        Assert.Equal(new[] { first, last }, session.NativeGraphics.PendingContacts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hosted_and_native_enemy_contact_produce_the_same_visual_reaction(bool ready)
    {
        var (native, nativeGraphics) = Fixture(expanded: false);
        var (hosted, hostedGraphics) = Fixture(expanded: false);
        ushort resourceFlags = ready ? (ushort)0x40 : (ushort)1;
        nativeGraphics = nativeGraphics with { Models = nativeGraphics.Models.SetItem(3, nativeGraphics.Models[3] with { ResourceFlags44 = resourceFlags }) };
        hostedGraphics = hostedGraphics with { Models = hostedGraphics.Models.SetItem(3, hostedGraphics.Models[3] with { ResourceFlags44 = resourceFlags }) };
        NativeBattleContactPacket packet = new(new(3, 5, 0, 0, ready ? (ushort)0 : (ushort)4, 123, 0), NoSecondary);
        NativeBattleModelLifecycleServices services = hosted.CaptureExpandedModelLifecycleServices(hostedGraphics.PresentationRoster!);
        NativeBattleGraphicsState nativeResult = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("ConsumeNativeGraphicsContacts", PrivateInstance)!.Invoke(native, [nativeGraphics with { PendingContacts = [packet], PresentationRoster = null }])!;
        NativeBattleGraphicsState hostedResult = Consume(hosted, hostedGraphics with { PendingContacts = [packet] }, services);

        Assert.Equal(ready ? (byte)5 : (byte)1, nativeResult.Models[3].Sequence);
        Assert.Equal(ready ? (byte)0 : (byte)5, nativeResult.Models[3].SequenceState!.Pending3);
        Assert.Equal(JsonSerializer.Serialize(nativeResult.Models[3]), JsonSerializer.Serialize(hostedResult.Models[3]));
        NativeBattleEffectTask[] nativeTasks = nativeResult.Effects.Slots.Where(task => (task.Flags0 & 1) != 0).ToArray();
        NativeBattleEffectTask[] hostedTasks = hostedResult.Effects.Slots.Where(task => (task.Flags0 & 1) != 0).ToArray();
        Assert.Equal(ready ? 1 : 2, hostedTasks.Length);
        Assert.Equal(nativeTasks.Length, hostedTasks.Length);
        for (int index = 0; index < nativeTasks.Length; index++)
        {
            Assert.Equal(nativeTasks[index].Glyphs.AsEnumerable(), hostedTasks[index].Glyphs.AsEnumerable());
            Assert.Equal(nativeTasks[index], hostedTasks[index] with { Glyphs = nativeTasks[index].Glyphs });
        }
        Assert.Equal(nativeResult.Effects.Head, hostedResult.Effects.Head);
        Assert.Equal(0, hostedResult.Effects.Tail);
        Assert.Equal(ready ? (int?)null : 0, nativeResult.Effects.Tail);
        Assert.Empty(Contacts(native));
        Assert.Equal(packet, Assert.Single(Contacts(hosted)));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(bool expanded, bool hole = false)
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = expanded,
            Combatants = Enumerable.Range(0, expanded ? 11 : 1).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true })
                .Concat(Enumerable.Range(0, expanded ? 14 : 1).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", ModelId = 61 })).ToArray(),
            Items = [new(1, 3)]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        if (hole)
        {
            session.Combatants.Single(actor => actor.Id == "ally-10").BindRosterSlot(null, bindNative: false);
        }
        BattleModelRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        Dictionary<int, NativeBattleEffectModelBinding> presentationBindings = [];
        foreach (int row in roster.PartyRows.Concat(roster.EnemyRows))
        {
            BattleModelOwner actor = roster[row];
            models[row] = models[row] with { ModelId4 = actor.IsParty ? (byte)1 : (byte)61, Flags0 = 2, Flags8 = 0, ResourceFlags44 = 1, Sequence = 1, SequenceState = new(), HasLinkedModelRing140 = false };
            presentationBindings.Add(row, new(actor.ActorId, models[row], HitResource) { IsParty = actor.IsParty });
        }
        var bindings = presentationBindings.Values.Select(binding => new NativeBattleGraphicsBinding(binding.ActorId, binding.Model.ModelId4!.Value, binding.Resource)).ToImmutableArray();
        ImmutableArray<sbyte> saved = Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray().SetItem(2, 4);
        ImmutableArray<sbyte> battle = Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray().SetItem(3, 5).SetItem(19, 21).SetItem(23, 2);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved, battle);
        Field("definition").SetValue(session, definition with { NativeGraphics = new(rules, bindings, []) });
        Field("presentationNativeTargets").SetValue(session, presentationBindings);
        NativeBattleGraphicsState graphics = new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = roster,
            UiServices = NativeBattleUiServices.Initialize()
        };
        return (session, graphics);
    }

    private static NativeBattleGraphicsState Consume(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        return (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("ConsumeHostedNativeGraphicsContacts", PrivateInstance)!.Invoke(session, [graphics, services])!;
    }

    private static List<NativeBattleContactPacket> Contacts(BattleSession session) => (List<NativeBattleContactPacket>)Field("presentationContacts").GetValue(session)!;

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
