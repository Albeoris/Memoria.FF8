using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticReflectionLineageTests
{
    private static BattleDiagnosticEffectDispatch Source => new(1, 10, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 2) { NativeActorIndex = 3, NativeTargetMask = 8, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
    private static BattleDiagnosticEffectDispatch Child => new(2, 11, "enemy", BattleAbility.EnemyAbility, 31, 106, 106, 247) { NativeActorIndex = 3, NativeTargetMask = 1, NativeHeaderIndex = 1, NativeHeaderGeneration = 2, Reflection = new(0x487DF0, 1, 0, 1, 1, 2, "enemy", 3, "enemy", 3, 0x80, "party", 0, 2, 31, 106, 8, 0x10, 31, 106, 0, 1) };

    [Fact]
    public void Reflection_retains_the_caster_and_changes_only_the_proved_recipient()
    {
        Assert.True(BattleDiagnosticReflectionLineage.Matches(Child, Source));
        BattleDiagnosticEffectDispatch restored = JsonSerializer.Deserialize<BattleDiagnosticEffectDispatch>(JsonSerializer.Serialize(Child))!;
        Assert.Equal(Child, restored);
        Assert.True(BattleDiagnosticReflectionLineage.Matches(restored, Source));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Unlinked_tainted_and_reused_header_receipts_are_rejected(int mutation)
    {
        BattleDiagnosticEffectDispatch child = mutation switch
        {
            0 => Child with { Reflection = null },
            1 => Child with { Reflection = Child.Reflection! with { SourceDispatchSerial = 9 } },
            2 => Child with { Reflection = Child.Reflection! with { SourceHeaderGeneration = 2 } },
            3 => Child with { NativeHeaderGeneration = 3 },
            4 => Child with { NativeTargetMask = 8 },
            5 => Child with { ActorId = "party" },
            6 => Child with { RequestedEffect = 107 },
            7 => Child with { Reflection = Child.Reflection! with { ReflectorStatuses1 = 0 } },
            8 => Child with { Reflection = Child.Reflection! with { SourceAttackFlags = 0 } },
            _ => Child with { Ability = BattleAbility.Magic }
        };
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child, Source));
    }

    [Fact]
    public void A_player_Draw_source_produces_a_Magic_child_with_native_target_modifiers()
    {
        BattleDiagnosticEffectDispatch source = Source with { ActorId = "party", Ability = BattleAbility.Draw, NativeActorIndex = 0, NativeTargetMask = 8 };
        BattleDiagnosticEffectDispatch child = Child with { ActorId = "party", Ability = BattleAbility.Magic, NativeActorIndex = 0, NativeTargetMask = 0x6001, Reflection = Child.Reflection! with { ActorId = "party", ActorIndex = 0, ReflectedTargetFlags = 3, ReflectedTargetMask = 0x6001 } };
        Assert.True(BattleDiagnosticReflectionLineage.Matches(child, source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_header_preparation_invalidates_a_pending_child_after_reuse_or_reset(bool reset)
    {
        BattleNativeAction sourceAction = new(2, 31, 0, 0) { MagicId = 106, AttackType = 8, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 0x10, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleNativeAction reflectedAction = sourceAction with { Command = 247 };
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", NativeStatus1 = 0x80 }] }, BattleAtbMode.TurnBased);
        session.DiagnosticEffectDispatched += static _ => { };
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        (packets, _) = packets.Prepare(new(3, 2, 0, -1, 31, 106, null));
        (packets, _) = packets.Publish(0);
        (packets, _) = packets.Prepare(new(3, 247, 0, -1, 31, 106, null));
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        Invoke(session, "ObserveDiagnosticPreparedHeader", 0);
        BattleDiagnosticEffectDispatch source = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", Source)!;
        object pending = Invoke(session, "PrepareDiagnosticReflection", 0, session.Combatants[1], session.Combatants[1], session.Combatants[0], sourceAction, reflectedAction, (ushort)1)!;
        FieldInfo production = typeof(BattleSession).GetField("diagnosticReflectionProduction", BindingFlags.Instance | BindingFlags.NonPublic)!;
        production.SetValue(session, pending);
        Invoke(session, "ObserveDiagnosticPreparedHeader", 1);
        production.SetValue(session, null);
        BattleDiagnosticEffectDispatch valid = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", Child with { Reflection = null })!;
        Assert.True(BattleDiagnosticReflectionLineage.Matches(valid, source));
        production.SetValue(session, pending);
        Invoke(session, "ObserveDiagnosticPreparedHeader", 1);
        production.SetValue(session, null);
        if (reset)
        {
            Invoke(session, "ResetDiagnosticReflectionHeaders");
        }
        else
        {
            Invoke(session, "ObserveDiagnosticPreparedHeader", 0);
        }
        BattleDiagnosticEffectDispatch stale = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", Child with { Reflection = null })!;
        Assert.Null(stale.Reflection);
        Assert.Equal(reset ? "CHILD_HEADER_GENERATION_MISSING" : "SOURCE_HEADER_GENERATION_STALE", stale.ReflectionRejectionReason);
        Assert.DoesNotContain("diagnosticHeaderGenerations", JsonSerializer.Serialize(session.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_secondary_bank_production_retains_selected_and_normalized_recipients_until_dispatch(bool untargetable)
    {
        BattleNativeAction sourceAction = new(2, 31, 0, 0) { MagicId = 106, Name = "Reflection fixture", AttackType = 8, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 0x10, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 100, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleNativeAction reflectedAction = sourceAction with { Command = 247 };
        BattleSession session = new(new BattleDefinition { InitialRandom = NativeBattleRandomState.ForecastPhase(0), NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = [sourceAction, reflectedAction], Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, NativeStatus1 = 0x80 }, new() { Id = "enemy", Name = "Enemy" }, new() { Id = "other-4", Name = "Other", InitialUntargetable = untargetable }, new() { Id = "other-5", Name = "Other", InitialUntargetable = untargetable }, new() { Id = "other-6", Name = "Other", InitialUntargetable = untargetable }] }, BattleAtbMode.TurnBased);
        session.DiagnosticEffectDispatched += static _ => { };
        BattleAttackCommand command = new("party", "party", BattleAbility.Magic, 31) { NativeQueuedCommand = 2, NativeAction = sourceAction, NativeTargetMask = 1, FreeNativeMagic = true };
        (NativeBattleCommandBanks banks, NativeBattleCommandLocation location) = session.NativeCommandBanks.Allocate(2);
        banks = banks.WriteCommand(location, 0, 0, 2, 0, 0, 31, new ushort[] { 1 }).BindCommand(location, 0, command);
        typeof(BattleSession).GetField("nativeCommandBanks", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, banks);
        typeof(BattleSession).GetField("nativeDispatchCommandLocation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, location);
        Invoke(session, "SetNativeActionEvent", session.Combatants[0], (byte)2);
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 0, -1, 31, 106, null));
        (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0x80), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        Invoke(session, "ObserveDiagnosticPreparedHeader", 0);
        BattleActionPresentation presentation = new("party", "party", BattleActionPhase.Recovery, 1, BattleAbility.Magic, 31) { NativeAction = sourceAction, NativeQueuedCommand = 2, NativeTargetMask = 1, FreeNativeMagic = true };
        typeof(BattleSession).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, NativeBattleRandomState.ForecastPhase(0));
        Invoke(session, "ResolveNativeReflection", session.Combatants[0], session.Combatants[0], presentation, sourceAction, 0);
        Assert.Equal((byte)247, session.NativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot + 1].ReadByte(1));
        Invoke(session, "DispatchNativeSecondaryBankCommand");
        Assert.Equal(2, session.NativeActionPackets.ActionCount);
        BattleDiagnosticEffectDispatch source = new(1, 10, "party", BattleAbility.Magic, 31, 106, 106, 2) { NativeActorIndex = 0, NativeTargetMask = 1, NativeHeaderIndex = 0 };
        source = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", source)!;
        ushort actualMask = untargetable ? (ushort)8 : (ushort)64;
        BattleDiagnosticEffectDispatch child = new(2, 11, "party", BattleAbility.Magic, 31, 106, 106, 247) { NativeActorIndex = 0, NativeTargetMask = actualMask, NativeHeaderIndex = 1 };
        child = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", child)!;
        Assert.Null(child.ReflectionRejectionReason);
        Assert.True(BattleDiagnosticReflectionLineage.Matches(child, source));
        BattleDiagnosticReflection proof = Assert.IsType<BattleDiagnosticReflection>(child.Reflection);
        Assert.Equal((byte)6, proof.RecipientIndex);
        Assert.Equal((ushort)64, proof.ReflectedTargetMask);
        BattleDiagnosticReflectionTargetProvenance targets = Assert.IsType<BattleDiagnosticReflectionTargetProvenance>(proof.TargetProvenance);
        BattleDiagnosticReflectionTargetResolution resolution = Assert.Single(targets.Resolutions);
        Assert.Equal((ushort)64, resolution.InputMask);
        Assert.Equal((ushort)64, resolution.SelectedMask);
        Assert.Equal(untargetable ? (ushort)9 : (ushort)121, resolution.AvailableMask);
        Assert.Equal(actualMask, resolution.ResultMask);
        Assert.Equal(untargetable ? (byte)3 : (byte)6, Assert.Single(targets.ContactRecipients));
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child with { Reflection = proof with { TargetProvenance = targets with { HeaderGeneration = targets.HeaderGeneration + 1 } } }, source));
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child with { Reflection = proof with { TargetProvenance = targets with { ContactRecipients = [0] } } }, source));
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child with { Reflection = proof with { TargetProvenance = targets with { Resolutions = [resolution with { ResultMask = 1 }] } } }, source));
        Assert.False(BattleDiagnosticReflectionLineage.Matches(child with { Reflection = proof with { TargetProvenance = targets with { Resolutions = [resolution with { AvailableMask = 0x80 }] } } }, source));
    }

    [Fact]
    public void Unlinked_native247_records_its_missing_proof_without_accepting_it()
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        session.DiagnosticEffectDispatched += static _ => { };
        Invoke(session, "ObserveDiagnosticPreparedHeader", 1);
        BattleDiagnosticEffectDispatch rejected = (BattleDiagnosticEffectDispatch)Invoke(session, "CompleteDiagnosticReflection", Child with { Reflection = null })!;
        Assert.Null(rejected.Reflection);
        Assert.Equal("PENDING_HEADER_ABSENT", rejected.ReflectionRejectionReason);
        Assert.Equal(0, rejected.ReflectionRejection!.PendingHeaderCount);
        Assert.Null(rejected.ReflectionRejection.Candidate);
    }

    [Fact]
    public void Unobserved_header_preparation_does_not_allocate_diagnostic_maps()
    {
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        Invoke(session, "ObserveDiagnosticPreparedHeader", 0);
        foreach (string field in new[] { "diagnosticHeaderGenerations", "diagnosticHeaderDispatches", "diagnosticReflectedHeaders", "diagnosticReflectedCommands" })
        {
            Assert.Null(typeof(BattleSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session));
        }
    }

    private static object? Invoke(BattleSession session, string method, params object[] arguments)
    {
        return typeof(BattleSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments);
    }
}
