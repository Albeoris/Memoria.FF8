using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledEnemyGuestMessageTests
{
    private const byte Flare = 15;
    private const byte Death = 43;

    [Theory]
    [InlineData(509)]
    [InlineData(510)]
    [InlineData(981)]
    public void Original_reserve_scripts_admit_on_the_installed_formation(ushort formation)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, knockedOutAlly: formation == 981, survivorHpBonus: formation == 509 ? (ushort)9999 : null), formation, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.NotNull(battle.Presentation);
        TickUntil(battle, () => Settled(battle) && (formation != 981 || battle.Combatants.Any(IsLoadedReserve)), "original formation entry");

        if (formation == 509)
        {
            BattleCombatant hidden = Assert.Single(battle.Combatants, actor => actor.ModelId == 123 && actor.IsActive);
            BattleCombatant initial = Assert.Single(battle.Combatants, actor => actor.ModelId == 122 && actor.IsActive);
            Assert.Equal(3, hidden.NativeActorIndex);
            Assert.Equal(4, initial.NativeActorIndex);
            Assert.True(hidden.Untargetable);
            Assert.DoesNotContain(battle.Events, item => item.Kind == "Queued" && item.ActorId == hidden.Id && item.Amount == 352);

            BattleSession restored = battle.ForkForContinuation();
            byte initialTurn = initial.AiTurnCount;
            SetReadyActor(battle, initial);
            TickUntil(battle, () => initial.AiTurnCount > initialTurn && Settled(battle), "model 122 original Turn");
            Assert.Equal(initialTurn + 1, initial.AiTurnCount);
            Assert.True(hidden.IsActive);
            BattleCombatant restoredInitial = Assert.Single(restored.Combatants, actor => actor.Id == initial.Id);
            Assert.Contains(restored.Combatants, actor => actor.Id == hidden.Id && actor.IsActive && actor.Untargetable);
            SetReadyActor(restored, restoredInitial);
            TickUntil(restored, () => restoredInitial.AiTurnCount > initialTurn && Settled(restored), "model 122 restored Turn");
            Assert.Equal(initial.AiTurnCount, restoredInitial.AiTurnCount);
            Assert.Equal(battle.Capture().Random!.Indexes.ToArray(), restored.Capture().Random!.Indexes.ToArray());

            BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-0");
            BattleCombatant observer = battle.Combatants.Single(actor => actor.Id == "party-1");
            for (int cast = 0; cast < 10 && initial.IsActive && initial.IsAlive; cast++)
            {
                CastMagic(battle, caster, initial, observer, Flare);
            }
            TickUntil(battle, () => !initial.IsActive && Settled(battle) && battle.Combatants.Any(IsLoadedReserve), "model 122 Death and original reserve allocation");
            BattleCombatant replacement = Assert.Single(battle.Combatants, IsLoadedReserve);
            Assert.Equal(3, replacement.SceneSlot);
            Assert.Equal(5, replacement.NativeActorIndex);
            return;
        }
        if (formation == 510)
        {
            Assert.Equal(new[] { 124, 125, 125 }, battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).Select(actor => actor.ModelId));
            return;
        }

        BattleCombatant reserve = Assert.Single(battle.Combatants, IsLoadedReserve);
        Assert.Equal(7, reserve.SceneSlot);
        Assert.Equal(6, reserve.NativeActorIndex);
        Assert.NotNull(reserve.ResolvedNativeLevel);
        NativeBattleAiDefinition program = Assert.IsType<NativeBattleAiDefinition>(reserve.NativeAi);
        NativeBattleAiHeapTextDirectory textDirectory = Assert.IsType<NativeBattleAiHeapTextDirectory>(program.HeapTextDirectory);
        Assert.Empty(program.Texts);
        Assert.Equal(new byte[] { 26, 0 }, program.Scripts[1][15..17]);
        Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.IsActive && !actor.IsAlive);

        BattleModelOwner owner = battle.ReadPresentationModelOwner(reserve.Id);
        NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
        int resourceRow = Assert.IsType<int>(graphics.ActorModels.Get(owner).OriginalResourceRow104);
        BattleSnapshot beforeTurn = battle.Capture();
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Fork(battle.CaptureRetainedBattleMemory(beforeTurn, NativeFieldRuntimeProfile.FF8EnExecutableHash));
        NativeBattleModelCpuResources cpu = retained.ModelCpuResources!;
        NativeBattleFileStaging staging = retained.FileStaging!;
        NativeBattleModelCpuReference section = cpu.Rows[resourceRow].Require(32);
        uint entry = checked(section.NativePlacementAddress + (uint)textDirectory.Offset);
        Assert.Equal(checked(NativeBattleModelCpuResources.EnemyAddress + (uint)cpu.EnemyCursor!.Value), entry);
        Assert.Equal(new byte[] { 0, 0 }, NativeBattleModelEnemyHeap.ReadGuest(cpu, staging, section, entry, 2));

        BattleEvent message = RunReserveTurn(battle, reserve.Id);
        Assert.Equal(entry, message.NativeAiHeapMessage!.EntryAddress);
        Assert.Equal(0, message.NativeAiHeapMessage.EntryOffset);
        Assert.Equal(entry, message.NativeAiHeapMessage.TextAddress);
        Assert.Equal(new byte[] { 0 }, message.NativeAiHeapMessage.Encoded.ToArray());
        Assert.Equal(string.Empty, message.Message);
        Assert.Equal(3, message.NativeMessageMode);
        Assert.Null(message.NativeTextIdentity);

        if (!attached)
        {
            BattleSession restored = BattleSession.Restore(beforeTurn);
            restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            BattleEvent replay = RunReserveTurn(restored, reserve.Id);
            Assert.Equal(message.ActorId, replay.ActorId);
            Assert.Equal(message.Message, replay.Message);
            Assert.Equal(message.NativeAiHeapMessage.EntryAddress, replay.NativeAiHeapMessage!.EntryAddress);
            Assert.Equal(message.NativeAiHeapMessage.Encoded.ToArray(), replay.NativeAiHeapMessage.Encoded.ToArray());
            Assert.Equal(battle.Capture().Random!.Indexes.ToArray(), restored.Capture().Random!.Indexes.ToArray());
        }
    }

    [Fact]
    public void Formation510_reserve_turn_reads_the_adjacent_private_skeleton()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, knockedOutAlly: true, survivorHpBonus: 9999), 510, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        TickUntil(battle, () => Settled(battle), "formation 510 entry");
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party-0");
        BattleCombatant observer = battle.Combatants.Single(actor => actor.Id == "party-1");
        foreach (BattleCombatant incumbent in battle.Combatants.Where(actor => actor.ModelId == 125 && actor.IsActive).ToArray())
        {
            BattleModelOwner owner = battle.ReadPresentationModelOwner(incumbent.Id);
            Assert.True(incumbent.NativeAi?.DeathSequenceReleasesAllocation);
            for (int cast = 0; cast < 10 && incumbent.IsAlive; cast++)
            {
                CastMagic(battle, caster, incumbent, observer, Flare);
            }
            TickUntil(battle, () => !incumbent.IsAlive && (!process.Graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) || (model.Flags0 & 1) == 0), "original model 125 death release");
        }
        TickUntil(battle, () => battle.ObserveDiagnosticDrainBlockers().Count == 0, "original model 125 work drain");
        BattleCombatant original = Assert.Single(battle.Combatants, actor => actor.ModelId == 124 && actor.IsActive);
        for (int turn = 0; turn < 16 && !battle.Combatants.Any(actor => IsLoadedReserve(actor) && actor.SceneSlot == 5); turn++)
        {
            byte priorTurn = original.AiTurnCount;
            SetReadyActor(battle, original, observer);
            TickUntil(battle, () => original.AiTurnCount > priorTurn && Settled(battle), $"model 124 Turn {priorTurn + 1}");
        }

        BattleCombatant first = Assert.Single(battle.Combatants, actor => IsLoadedReserve(actor) && actor.SceneSlot == 4);
        BattleCombatant second = Assert.Single(battle.Combatants, actor => IsLoadedReserve(actor) && actor.SceneSlot == 5);
        Assert.Equal(4, first.NativeActorIndex);
        Assert.Equal(5, second.NativeActorIndex);
        if (!battle.Combatants.Any(IsAllocatedKnockout))
        {
            Assert.True(observer.IsActive && observer.IsAlive);
            for (int attempt = 0; attempt < 3 && observer.IsAlive; attempt++)
            {
                CastMagic(battle, caster, observer, observer, Death);
            }
        }
        Assert.Contains(battle.Combatants, IsAllocatedKnockout);
        BattleSnapshot before = battle.Capture();
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Fork(battle.CaptureRetainedBattleMemory(before, NativeFieldRuntimeProfile.FF8EnExecutableHash));
        NativeBattleModelCpuResources cpu = retained.ModelCpuResources!;
        NativeBattleFileStaging staging = retained.FileStaging!;
        NativeBattleModelCpuResourceRow firstRow = cpu.Rows[process.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(first.Id)).OriginalResourceRow104!.Value];
        NativeBattleModelCpuResourceRow secondRow = cpu.Rows[process.Graphics.ActorModels.Get(battle.ReadPresentationModelOwner(second.Id)).OriginalResourceRow104!.Value];
        NativeBattleModelCpuReference section = firstRow.Require(32);
        NativeBattleAiHeapTextDirectory table = Assert.IsType<NativeBattleAiHeapTextDirectory>(first.NativeAi!.HeapTextDirectory);
        uint entry = checked(section.NativePlacementAddress + (uint)table.Offset);
        Assert.Equal(section.NativePlacementAddress, secondRow.Require(32).NativePlacementAddress);
        Assert.Equal(entry, secondRow.Require(12).NativePlacementAddress);
        Assert.Equal(new byte[] { 0x12, 0x00 }, NativeBattleModelEnemyHeap.ReadGuest(cpu, staging, section, entry, 2));

        BattleEvent message = RunReserveTurn(battle, first.Id, caster);
        Assert.Equal(entry, message.NativeAiHeapMessage!.EntryAddress);
        Assert.Equal(0x12, message.NativeAiHeapMessage.EntryOffset);
        Assert.Equal(entry + 0x12, message.NativeAiHeapMessage.TextAddress);
        Assert.Equal(new byte[] { 0 }, message.NativeAiHeapMessage.Encoded.ToArray());
    }

    private static void CastMagic(BattleSession battle, BattleCombatant caster, BattleCombatant target, BattleCombatant observer, byte spell)
    {
        SetReadyActor(battle, caster, observer);
        TickUntil(battle, () => battle.CanChoosePlayerCommand(caster.Id), "player ready to cast installed magic");
        int firstEvent = battle.Events.Count;
        battle.QueueAction(caster.Id, target.Id, BattleAbility.Magic, spell);
        TickUntil(battle, () => Settled(battle) && battle.Events.Skip(firstEvent).Any(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id), "installed magic and original contact completion");
    }

    private static void PrepareParty(GameSession session, bool knockedOutAlly, ushort? survivorHpBonus = null)
    {
        NativeCharacterVitals vitals = session.State.CharacterVitals!.Rest();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (int row in new[] { 0, 3 })
        {
            records[row] = records[row] with { MemberFlags = (ushort)(records[row].MemberFlags | 1), Status = 0 };
        }
        if (survivorHpBonus is ushort bonus)
        {
            byte[] stats = records[0].StatBonuses.ToArray();
            stats[2] = byte.MaxValue;
            NativeMagicStock[] spells = records[0].MagicInventory.ToArray();
            spells[0] = new(Flare, 20);
            spells[1] = new(Death, 3);
            int magic = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2);
            Assert.True(magic >= 0);
            records[0] = records[0] with { HitPointBonus = bonus, StatBonuses = stats, BonusSources = null, MagicInventory = spells, Junction = records[0].Junction with { Commands = checked((uint)(20 + magic)) } };
            records[1] = records[1] with { HitPointBonus = bonus, BonusSources = null, MemberFlags = (ushort)(records[1].MemberFlags | 1), Status = 0 };
        }
        byte[] party = survivorHpBonus.HasValue ? [0, 1, 3] : [0, 3, byte.MaxValue];
        vitals = (vitals with { CombatParty = party, Records = records }).RebuildParty().Rest();
        if (knockedOutAlly)
        {
            records = vitals.Records.ToArray();
            records[3] = records[3] with { HitPoints = new(0, 0), Status = 1 };
            vitals = vitals with { Records = records };
        }
        session.SetCharacterVitals(vitals);
    }

    private static BattleEvent RunReserveTurn(BattleSession battle, string reserveId, BattleCombatant? observer = null)
    {
        BattleCombatant reserve = battle.Combatants.Single(actor => actor.Id == reserveId);
        byte priorTurn = reserve.AiTurnCount;
        int firstEvent = battle.Events.Count;
        SetReadyActor(battle, reserve, observer);
        TickUntil(battle, () => reserve.AiTurnCount > priorTurn && Settled(battle) && Messages().Any(), "model 0 Turn and native message task");
        for (int callback = 0; callback < 32; callback++)
        {
            battle.Tick();
        }
        Assert.Equal(priorTurn + 1, reserve.AiTurnCount);
        return Assert.Single(Messages());

        IEnumerable<BattleEvent> Messages() => battle.Events.Skip(firstEvent).Where(message => message.ActorId == reserveId && message.NativeAiHeapMessage is not null);
    }

    private static void SetReadyActor(BattleSession battle, BattleCombatant ready, BattleCombatant? observer = null)
    {
        foreach (BattleCombatant actor in battle.Combatants)
        {
            actor.Atb = 0;
        }
        ready.Atb = BattleSession.AtbMaximum;
        if (observer is not null)
        {
            observer.Atb = BattleSession.AtbMaximum;
        }
    }

    private static bool IsLoadedReserve(BattleCombatant actor) => !actor.IsPlayer && actor.ModelId == 0 && actor.IsActive && actor.ResolvedNativeLevel is not null && !actor.NativeAllocationPendingStatistics;

    private static bool IsAllocatedKnockout(BattleCombatant actor) => actor.IsPlayer && actor.IsActive && actor.NativeActorIndex is >= 0 and < 3 && (actor.NativeStatus0 & 1) != 0;

    private static bool Settled(BattleSession battle) => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null &&
        battle.NativeScriptState.AllocationControl?.ActionGate != true && battle.Presentation is not { HasActiveAction: true } and not { HasPendingModelLoad: true };

    private static void TickUntil(BattleSession battle, Func<bool> reached, string boundary)
    {
        for (int callback = 0; callback < 1200 && !reached(); callback++)
        {
            battle.Tick();
        }
        string roster = string.Join(", ", battle.Combatants.Where(actor => actor.IsActive).Select(actor => $"{actor.Id} HP={actor.CurrentHp} Turn={actor.AiTurnCount}"));
        string events = string.Join(", ", battle.Events.TakeLast(8).Select(message => $"{message.Kind}/{message.ActorId}/{message.TargetId}"));
        Assert.True(reached(), $"Timed out at {boundary}. Outcome={battle.Outcome}, active={battle.ActiveAction?.ActorId}, allocation={battle.NativeScriptState.AllocationControl}. {roster}. {events}");
    }
}
