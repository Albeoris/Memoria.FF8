using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleGuardianChargeRestoreTests
{
    [Fact]
    public void Successful48E620QueuesFe64WithItsFullGaugeAndReleasesTheChargeOwner()
    {
        BattleSnapshot ready = RawReadyInput();
        Assert.Empty(ready.GuardianForceCasts);
        Assert.Null(ready.ActiveAction);
        Assert.Empty(ready.QueuedActions);
        Assert.Equal(new NativeBattleAtbState(12000, 12000), ready.Combatants[0].NativeAtb);
        Assert.Equal(BattleSession.AtbMaximum, ready.Combatants[0].Atb);
        Assert.Equal(0u, ready.Combatants[0].NativeStatus!.Battle & 0x80000000u);
        NativeBattleCommandBank bank = ready.NativeCommandBanks!.Banks[1];
        BattleAttackCommand command = Assert.IsType<BattleAttackCommand>(bank.LogicalCommands[0]);
        Assert.Equal((byte?)254, command.NativeQueuedCommand);
        Assert.Null(command.NativeAction);
        Assert.Null(command.NativeHeaderIndex);
        Assert.Equal((ushort)64, bank.Packets[0].ReadWord(4));
        Assert.Equal((ushort)0x80F8, bank.Packets[0].ReadWord(6));
        BattleSession restored = RestoreReadyInputOwners(ready);
        Assert.Equal(command, Assert.Single(restored.QueuedActions));
        Assert.Equal(new NativeBattleAtbState(12000, 12000), restored.Combatants[0].NativeAtb);
        Assert.Equal(0, restored.NativeActionPackets.ActionCount);
    }

    [Fact]
    public void ReadyInputJsonPreservesPhysicalBankAndCounterOwners()
    {
        BattleSnapshot saved = RawReadyInput();
        BattleSnapshot json = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!;
        BattleSession first = RestoreReadyInputOwners(saved);
        BattleSession second = RestoreReadyInputOwners(json);
        Assert.Equal(JsonSerializer.Serialize(first.NativeCommandBanks), JsonSerializer.Serialize(second.NativeCommandBanks));
        Assert.Equal(JsonSerializer.Serialize(first.GuardianForces), JsonSerializer.Serialize(second.GuardianForces));
        Assert.Equal(first.Combatants[0].NativeAtb, second.Combatants[0].NativeAtb);
        Assert.Empty(first.Events);
        Assert.Empty(second.Events);
    }

    [Fact]
    public void InitialCommand3UsesItsOrdinaryBankAndSelectedTargetWithoutAnInlineAction()
    {
        BattleSnapshot saved = RawReadyInput();
        BattleSession source = RestoreReadyInputOwners(saved);
        BattleAttackCommand command = Assert.Single(source.QueuedActions) with { NativeQueuedCommand = 3, NativeTargetMask = 0 };
        typeof(BattleSession).GetField("nativeCommandBanks", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, NativeBattleCommandBanks.Initialize());
        Method("EnqueueNativeInputCommand").Invoke(source, [command]);
        BattleSnapshot queued = source.Capture();
        Assert.Equal((byte)3, queued.NativeCommandBanks!.Banks[2].Packets[0].ReadByte(1));
        Assert.Equal((ushort)8, queued.NativeCommandBanks.Banks[2].Packets[0].ReadWord(6));
        BattleSession restored = RestoreReadyInputOwners(queued);
        Assert.Equal(command, Assert.Single(restored.QueuedActions));
    }

    [Fact]
    public void FeReadyCommandCannotMoveIntoTheOrdinaryInputBank()
    {
        BattleSnapshot saved = RawReadyInput();
        NativeBattleCommandBanks banks = saved.NativeCommandBanks!;
        banks = banks with { Banks = banks.Banks.SetItem(2, banks.Banks[1]).SetItem(1, NativeBattleCommandBanks.Initialize().Banks[1]) };
        AssertReadyInputError<InvalidDataException>(saved with { NativeCommandBanks = banks });
    }

    [Theory]
    [InlineData(0, (byte)1)]
    [InlineData(1, (byte)3)]
    [InlineData(2, (byte)1)]
    [InlineData(3, (byte)1)]
    [InlineData(4, (byte)65)]
    [InlineData(6, (byte)7)]
    public void ReadyInputPhysicalActorCommandFlagsIdAndTargetRemainStrict(int offset, byte value)
    {
        BattleSnapshot saved = RawReadyInput();
        NativeBattleCommandBanks banks = saved.NativeCommandBanks!;
        NativeBattleCommandBank bank = banks.Banks[1];
        NativeBattleCommandPacket packet = bank.Packets[0] with { Bytes = bank.Packets[0].Bytes.SetItem(offset, value) };
        banks = banks with { Banks = banks.Banks.SetItem(1, bank with { Packets = bank.Packets.SetItem(0, packet) }) };
        AssertReadyInputError<InvalidDataException>(saved with { NativeCommandBanks = banks });
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void UnknownReadyInputIdOrTargetCannotBeFilledFromTheLogicalCommand(int offset)
    {
        BattleSnapshot saved = RawReadyInput();
        NativeBattleCommandBanks banks = saved.NativeCommandBanks!;
        NativeBattleCommandBank bank = banks.Banks[1];
        NativeBattleCommandPacket packet = bank.Packets[0] with { UnknownByteMask = (ushort)(bank.Packets[0].UnknownByteMask | 1 << offset) };
        banks = banks with { Banks = banks.Banks.SetItem(1, bank with { Packets = bank.Packets.SetItem(0, packet) }) };
        AssertReadyInputError<NotSupportedException>(saved with { NativeCommandBanks = banks });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RawBankGaugeStillRequiresLivingEquippedInstalledGf(int corruption)
    {
        BattleSnapshot saved = RawReadyInput();
        saved = corruption switch
        {
            0 => saved with { GuardianForces = [saved.GuardianForces[0] with { HitPoints = 0 }] },
            1 => saved with { Definition = saved.Definition with { Combatants = [saved.Definition.Combatants[0] with { EquipmentIds = [] }, .. saved.Definition.Combatants.Skip(1)] } },
            2 => saved with { Definition = saved.Definition with { ActionDefinitions = [] } },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        if (corruption == 2)
        {
            AssertReadyInputError<NotSupportedException>(saved);
        }
        else
        {
            AssertReadyInputError<InvalidDataException>(saved);
        }
    }

    [Fact]
    public void ReadyInputCannotAlsoRetainItsUnfinishedCharge()
    {
        BattleSnapshot saved = RawReadyInput();
        BattleSnapshot charging = RawCharge(0x40);
        saved = saved with { GuardianForceCasts = charging.GuardianForceCasts, NativeScriptState = charging.NativeScriptState };
        AssertReadyInputError<InvalidDataException>(saved);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PhysicalReadyBankDoesNotInventAGaugeOrBypassHudValidation(bool missing)
    {
        BattleSnapshot saved = RawReadyInput();
        BattleCombatantSnapshot actor = saved.Combatants[0];
        actor = missing ? actor with { NativeAtb = null } : actor with { Atb = BattleSession.AtbMaximum - 1 };
        AssertReadyInputError<InvalidDataException>(saved with { Combatants = [actor, .. saved.Combatants.Skip(1)] });
    }

    [Fact]
    public void ComponentBankRetainsItsZeroGaugeInvariant()
    {
        AssertReadyInputError<InvalidDataException>(RawReadyInput() with { NativeGraphics = null });
    }

    [Fact]
    public void UnbankedManagedGfDoesNotBorrowThePhysicalBankAdmission()
    {
        BattleSnapshot saved = RawReadyInput();
        BattleSession target = RestoreReadyInputOwners(saved);
        BattleAttackCommand command = Assert.Single(target.QueuedActions);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Method("ValidateSavedAction").Invoke(target, [command.ActorId, command.TargetId, new HashSet<string>(), command.Ability, command.ActionId, true, null, command.NativeTargetMask, null, null, null, null, false, null, null]));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static BattleSnapshot RawReadyInput()
    {
        BattleSnapshot pending = RawCharge(0x40);
        BattleCombatantSnapshot actor = pending.Combatants[0] with { NativeAtb = new(12000, 12000), Atb = BattleSession.AtbMaximum };
        pending = pending with { Combatants = [actor, .. pending.Combatants.Skip(1)], GuardianForceCasts = [pending.GuardianForceCasts[0] with { RemainingTicks = 0 }] };
        BattleSession session = RestoreChargeOwners(pending);
        Property(nameof(BattleSession.NativeGraphics)).SetValue(session, pending.NativeGraphics);
        bool completed = (bool)Method("PollNativeGuardianScriptTask").Invoke(session, [pending.NativeScriptState.Tasks.Slots[CastSlot(pending)]])!;
        Assert.True(completed);
        return session.Capture();
    }

    private static BattleSession RestoreReadyInputOwners(BattleSnapshot saved)
    {
        BattleSession target = RestoreChargeOwners(saved);
        Property(nameof(BattleSession.NativeGraphics)).SetValue(target, saved.NativeGraphics);
        saved.NativeCommandBanks!.Validate();
        typeof(BattleSession).GetField("nativeCommandBanks", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, saved.NativeCommandBanks);
        Method("ValidateNativeCommandBindings").Invoke(target, [new HashSet<string>()]);
        return target;
    }

    private static void AssertReadyInputError<T>(BattleSnapshot saved) where T : Exception
    {
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => RestoreReadyInputOwners(saved));
        Assert.IsType<T>(error.InnerException);
    }
}
