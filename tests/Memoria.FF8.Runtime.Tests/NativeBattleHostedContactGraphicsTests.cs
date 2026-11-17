using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedContactGraphicsTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly NativeBattleGraphicsResource HitResource = new([5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 0, 0xA0, 0, 0xA1]) { AnimationFrames = [2] };
    private static readonly BattlePartyActorModel FourthAlly = new(new("ally-3"));
    private static readonly BattlePartyActorModel LastAlly = new(new("ally-10"));
    private static readonly BattleEnemyActorModel FirstEnemy = new(new("enemy-0"));
    private static readonly BattleEnemyActorModel FifthEnemy = new(new("enemy-4"));
    private static readonly BattleEnemyActorModel LastEnemy = new(new("enemy-13"));

    [Fact]
    public void Hosted_contacts_apply_original_model_reactions_in_order_without_a_logical_actor_lookup()
    {
        var (session, graphics) = Fixture(expanded: true);
        BattleCombatant target = session.Combatants.Single(actor => actor.Id == "ally-3");
        target.NativeStatus1 = 0x880000;
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        NativeBattleContactPacket first = new(new(FourthAlly, 5, 0, 0, 4, 123, 0x8), new(LastAlly, 5, 0, 0, 0, 23, 0x800000));
        NativeBattleContactPacket second = new(new(FifthEnemy, 5, 0, 0, 0, 234, 0), null);
        NativeBattleContactPacket third = new(new(LastEnemy, 5, 0, 0, 0, 345, 0x880000), new(LastAlly, 5, 0, 0, 0, 45, 0));
        graphics = graphics with
        {
            ActorModels = graphics.ActorModels.Set(FifthEnemy, graphics.ActorModels.Get(FifthEnemy) with { ResourceFlags44 = 0x40 }).Set(LastEnemy, graphics.ActorModels.Get(LastEnemy) with { Flags0 = 0 }),
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
        Assert.Equal((uint?)0x200024, result.ActorModels.Get(FourthAlly).Flags8);
        Assert.Equal((uint?)4, result.ActorModels.Get(LastAlly).Flags8);
        Assert.Equal((uint?)0, result.ActorModels.Get(FifthEnemy).Flags8);
        Assert.Equal((byte)5, result.ActorModels.Get(FifthEnemy).Sequence);
        Assert.Equal((ushort?)1, result.ActorModels.Get(FifthEnemy).ResourceFlags44);
        Assert.Equal(graphics.ActorModels.Get(LastEnemy), result.ActorModels.Get(LastEnemy));
        Assert.Equal(4, result.Effects.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Equal(FourthAlly, Assert.Single(result.Effects.Slots.Where(task => task.Kind == NativeBattleEffectKind.StatusReset506860)).ModelOwner);
        Dictionary<BattleModelOwner, short> anchors = new() { [FourthAlly] = 127, [LastAlly] = 134, [FifthEnemy] = 135 };
        NativeBattleEffectsStep stepped = result.Effects.Advance(result.ActorModels, owner => new(anchors[owner], 100));
        Assert.Equal(new[] { 123, 126, 115 }, stepped.State.TextDraws.Select(draw => (int)draw.X));
        Assert.Equal(new byte[] { 58, 59, 60, 0 }, stepped.State.TextDraws[0].Glyphs);
        Assert.Equal(new byte[] { 58, 59, 0 }, stepped.State.TextDraws[1].Glyphs);
        Assert.Equal(new byte[] { 57, 58, 59, 0 }, stepped.State.TextDraws[2].Glyphs);
        Assert.Equal(before, session.Combatants.Select(actor => (actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.NativeCombatantFlags, actor.Atb, actor.IsBusy)).ToArray());
        Assert.Equal(items, session.Items);
        Assert.Equal(rng, rngFields.Select(name => Field(name).GetValue(session)).ToArray());
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
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        NativeBattleContactPacket first = new(new(FourthAlly, 5, 0, 0, 4, 123, 0), null);
        NativeBattleContactHalf later = new(LastEnemy, 5, 0, 0, 0, 30, 0);
        NativeBattleContactPacket last = failure switch
        {
            "outside-primary" => new(later with { Recipient = new BattleEnemyActorModel(new("outside-fixture")) }, null),
            "hole-secondary" => new(later, later with { Recipient = LastAlly }),
            _ => new(later, null)
        };
        if (failure == "missing-status")
        {
            services = new(services.Facts.SetItem(LastEnemy, services.Facts[LastEnemy]! with { RawBattleStatuses = null }));
        }
        graphics = graphics with
        {
            PendingContacts = [first, last],
            Flags = 0x10000000,
            // An immediate party petrify would need the real status palette owner.
            ActorModels = graphics.ActorModels.Set(FourthAlly, graphics.ActorModels.Get(FourthAlly) with { ResourceFlags44 = 0x40 })
        };
        typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics))!.SetValue(session.Presentation!, graphics);
        var models = graphics.ActorModels;
        var effects = graphics.Effects;

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => Consume(session, graphics, services));

        Assert.IsType<InvalidDataException>(invocation.InnerException);
        Assert.Empty(Contacts(session));
        Assert.Same(graphics, session.Presentation?.Graphics);
        Assert.Equal(models, session.Presentation!.Graphics.ActorModels);
        Assert.Same(effects, session.Presentation!.Graphics.Effects);
        Assert.Equal(0x10000000u, session.Presentation!.Graphics.Flags);
        Assert.Equal(new[] { first, last }, session.Presentation!.Graphics.PendingContacts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_enemy_contact_selects_or_queues_the_original_reaction(bool ready)
    {
        var (session, graphics) = Fixture(expanded: false);
        ushort resourceFlags = ready ? (ushort)0x40 : (ushort)1;
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(FirstEnemy, graphics.ActorModels.Get(FirstEnemy) with { ResourceFlags44 = resourceFlags }) };
        NativeBattleContactPacket packet = new(new(FirstEnemy, 5, 0, 0, ready ? (ushort)0 : (ushort)4, 123, 0), null);
        NativeBattleModelLifecycleServices services = session.CaptureModelLifecycleServices(graphics.ActorModels.Roster);
        NativeBattleGraphicsState result = Consume(session, graphics with { PendingContacts = [packet] }, services);

        Assert.Equal(ready ? (byte)5 : (byte)1, result.ActorModels.Get(FirstEnemy).Sequence);
        Assert.Equal(ready ? (byte)0 : (byte)5, result.ActorModels.Get(FirstEnemy).SequenceState!.Pending3);
        Assert.Equal(ready ? 1 : 2, result.Effects.Slots.Count(task => (task.Flags0 & 1) != 0));
        Assert.Equal(packet, Assert.Single(Contacts(session)));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(bool expanded, bool hole = false)
    {
        BattleDefinition definition = new()
        {
            Combatants = Enumerable.Range(0, expanded ? 11 : 1).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true })
                .Concat(Enumerable.Range(0, expanded ? 14 : 1).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", ModelId = 61 })).ToArray(),
            Items = [new(1, 3)]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        if (hole)
        {
            session.Combatants.Single(actor => actor.Id == "ally-10").BindRosterSlot(null, bindNative: false);
        }
        var (roster, aliases) = session.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(2)
        {
            ModelId4 = owner.Side == BattleSide.Allies ? (byte)1 : (byte)61,
            Flags8 = 0,
            ResourceFlags44 = 1,
            Sequence = 1,
            SequenceState = new(),
            HasLinkedModelRing140 = false
        }, BattleContactTaskStorageContract.ExpandableArena);
        Dictionary<BattleModelOwner, NativeBattleEffectModelBinding> presentationBindings = graphics.ActorModels.Entries.ToDictionary(entry => entry.Owner, entry => new NativeBattleEffectModelBinding(entry.Owner, entry.State, HitResource));
        var bindings = presentationBindings.Values.Select(binding => new NativeBattleGraphicsBinding(binding.ActorId, binding.Model.ModelId4!.Value, binding.Resource)).ToImmutableArray();
        ImmutableArray<sbyte> saved = Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray().SetItem(2, 4);
        ImmutableArray<sbyte> battle = Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray().SetItem(3, 5).SetItem(19, 21).SetItem(23, 2);
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved, battle);
        NativeBattleHostedTestFixture.BindProcess(session, graphics, new(rules, bindings, []));
        NativeBattleHostedTestFixture.SetField(session.Presentation!, "presentationNativeTargets", presentationBindings);
        return (session, graphics with { UiServices = NativeBattleUiServices.Initialize() });
    }

    private static NativeBattleGraphicsState Consume(BattleSession session, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        return (NativeBattleGraphicsState)typeof(BattlePresentationProcess).GetMethod("ConsumeHostedNativeGraphicsContacts", PrivateInstance)!.Invoke(session.Presentation!, [graphics, services, NativeBattleContactOrigin.Ordinary])!;
    }

    private static List<NativeBattleContactPacket> Contacts(BattleSession session) => (List<NativeBattleContactPacket>)typeof(BattlePresentationProcess).GetField("presentationContacts", PrivateInstance)!.GetValue(session.Presentation!)!;

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
