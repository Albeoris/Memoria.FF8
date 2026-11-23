using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect64HostedAdmissionTests
{
    private const string Boss = "ultimecia-griever";
    private static readonly BattleEnemyActorReference Actor = new(Boss);
    private static readonly BattleEnemyActorModel Model = new(Actor);
    private static readonly NativeBattleActionHeaderPreparation Header = new(Model, 8, 16, 0, 353, 64, null);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Original_callback_uses_one_neutral_self_packet_without_requiring_impact(byte callback)
    {
        var battle = CreateBattle();
        var command = Command(callback);
        var disposition = ReadDisposition(battle, command, Header);
        NativeBattleContactPacket packet = new(new(Model, 0, 1, 0, 0, 0, 0), null);
        ImmutableArray<ExpandedBattleContact> contacts = [new(Boss, packet)];
        ValidateGeometry(contacts);
        var invocation = new BattleSession.ExpandedHostedInvocation(command, Header, contacts, null, disposition);

        Assert.Equal(BattleSession.ExpandedHostedContactDisposition.ContextOnly, disposition);
        Assert.Equal(packet, Assert.Single(invocation.Contacts).Packet);
        Assert.Equal(0, invocation.RequiredContactCount);
    }

    [Fact]
    public void Admission_rejects_unproved_producer_stage_and_damage_record()
    {
        BattleSession battle = CreateBattle();
        BattleActionPresentation command = Command();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ReadDisposition(battle, Command(1), Header)).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ReadDisposition(CreateBattle(141), command, Header)).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ReadDisposition(battle, command, Header with { Sequence = 17 })).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ReadDisposition(battle, command with { NativeAction = command.NativeAction! with { Power = 1 } }, Header)).InnerException);
    }

    [Fact]
    public void Geometry_guard_rejects_missing_foreign_and_damaging_physical_packets()
    {
        NativeBattleContactHalf self = new(Model, 0, 1, 0, 0, 0, 0);
        NativeBattleContactHalf foreign = self with { Recipient = new BattlePartyActorModel(new("ally")) };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ValidateGeometry([])).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ValidateGeometry([new("ally", new(foreign, null))])).InnerException);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => ValidateGeometry([new(Boss, new(self with { Amount6 = 1 }, null))])).InnerException);
    }

    private static BattleSession CreateBattle(int stage = 142)
    {
        return new(new()
        {
            EncounterId = 511,
            StageId = stage,
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = Boss, Name = "Ultimecia", ModelId = 124 }]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleActionPresentation Command(byte callback = 2)
    {
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([Actor]), BattleTargetEligibility.Living, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        return new(Boss, Boss, BattleActionPhase.Recovery, target, BattleAbility.EnemyAbility, 353)
        {
            NativeAiCallbackStage = callback,
            NativeQueuedCommand = 8,
            NativeSequence = 16,
            NativeAction = new(8, 353, 3, 353)
            {
                MagicId = 64, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3,
                DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
                Hit = 255, Critical = null, PowerModifier = null, LevelModifier = null,
                RawRecord = [0xFF, 0xFF, 0x40, 0, 0x82, 0, 0, 0, 3, 1, 0, 0, 0, 0xFF, 0, 0, 0, 0, 0, 0],
                SupplementalRecord = []
            }
        };
    }

    private static BattleSession.ExpandedHostedContactDisposition ReadDisposition(BattleSession battle, BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("ReadExpandedHostedContactDisposition", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<BattleSession.ExpandedHostedContactDisposition>(method.Invoke(battle, [command, header]));
    }

    private static void ValidateGeometry(ImmutableArray<ExpandedBattleContact> contacts)
    {
        MethodInfo method = typeof(BattleSession).GetMethod("ValidateEffect64Geometry", BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, [Header, contacts]);
    }
}
