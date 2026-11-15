using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedCinematicContextTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BattleEnemyActorModel Source = new(new("enemy"));
    private static readonly NativeBattleContactPacket Context = new(new(Source, 0, 1, 0, 0x10, 0, 0x100), null);

    [Theory]
    [InlineData(243, 147)]
    [InlineData(244, 148)]
    [InlineData(245, 149)]
    [InlineData(246, 150)]
    [InlineData(247, 144)]
    public void Proven_native_context_retains_original_status_mirrors_and_has_no_required_physical_impact(ushort effect, ushort action)
    {
        BattleSession candidate = Candidate();
        Stage(candidate, Action(effect, action), Context);
        Assert.Equal(NativeBattleCinematicContactPublication.ContextOnly, NativeBattleCinematicProfile.ForEffect(effect).ContactPublication);
        Assert.Equal(NativeBattleCinematicContactPublication.Required, NativeBattleCinematicProfile.ForEffect(248).ContactPublication);
        object[] operations = ReadOperations(candidate);
        object operation = Assert.Single(operations);
        var contacts = Assert.IsType<ImmutableArray<ExpandedBattleContact>>(operation.GetType().GetProperty("Contacts")!.GetValue(operation));
        Assert.Equal(Context, Assert.Single(contacts).Packet);
        Assert.Equal("ContextOnly", operation.GetType().GetProperty("ContactDisposition")!.GetValue(operation)!.ToString());
    }

    [Theory]
    [InlineData(244, 148, 0)]
    [InlineData(245, 149, 1)]
    [InlineData(246, 150, 2)]
    [InlineData(247, 144, 3)]
    public void Mismatched_action_or_generated_contact_cannot_enter_the_candidate_stream(ushort effect, ushort id, int mismatch)
    {
        BattleSession candidate = Candidate();
        BattleNativeAction action = Action(effect, id);
        if (mismatch == 0)
        {
            action = action with { Statuses1 = 1 };
        }
        NativeBattleContactPacket contact = mismatch switch
        {
            1 => Context with { Primary = Context.Primary with { Amount6 = 7 } },
            2 => Context with { Primary = Context.Primary with { Sequence = 5 } },
            3 => Context with { Secondary = Context.Primary },
            _ => Context
        };
        var error = Assert.Throws<TargetInvocationException>(() => Stage(candidate, action, contact));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Empty(ReadOperations(candidate));
    }

    private static BattleNativeAction Action(ushort effect, ushort action)
    {
        return new(8, action, 3, action)
        {
            MagicId = effect, AttackType = 0, Power = 0, AttackFlags = 3, DamagePresentation = 0, HitCount = 1, Hit = 255,
            StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Element = 0, RawRecord = [], SupplementalRecord = [],
            TargetFlags = null, Critical = null, ElementPercent = null, PowerModifier = null, LevelModifier = null
        };
    }

    private static BattleSession Candidate()
    {
        BattleSession candidate = new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetField("preparingExpandedHostedAction", Private)!.SetValue(candidate, true);
        return candidate;
    }

    private static object[] ReadOperations(BattleSession candidate)
    {
        var operations = (System.Collections.IEnumerable)ReadHostedOperations(candidate, Private);
        return operations.Cast<object>().ToArray();
    }

    private static void Stage(BattleSession candidate, BattleNativeAction action, NativeBattleContactPacket contact)
    {
        byte sequence = action.MagicId == 247 ? (byte)15 : (byte)14;
        NativeBattleActionHeaderPreparation header = new(Source, 8, sequence, -1, action.ActionId, action.MagicId, null);
        BattleActionPresentation command = new("enemy", "enemy", BattleActionPhase.Recovery, 1, BattleTargetRequest.None, BattleAbility.EnemyAbility, action.ActionId)
        {
            NativeQueuedCommand = 8,
            NativeAction = action,
            NativeSequence = sequence
        };
        typeof(BattleSession).GetMethod("StageExpandedHostedInvocationCore", Private)!.Invoke(candidate, [command, header, ImmutableArray.Create(new ExpandedBattleContact("enemy", contact)), null, false, false]);
    }

    private static object ReadHostedOperations(BattleSession battle, BindingFlags flags)
    {
        object cursor = typeof(BattleSession).GetField("expandedOperationCursor", flags)!.GetValue(battle)!;
        return cursor.GetType().GetProperty("Operations")!.GetValue(cursor)!;
    }

}
