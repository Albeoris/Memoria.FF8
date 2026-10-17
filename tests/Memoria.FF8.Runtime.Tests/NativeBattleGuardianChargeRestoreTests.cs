using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleGuardianChargeRestoreTests
{
    [Fact]
    public void OrdinaryCommand3OwnsChargeWithoutAnActionHeaderAndRetainsItsGrowingRawGauge()
    {
        BattleSnapshot snapshot = RawCharge();
        BattleGuardianForceCast cast = Assert.Single(snapshot.GuardianForceCasts);
        Assert.Equal((ushort)1060, cast.InitialTicks);
        Assert.Equal((ushort)1058, cast.RemainingTicks);
        Assert.Null(cast.Command.NativeAction);
        Assert.Null(cast.Command.NativeHeaderIndex);
        Assert.Equal((byte?)254, cast.Command.NativeQueuedCommand);
        Assert.Equal(new NativeBattleAtbState(12000, 204), snapshot.Combatants[0].NativeAtb);
        Assert.Equal(170, snapshot.Combatants[0].Atb);
        Assert.Equal(0, snapshot.NativeActionPackets!.ActionCount);
        NativeBattleScriptTask task = snapshot.NativeScriptState.Tasks.Slots[CastSlot(snapshot)];
        Assert.Equal((byte)0, task.ReadByte(12));
        Assert.Equal((byte)254, task.ReadByte(13));
        Assert.Equal((byte)64, task.ReadByte(14));
        //Target flags0 retain the native8000 group marker alongside selected physical slot3.
        Assert.Equal((ushort)0x8008, task.ReadWord(10));
        BattleSession restored = RestoreChargeOwners(snapshot);
        Assert.Equal(cast, Assert.Single(restored.GuardianForceCasts));
        Assert.Equal(snapshot.Combatants[0].NativeAtb, restored.Combatants[0].NativeAtb);
        Assert.True(restored.Combatants[0].IsBusy);
        Invoke(restored, "DispatchGuardianForceCasts");
        Assert.Equal((ushort)1056, Assert.Single(restored.GuardianForceCasts).RemainingTicks);
        Assert.Equal(new NativeBattleAtbState(12000, 204), restored.Combatants[0].NativeAtb);
    }

    [Fact]
    public void ComponentChargeCannotUseARawGaugeWithoutTheSavedGraphicsPath()
    {
        BattleSnapshot snapshot = RawCharge() with { NativeGraphics = null };
        AssertRestoreError<InvalidDataException>(snapshot);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RawChargeStillRequiresItsValidatedGaugeAndMatchingHud(bool missing)
    {
        BattleSnapshot snapshot = RawCharge();
        BattleCombatantSnapshot actor = snapshot.Combatants[0];
        actor = missing ? actor with { NativeAtb = null } : actor with { Atb = 171 };
        AssertRestoreError<InvalidDataException>(snapshot with { Combatants = [actor, .. snapshot.Combatants.Skip(1)] });
    }

    [Theory]
    [InlineData(12, (byte)1)]
    [InlineData(13, (byte)3)]
    [InlineData(14, (byte)65)]
    public void ChargeTaskRequiresItsOriginalActorFeCommandAndGfId(int offset, byte value)
    {
        BattleSnapshot snapshot = RawCharge();
        int slot = CastSlot(snapshot);
        NativeBattleScriptTasks tasks = snapshot.NativeScriptState.Tasks;
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, tasks.Slots[slot].WriteByte(offset, value)) };
        AssertRestoreError<InvalidDataException>(snapshot with { NativeScriptState = snapshot.NativeScriptState with { Tasks = tasks } });
    }

    [Fact]
    public void MissingChargeCallbackCannotBeReconstructedFromTheCastRecord()
    {
        BattleSnapshot snapshot = RawCharge();
        NativeBattleScriptTasks tasks = snapshot.NativeScriptState.Tasks;
        int slot = CastSlot(snapshot);
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, tasks.Slots[slot].WriteDword(0, 0x48E8B0)) };
        AssertRestoreError<InvalidDataException>(snapshot with { NativeScriptState = snapshot.NativeScriptState with { Tasks = tasks } });
    }

    [Fact]
    public void DuplicateChargeCallbacksCannotShareOneCanonicalCastRecord()
    {
        BattleSnapshot snapshot = RawCharge();
        NativeBattleScriptTasks tasks = snapshot.NativeScriptState.Tasks;
        (tasks, int slot) = tasks.Allocate(0x48E620);
        NativeBattleScriptTask task = tasks.Slots[slot].WriteByte(12, 0).WriteByte(13, 254).WriteByte(14, 64);
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, task) };
        AssertRestoreError<InvalidDataException>(snapshot with { NativeScriptState = snapshot.NativeScriptState with { Tasks = tasks } });
    }

    [Fact]
    public void UnknownTaskCommandByteCannotBeSeededFromTheLogicalCast()
    {
        BattleSnapshot snapshot = RawCharge();
        NativeBattleScriptTasks tasks = snapshot.NativeScriptState.Tasks;
        int slot = CastSlot(snapshot);
        NativeBattleScriptTask task = tasks.Slots[slot] with { KnownBytes = (ushort)(tasks.Slots[slot].KnownBytes & ~(1 << 13)) };
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, task) };
        AssertRestoreError<NotSupportedException>(snapshot with { NativeScriptState = snapshot.NativeScriptState with { Tasks = tasks } });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RetainedGaugeDoesNotBypassCastActorAbilityTicksOrEquipment(int corruption)
    {
        BattleSnapshot snapshot = RawCharge();
        BattleGuardianForceCast cast = Assert.Single(snapshot.GuardianForceCasts);
        snapshot = corruption switch
        {
            0 => snapshot with { GuardianForceCasts = [cast with { Command = cast.Command with { ActorId = "enemy" } }] },
            1 => snapshot with { GuardianForceCasts = [cast with { Command = cast.Command with { Ability = BattleAbility.Magic } }] },
            2 => snapshot with { GuardianForceCasts = [cast with { RemainingTicks = 1061 }] },
            3 => snapshot with { Definition = snapshot.Definition with { Combatants = [snapshot.Definition.Combatants[0] with { EquipmentIds = [] }, .. snapshot.Definition.Combatants.Skip(1)] } },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        AssertRestoreError<InvalidDataException>(snapshot);
    }

    [Fact]
    public void JsonRetainsBothIndependentChargeAndGaugeCounters()
    {
        BattleSnapshot original = RawCharge();
        BattleSnapshot saved = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original))!;
        BattleSession first = RestoreChargeOwners(original);
        BattleSession second = RestoreChargeOwners(saved);
        Invoke(first, "DispatchGuardianForceCasts");
        Invoke(second, "DispatchGuardianForceCasts");
        Assert.Equal(JsonSerializer.Serialize(first.GuardianForceCasts), JsonSerializer.Serialize(second.GuardianForceCasts));
        Assert.Equal(first.Combatants[0].NativeAtb, second.Combatants[0].NativeAtb);
    }

    private static BattleSnapshot RawCharge(byte targetFlags = 0)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { Name = "Thunder Storm", MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = targetFlags, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleDefinition definition = new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            GuardianForces = [new(0, 300, 300)],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 0, "Thunder Storm", BattleActionEffect.Native, 0, false) { NativeAction = native }],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [0], GuardianForceCompatibility = Enumerable.Repeat((ushort)3100, 16).ToArray(), Speed = 21 }, new() { Id = "enemy", Name = "Enemy" }]
        };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        Property(nameof(BattleSession.NativeGraphics)).SetValue(session, new NativeBattleGraphicsState([], [], 0, 0) { EntryReady = true });
        session.QueueAction("party", "enemy", BattleAbility.GuardianForce, 0);
        Assert.Equal((byte?)3, Assert.Single(session.QueuedActions).NativeQueuedCommand);
        Invoke(session, "DispatchAction");
        Assert.Equal((ushort)1060, Assert.Single(session.GuardianForceCasts).InitialTicks);
        Method("RunNativeDescriptorActionCallback").Invoke(session, [session.NativeGraphics!, 0x47DDA0u]);
        for (int service = 0; service < 4; service++)
        {
            Method("AdvanceNativeAtbParticipant").Invoke(session, [session.Combatants[0], null]);
        }
        Invoke(session, "DispatchGuardianForceCasts");
        return session.Capture();
    }

    private static BattleSession RestoreChargeOwners(BattleSnapshot saved)
    {
        //Exercise the reached restore methods in their real order without inventing model/texture fixtures.
        //The installed App regression owns full GameSession.Load and independent rendered continuation.
        BattleSession target = new(saved.Definition, saved.Mode);
        BattleCombatantSnapshot actor = saved.Combatants[0];
        typeof(BattleSession).GetMethod("RestoreNativeStatus", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [target.Combatants[0], actor.NativeStatus]);
        Method("RestoreNativeAtb").Invoke(target, [target.Combatants[0], actor.NativeAtb, actor.Atb]);
        Method("RestoreGuardianForces").Invoke(target, [saved]);
        return target;
    }

    private static void AssertRestoreError<T>(BattleSnapshot saved) where T : Exception
    {
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => RestoreChargeOwners(saved));
        Assert.IsType<T>(error.InnerException);
    }

    private static int CastSlot(BattleSnapshot saved) => Assert.Single(Enumerable.Range(0, 16), index => saved.NativeScriptState.Tasks.Allocated(index) && saved.NativeScriptState.Tasks.Slots[index].ReadDword(0) == 0x48E620);
    private static void Invoke(BattleSession session, string name) => Method(name).Invoke(session, null);
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static PropertyInfo Property(string name) => typeof(BattleSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!;
}
