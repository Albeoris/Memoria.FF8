using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanTests
{
    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(159)]
    public void NativeScanSetsTheOriginalModelBitAndPersistsItAcrossStandaloneRestore(int model)
    {
        BattleSession battle = Create(model);

        Hit(battle);

        Assert.True(battle.Capture().NativeScannedModels!.Contains(model));
        Assert.True(BattleSession.Restore(battle.Capture()).Capture().NativeScannedModels!.Contains(model));
        Assert.Single(battle.Events, entry => entry.Kind == "Scan");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void RepeatedScanUsesTheActualSavedAlwaysShowOption(bool alwaysShow, int messages)
    {
        BattleSession battle = Create(alwaysShow: alwaysShow);
        Hit(battle);
        BattleSnapshot snapshot = battle.Capture() with { ActiveAction = null };
        snapshot.Combatants[0] = snapshot.Combatants[0] with { Atb = BattleSession.AtbMaximum };
        battle = BattleSession.Restore(snapshot);

        Hit(battle);

        Assert.Equal(messages, battle.Events.Count(entry => entry.Kind == "Scan"));
        Assert.True(battle.Capture().NativeScannedModels!.Contains(1));
    }

    [Theory]
    [InlineData(61, false, 3, 0)]
    [InlineData(61, true, 1, 1)]
    [InlineData(77, false, 1, 1)]
    public void Contact_flags_use_descriptor_model_history_and_preserve_success_bit_when_known_Scan_is_suppressed(int priorModel, bool alwaysShow, byte flags, int messages)
    {
        BattleSession battle = Create(61, alwaysShow, history: Empty().Add(priorModel));
        battle.QueueNativePlayerCommand("party", "enemy", 2, 1);
        for (int admission = 0; admission < 7 && battle.NativeActionPackets.ActionCount == 0; admission++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        NativeBattleContactPacket packet = battle.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value);
        Assert.Equal(flags, packet.Primary.Flags2);
        Assert.Equal(messages, battle.Events.Count(entry => entry.Kind == "Scan"));
        NativeScannedModels after = battle.Capture().NativeScannedModels!;
        Assert.True(after.Contains(61));
        Assert.True(after.Contains(priorModel));
        Assert.Equal(after.Words, BattleSession.Restore(battle.Capture()).Capture().NativeScannedModels!.Words);
    }

    [Fact]
    public void UnknownOldHistoryFailsAtScanInsteadOfInventingAnEmptySavedBank()
    {
        BattleSession battle = Create(known: false);

        Assert.Contains("current saved160-bit", Assert.Throws<NotSupportedException>(() => Hit(battle)).Message);
    }

    [Fact]
    public void MissingSnapshotHistoryAndBitClearingReceiptsAreRejected()
    {
        BattleSession battle = Create();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { NativeScannedModels = null }));
        NativeScannedModels before = Empty().Add(31);
        Assert.Throws<InvalidDataException>(() => NativeScannedModels.ValidateReceipt(before, Empty()));
    }

    private static NativeScannedModels Empty()
    {
        return new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0, 0, 0, 0));
    }

    private static void Hit(BattleSession battle)
    {
        battle.QueueAction("party", "enemy", BattleAbility.Magic, 1);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(int enemyModel = 1, bool alwaysShow = false, bool known = true, NativeScannedModels? history = null)
    {
        BattleNativeAction scan = new(2, 1, 1, 1)
        {
            MagicId = 1,
            AttackType = 12,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Name = "Native Scan component",
            NativeScannedModels = known ? history ?? Empty() : null,
            NativeScanAlwaysShow = alwaysShow,
            NativeActions = [scan],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Scan", BattleActionEffect.Native, 0, false) { NativeAction = scan }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MagicStocks = [new(1, 2)], Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeCommands = [new(2, "Scan", 0x50, 1)] },
                new() { Id = "enemy", Name = "Enemy", ModelId = enemyModel }
            ]
        }, BattleAtbMode.Normal);
    }
}
