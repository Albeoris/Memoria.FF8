using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedEffectContactConsumerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RepeatedRecipientPublishesEachOrdinalAndOwnsOneSequenceRow()
    {
        BattleSession battle = Create();
        BattleActionPresentation command = Command();
        NativeBattleContactPacket first = Packet(3, 20);
        NativeBattleContactPacket second = Packet(3, 30);
        SetContacts(battle, [new("enemy-0", first), new("enemy-0", second)]);

        Publish(battle, command, new([first with { PhysicalContactIndex = 0 }], [])
        {
            Sequences = [new(OriginalOwner(3), NativeBattleForeignSequenceKind.Hit)]
        });
        Assert.Throws<InvalidDataException>(() => Invoke(battle, "ValidateExpandedEffectContactCompletion"));
        Assert.Throws<InvalidDataException>(() => Publish(battle, command, new([first with { PhysicalContactIndex = 0 }], [])));
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact");

        Publish(battle, command, new([second with { PhysicalContactIndex = 1 }], []));
        Invoke(battle, "ValidateExpandedEffectContactCompletion");
        Assert.Equal(new[] { 20, 30 }, battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.Amount));
        Assert.All(battle.Events.Where(entry => entry.Kind == "EffectContact"), entry => Assert.Equal("enemy-0", entry.TargetId));
        Assert.Equal("enemy-0", Assert.Single(battle.Events, entry => entry.Kind == "EffectModelSequence").TargetId);
    }

    [Fact]
    public void IdenticalPacketsNeedOrdinalsAndLogicalTailReceivesSeparateOrdinals()
    {
        BattleSession battle = Create();
        BattleActionPresentation command = Command();
        NativeBattleContactPacket repeated = Packet(3, 20);
        ExpandedBattleContact[] contacts = [.. Enumerable.Repeat(new ExpandedBattleContact("enemy-0", repeated), 6), new("enemy-0", Packet(0, 35))];
        SetContacts(battle, [.. contacts]);

        Assert.Throws<NotSupportedException>(() => Publish(battle, command, new([repeated], [])));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact");
        Publish(battle, command, new([.. Enumerable.Range(0, 5).Select(index => repeated with { PhysicalContactIndex = index })], []));
        Assert.Equal(5, battle.Events.Count(entry => entry.Kind == "EffectContact"));
        Publish(battle, command, new([repeated with { PhysicalContactIndex = 5 }], []));

        Invoke(battle, "ValidateExpandedEffectContactCompletion");
        Assert.Equal(new[] { 20, 20, 20, 20, 20, 20, 35 }, battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.Amount));
    }

    [Fact]
    public void SingleTargetHitPassesReuseRowsAndNeverUseRowSeven()
    {
        BattleSession battle = Create();
        BattleCombatant source = battle.Combatants[0];
        BattleCombatant[] targets = battle.Combatants.Skip(1).ToArray();
        Assert.Equal(new byte[] { 3, 1, 3, 2, 4, 5 }, ResolveRows(battle, source, [targets[0], targets[1], targets[0], targets[2], targets[3], targets[4]]));
        Assert.Equal(new byte[] { 3, 1, 2, 4, 5, 6 }, ResolveRows(battle, source, targets));
        Assert.Equal(new byte[] { 0, 3, 0, 3 }, ResolveRows(battle, source, [source, targets[0], source, targets[0]]));
    }

    private static byte[] ResolveRows(BattleSession battle, BattleCombatant source, BattleCombatant[] singleTargetPasses)
    {
        BattleNativeAction native = new(2, 1, 1, 1)
        {
            AttackType = 27, Power = 1, Hit = 0, HitCount = checked((byte)singleTargetPasses.Length), TargetFlags = 0x52, AttackFlags = 3,
            MagicId = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleActionPresentation command = Command() with { Ability = BattleAbility.Magic, ActionId = 1, NativeAction = native };
        BattleActionDefinition action = new(BattleAbility.Magic, 1, "Contact test", BattleActionEffect.Native, 1, false) { NativeAction = native };
        BattleCombatant[][] plan = singleTargetPasses.Select(recipient => new[] { recipient }).ToArray();
        typeof(BattleSession).GetField("nativeActionScratch", PrivateInstance)!.SetValue(battle, new NativeBattleActionScratch { ActorIndex = 0, Command = 2, Action = 1, EventActorB0F = 0, EventLane = 2, DeferredEffectsE00 = false });
        object? result = Invoke(battle, "ResolveExpandedHitPlan", source, command, action, native, plan, true);
        (ImmutableArray<ExpandedBattleContact> contacts, _) = Assert.IsType<ValueTuple<ImmutableArray<ExpandedBattleContact>, BattleCombatant?>>(result);
        return contacts.Select(contact => contact.Contact.Recipient).ToArray();
    }

    private static BattleSession Create()
    {
        return new(new()
        {
            UsesExpandedRoster = true,
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, MaximumHp = 2000, CurrentHp = 2000 },
                .. Enumerable.Range(0, 6).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = $"Enemy {index}", MaximumHp = 2000, CurrentHp = 2000 })
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleActionPresentation Command()
    {
        return new("caster", "enemy-0", BattleActionPhase.Recovery, 1) { NativeAction = null };
    }

    private static NativeBattleContactPacket Packet(byte row, ushort amount)
    {
        return new(new(row, 0, 0, 0, 0, amount, 0), new(255, 0, 0, 0, 0, 0, 0));
    }

    private static void SetContacts(BattleSession battle, ImmutableArray<ExpandedBattleContact> contacts)
    {
        typeof(BattleSession).GetField("expandedExpectedContacts", PrivateInstance)!.SetValue(battle, contacts.Length);
        Invoke(battle, "SetExpandedEffectContacts", contacts);
    }

    private static void Publish(BattleSession battle, BattleActionPresentation command, NativeBattleEffectPresentationStep step)
    {
        Invoke(battle, "PublishExpandedEffectStep", command, step);
    }

    private static object? Invoke(BattleSession battle, string method, params object[] arguments)
    {
        try
        {
            return typeof(BattleSession).GetMethod(method, PrivateInstance)!.Invoke(battle, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
