using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class StoryBattleRosterTests
{
    [Theory]
    [InlineData(350, 3, 32, 518, 796, -335, -5704, 21, 128, new int[] { 112, 113 })]
    [InlineData(369, 9, 57, 511, 794, 0, 952, 108, 128, new int[] { 105, 106, 107 })]
    public void OriginalStoryBattleInstructionAdmitsSelectedExtraAllyAndKeepsAuthoredEnemies(int fieldId, int objectId, int scriptId, int operation, int formation, int x, int y, int triangle, int direction, int[] models)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        byte[] party = [5, 0, 1];
        GameSession session = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
        session.StartNewGame();
        using GameWorld world = new(session, directory, sharedAssets: assets, expandedBattleAllyCount: 3) { UsePhysicalBattlePresentation = true };
        world.EnsureField();
        session.SetFullBattlePlaythrough(true);
        NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = party }).WithGuestBattleRecords();
        NativeCharacterVital[] records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1) }).ToArray();
        if (formation == 796)
        {
            // Keep the installed enemy HP and death script. A strong party Attack bounds the fixture's combat turns.
            NativeCharacterVital attacker = records[0];
            NativeStatBonusSources sources = attacker.BonusSources ?? NativeStatBonusSources.FromInitial(attacker.HitPointBonus, attacker.StatBonuses);
            records[0] = attacker.WithBonusSources(sources.Add(NativeStatBonusSources.Items, 0, [255, 0, 0, 0, 0, 0], vitals.LevelBasedStatBonuses));
        }
        session.SetCharacterVitals((vitals with { Records = records }).Rest());
        session.RequestField(new FieldTransition(fieldId, x, y, triangle, direction));
        world.EnsureField();
        world.Events.Step();

        FieldAssets field = Assert.IsType<FieldAssets>(world.Field);
        JsmProgram program = JsmProgram.Read(field.Jsm);
        Assert.Contains(program.Objects[objectId].Scripts, script => script.Id == scriptId && script.ContainsOperation(operation));
        Assert.Equal((Jsm.Opcode.PSHN_L, formation), (program.Operations[operation - 2].Opcode, program.Operations[operation - 2].Parameter));
        Assert.Equal((Jsm.Opcode.PSHN_L, 0), (program.Operations[operation - 1].Opcode, program.Operations[operation - 1].Parameter));
        Assert.Equal(Jsm.Opcode.BATTLE, program.Operations[operation].Opcode);
        Assert.Equal(models, world.BattleResources.LoadEncounter(formation).Enemies.Select(enemy => enemy.ModelId));

        // The component starts at the installed operand pushes after ordinary allocation.
        // The setup holds other controllers during this callback and resumes no preceding scene.
        JsonNode state = JsonNode.Parse(world.Events.Capture().GetRawText())!;
        JsonArray fibers = state["Fibers"]!.AsArray();
        JsonNode owner = Assert.Single(fibers, fiber => fiber!["ObjectId"]!.GetValue<int>() == objectId && fiber["ScriptId"]!.GetValue<int>() == scriptId && !fiber["Initialization"]!.GetValue<bool>())!;
        owner["Position"] = operation - 2;
        foreach (JsonNode? fiber in fibers.Where(fiber => fiber != owner && !fiber!["Complete"]!.GetValue<bool>()))
        {
            fiber!["WaitReason"] = nameof(WaitReason.Frames);
            fiber["WaitUntil"] = world.Events.Frame + 2;
        }
        world.Events.Restore(JsonSerializer.SerializeToElement(state));
        world.Events.Step();
        NativeBattleCompletion request = Assert.IsType<NativeBattleCompletion>(world.Events.PendingInteractiveBattle);
        Assert.Equal((fieldId, objectId, scriptId, operation, (ushort)formation, (ushort)0), (request.FieldId, request.ObjectId, request.ScriptId, request.Operation, request.Encounter, request.Flags));
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(new ScriptedBattleOwner(fieldId, objectId, scriptId, operation, 0, request.ProgramHash), occurrence.ScriptedOwner);
        Assert.Equal(world.BattleResources.FormationSourceHash(formation), occurrence.FormationHash);
        Assert.Equal(party, session.State.CharacterVitals!.GetBattlePartyRows(11));

        GameState pending = session.PrepareSnapshot() with { EventState = world.Events.Capture() };
        byte[] extras = [2];
        NativeCharacterVitals selected = pending.CharacterVitals! with { AdditionalCombatParty = extras };
        Assert.All(extras, row => Assert.True(selected.IsRosterMember(row)));
        Assert.Equal(party.Concat(extras), selected.GetBattlePartyRows(11));
        JsonObject expandedEvents = JsonNode.Parse(pending.EventState!.Value.GetRawText())!.AsObject();
        expandedEvents["CharacterVitals"]![nameof(NativeCharacterVitals.AdditionalCombatParty)] = JsonSerializer.SerializeToNode(extras);
        GameSession expandedSession = new(pending with { CharacterVitals = selected, EventState = JsonSerializer.SerializeToElement(expandedEvents), GameplayPreferences = pending.GameplayPreferences with { BattleAllyCount = 4 } });
        using GameWorld expandedWorld = new(expandedSession, directory, sharedAssets: assets);
        expandedWorld.EnsureField();
        Assert.Equal(3, world.ConfiguredBattleAllyCount);
        Assert.Equal(4, expandedWorld.ConfiguredBattleAllyCount);
        Assert.Equal(request, expandedWorld.Events.PendingInteractiveBattle);
        BattleInput start = new(BattleInputKind.StartEncounter, NativeSeed: 0);
        world.ApplyBattleInput(start);
        expandedWorld.ApplyBattleInput(start);
        BattleSession original = Assert.IsType<BattleSession>(world.Battle);
        BattleSession expanded = Assert.IsType<BattleSession>(expandedWorld.Battle);
        AssertOriginalStoryState(original, original, party);
        BattleCombatant[] enemies = original.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        Assert.Equal(models, enemies.Select(actor => actor.ModelId));
        Assert.Equal(Enumerable.Range(3, models.Length), enemies.Select(actor => actor.RequireNativeActorIndex()));
        Assert.All(original.Capture().Definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive), actor => Assert.NotNull(actor.NativeAi));
        BattleSession originalRestored = RestoreAndAdvance(world, original);
        AssertOriginalStoryState(original, originalRestored, party);
        BattleDefinition definition = expanded.ReadDefinition();
        Assert.True(definition.UsesOriginalEnemySlots);
        Assert.Equal(party.Concat(extras).Select(row => $"party-{row}"), expanded.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id));
        BattleCombatantDefinition[] authored = definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive && !definition.AddedEnemySources!.ContainsKey(actor.SceneSlot!.Value)).ToArray();
        Assert.Equal(models, authored.Select(actor => actor.ModelId));
        Assert.Equal(enemies.Select(actor => actor.SceneSlot), authored.Select(actor => actor.SceneSlot));
        Assert.All(authored, actor => Assert.NotNull(actor.NativeTemplate));
        if (formation == 794)
        {
            Assert.All(definition.Combatants.Where(actor => actor.SceneSlot is int slot && definition.AddedEnemySources!.ContainsKey(slot)), donor =>
                Assert.True(Assert.IsType<BattleSequenceSet>(expandedWorld.BattleResources.LoadEnemy(donor.ModelId).Sequences).CanCloneIndependently()));
            Assert.False(Assert.IsType<BattleSequenceSet>(expandedWorld.BattleResources.LoadEnemy(106).Sequences).CanCloneIndependently());
            Assert.False(Assert.IsType<BattleSequenceSet>(expandedWorld.BattleResources.LoadEnemy(107).Sequences).CanCloneIndependently());
        }
        ExpandedBattleEntryAdmissionTests.ReachReady(expandedWorld, expanded);
        Assert.Equal(request.Frame, world.Events.Frame);
        Assert.Equal(request.Frame, expandedWorld.Events.Frame);
        if (formation == 796)
        {
            AssertAdelRinoaDeathUsesOriginalResult(world, session, original, request);
            AssertAdelRinoaDeathUsesOriginalResult(expandedWorld, expandedSession, expanded, request);
        }
    }

    private static void AssertAdelRinoaDeathUsesOriginalResult(GameWorld world, GameSession session, BattleSession battle, NativeBattleCompletion request)
    {
        BattleEncounterOccurrence encounter = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        NativeBattleDefeatReturnBranch branch = world.Events.ReadNativeDefeatReturnBranch4706B0();
        Assert.Equal(branch.PersistentFlags182, battle.ReadDefinition().NativeFieldBattleMode);
        int appliedResults = session.State.AppliedBattleResultIds.Length;
        int abandonments = session.State.AbandonedNativeBattles?.Length ?? 0;
        uint victories = session.State.BattleStatistics!.Victories;
        BattleCombatant partyRinoa = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 5);
        BattleCombatant enemyRinoa = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 113);
        BattleCombatant adel = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 112);
        BattlePartyModelOwner partyOwner = Assert.IsAssignableFrom<BattlePartyModelOwner>(battle.ReadPresentationModelOwner(partyRinoa.Id));
        BattleEnemyModelOwner enemyOwner = Assert.IsAssignableFrom<BattleEnemyModelOwner>(battle.ReadPresentationModelOwner(enemyRinoa.Id));
        Assert.NotEqual(partyRinoa.Id, enemyRinoa.Id);
        var (_, aliases) = battle.ReadBattlePresentationPacketBindings();
        Assert.Equal(partyOwner, aliases.DecodeLiteral(0));
        Assert.Equal(enemyOwner, aliases.DecodeLiteral(4));
        Assert.Equal(1, enemyRinoa.SceneSlot);
        NativeBattleAiDefinition rinoaAi = Assert.IsType<NativeBattleAiDefinition>(battle.ReadDefinition().Combatants.Single(actor => actor.Id == enemyRinoa.Id).NativeAi);
        Assert.Equal(new byte[] { 24, 1, 57, 0 }, rinoaAi.Scripts[3].Take(4));

        ExpandedBattlePresentationProcess? process = battle.ExpandedPresentationProcess;
        bool EntryReady() => process?.EntryReady ?? battle.NativeGraphics!.EntryReady;
        for (int callback = 0; callback < 1200 && !EntryReady(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(EntryReady());
        BattleCombatant squall = Assert.Single(battle.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 0);
        int eventStart = battle.Events.Count;
        int issuedAttacks = 0;
        bool comparedPrivateCallback = false;
        for (int callback = 0; callback < 4800 && world.Battle is not null && (battle.Outcome == BattleOutcome.Active || battle.NativeGraphics is { } terminal && terminal.NativeOutcome?.FadeDurationB0C != 0); callback++)
        {
            if (battle.Outcome == BattleOutcome.Active && !battle.NativeScriptState.ScriptedExitRequested && enemyRinoa is { IsAlive: true, CurrentHp: > 0 } && !squall.IsBusy && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                squall.Atb = BattleSession.AtbMaximum;
                world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: squall.Id, TargetId: enemyRinoa.Id) { ForcePhysical = true });
                issuedAttacks++;
            }
            if (!comparedPrivateCallback && battle is { Outcome: BattleOutcome.Active, NativeGraphics.NativeOutcome.ResultByte6E7: 1 })
            {
                BattleSession copy = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(battle.Capture()));
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                copy.Tick();
                Assert.Equal(copy.NativeGraphics!.NativeOutcome, battle.NativeGraphics!.NativeOutcome);
                Assert.Equal(copy.NativeGraphics.Descriptors.Queue.ToArray(), battle.NativeGraphics.Descriptors.Queue.ToArray());
                Assert.Equal(copy.NativeHostCallbacks, battle.NativeHostCallbacks);
                Assert.Equal(copy.Capture().Random, battle.Capture().Random);
                comparedPrivateCallback = true;
                continue;
            }
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            Assert.Same(process, battle.ExpandedPresentationProcess);
        }

        Assert.True(enemyRinoa.CurrentHp == 0, $"Expanded={battle.UsesExpandedRoster}, frame={battle.Frame}, callbacks={battle.NativeHostCallbacks}, outcome={battle.Outcome}, Squall HP={squall.CurrentHp}, busy={squall.IsBusy}, Rinoa HP={enemyRinoa.CurrentHp}/{enemyRinoa.MaximumHp}, attacks={issuedAttacks}, pending={battle.QueuedActions.Count}/{battle.ActiveAction?.ActionId}, events={string.Join(", ", battle.Events.Skip(eventStart).TakeLast(8).Select(item => $"{item.Kind}:{item.ActorId}>{item.TargetId}({item.Amount})"))}.");
        Assert.Single(battle.Events.Skip(eventStart), item => item.Kind == "Defeated" && item.TargetId == enemyRinoa.Id);
        Assert.True(partyRinoa.IsAlive);
        Assert.True(adel.IsAlive);
        Assert.True(battle.NativeScriptState.ScriptedExitRequested);
        Assert.Contains(battle.Events.Skip(eventStart), item => item.ActorId == enemyRinoa.Id && item.NativeTextIdentity == BattleTextIdentity.Enemy(113, 1));
        Assert.Equal(BattleOutcome.ScriptedExit, battle.Outcome);
        Assert.Equal((byte)1, battle.NativeResult);
        NativeBattleOutcomeState terminalOutcome = Assert.IsType<NativeBattleOutcomeState>((process?.Graphics ?? battle.NativeGraphics)!.NativeOutcome);
        Assert.Equal((byte)1, terminalOutcome.ResultByte6E7);
        Assert.Equal((byte)3, terminalOutcome.FadeStyleE01);
        Assert.Equal((byte)0, terminalOutcome.FadeDurationB0C);
        Assert.Equal(process is null, comparedPrivateCallback);
        Assert.Equal(branch.ResumePreviousScene, battle.CanReturnToField);
        if (branch.ResumePreviousScene)
        {
            world.ApplyBattleInput(new(BattleInputKind.Return));
            NativeBattleCompletion returned = Assert.Single(world.Events.Scene.NativeBattleCompletions!, entry => entry.Frame == request.Frame && entry.Operation == request.Operation);
            Assert.Equal((byte)1, returned.Result);
            Assert.NotNull(returned.ReturnFrame);
            Assert.Null(returned.Abandonment);
            Assert.Equal(abandonments, session.State.AbandonedNativeBattles?.Length ?? 0);
        }
        else
        {
            Assert.True(world.Battle is null, $"Scripted result1 with saved mode 0x{branch.PersistentFlags182:X4} must settle through native field75 abandonment (expanded={battle.UsesExpandedRoster}, original fade={battle.NativeGraphics?.NativeOutcome?.FadeDurationB0C}).");
            Assert.Equal(75, session.State.FieldId);
            NativeBattleAbandonment abandoned = Assert.Single(session.State.AbandonedNativeBattles!, entry => entry.Encounter.OccurrenceId == encounter.OccurrenceId);
            Assert.Equal(abandonments + 1, session.State.AbandonedNativeBattles!.Length);
            Assert.Equal(branch, abandoned.Branch);
            Assert.Equal(encounter.ScriptedOwner, abandoned.Encounter.ScriptedOwner);
            Assert.Equal(request.Operation + 1, abandoned.ScriptWait?.NextOperation);
        }
        Assert.Null(world.Battle);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Equal(appliedResults + 1, session.State.AppliedBattleResultIds.Length);
        Assert.Single(session.State.AppliedBattleResultIds, id => id == encounter.OccurrenceId);
        Assert.Equal(victories, session.State.BattleStatistics!.Victories);
        BattleRewardSummary rewards = Assert.IsType<BattleRewardSummary>(world.LastBattleRewards);
        Assert.Empty(rewards.CharacterExperience);
        Assert.Empty(rewards.GuardianForceExperience);
        Assert.Empty(rewards.GuardianForceAbilityPoints);
        Assert.Empty(rewards.Items);
        Assert.Empty(rewards.Cards);
    }

    [Theory]
    [InlineData("story-battle-94", 130, 94)]
    [InlineData("story-battle-136", 768, 136)]
    [InlineData("story-battle-164", 776, 164)]
    public void StoryBattlePreservesPhysicalActorsAndCallbacksAtElevenAllies(string fixture, int field, int formation)
    {
        string path = TestResources.FixturePath(fixture);
        string directory = TestResources.GameDirectory;
        GameSession original = new(GameSession.Load(path));
        GameSession expanded = new(GameSession.Load(path));
        using GameWorld originalWorld = new(original, directory, expandedBattleAllyCount: 3) { UsePhysicalBattlePresentation = true };
        using GameWorld expandedWorld = new(expanded, directory, expandedBattleAllyCount: 11) { UsePhysicalBattlePresentation = true };
        originalWorld.EnsureField();
        expandedWorld.EnsureField();
        Assert.Equal(3, originalWorld.ConfiguredBattleAllyCount);
        Assert.Equal(11, expandedWorld.ConfiguredBattleAllyCount);
        NativeBattleCompletion request = Assert.IsType<NativeBattleCompletion>(originalWorld.Events.PendingInteractiveBattle);
        Assert.Equal(field, request.FieldId);
        Assert.Equal(formation, request.Encounter);
        Assert.Equal(request, expandedWorld.Events.PendingInteractiveBattle);
        Assert.Null(originalWorld.Battle);
        Assert.Null(expandedWorld.Battle);

        BattleInput start = new(BattleInputKind.StartEncounter, Mode: original.State.GameplayPreferences.AtbMode, NativeSeed: 0);
        originalWorld.ApplyBattleInput(start);
        expandedWorld.ApplyBattleInput(start);
        BattleSession originalBattle = Assert.IsType<BattleSession>(originalWorld.Battle);
        BattleSession expandedBattle = Assert.IsType<BattleSession>(expandedWorld.Battle);
        AssertOriginalStoryState(originalBattle, expandedBattle, original.State.CharacterVitals!.CombatParty);

        const int callbacks = 8;
        BattleInput tick = new(BattleInputKind.Tick, Frames: callbacks);
        Exception? originalCallback = Record.Exception(() => originalWorld.ApplyBattleInput(tick));
        Exception? expandedCallback = Record.Exception(() => expandedWorld.ApplyBattleInput(tick));
        Assert.Equal(originalCallback?.GetType(), expandedCallback?.GetType());
        Assert.Equal(originalCallback?.Message, expandedCallback?.Message);
        if (originalCallback is not null)
        {
            Assert.Equal(94, formation);
            Assert.IsType<NotSupportedException>(originalCallback);
            Assert.Contains("Native504060 completed blend requires its503300 captured pose", originalCallback.Message);
        }
        else
        {
            Assert.Equal(callbacks, originalBattle.NativeHostCallbacks);
        }
        Assert.Equal(request.Frame, originalWorld.Events.Frame);
        Assert.Equal(originalWorld.Events.Frame, expandedWorld.Events.Frame);
        AssertJsonEqual(original.ExamTimer, expanded.ExamTimer);
        AssertOriginalStoryState(originalBattle, expandedBattle, original.State.CharacterVitals.CombatParty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(11)]
    public void TowerStoryEntryPreservesOriginalSlotsWithAnEligibleFourthPartyMember(int expandedBattleAllyCount)
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.TowerEntry.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, expandedBattleAllyCount: expandedBattleAllyCount) { UsePhysicalBattlePresentation = true };
        world.EnsureField();
        for (int frame = 0; frame < 3000 && !world.HasPendingBattle; frame++)
        {
            PlayerAction input = world.CurrentDialogue?.CanConfirm == true && world.CurrentDialogue.FirstChoice is int first ? new(PlayerActionKind.Choose, first) : new(world.CurrentDialogue?.CanConfirm == true ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.True(world.Apply(input).Accepted);
        }

        Assert.True(world.HasPendingBattle);
        NativeCharacterVitals vitals = Assert.IsType<NativeCharacterVitals>(session.State.CharacterVitals);
        Assert.True(vitals.IsRosterMember(3));
        Assert.Equal(new byte[] { 1, 0, 5 }, vitals.GetBattlePartyRows(11));
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(29, occurrence.Formation);
        ScriptedBattleOwner owner = Assert.IsType<ScriptedBattleOwner>(occurrence.ScriptedOwner);
        Assert.Equal((314, 8, 98, 1375, 1), (owner.FieldId, owner.ObjectId, owner.ScriptId, owner.Operation, owner.Flags));

        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.False(battle.UsesExpandedRoster);
        Assert.NotNull(battle.NativeGraphics);
        Assert.Equal(new byte[] { 1, 0, 5 }, session.State.CharacterVitals!.CombatParty);
        AssertTowerSlots(battle);

        BattleSession restored = RestoreAndAdvance(world, battle);
        AssertTowerSlots(restored);
    }

    [Fact]
    public void Restored_native_reward_preview_recovers_missing_record_and_rejects_conflicting_binding()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("story-battle-136")));
        using GameWorld original = new(session, directory, expandedBattleAllyCount: 3) { UsePhysicalBattlePresentation = true };
        original.EnsureField();
        Assert.Equal((ushort)136, original.Events.PendingInteractiveBattle!.Encounter);
        original.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(original.Battle);
        Assert.False(battle.UsesExpandedRoster);
        Assert.NotNull(battle.NativeGraphics);
        original.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);

        BattleRewardSummary expected = original.PreviewBattleRewards();
        GameState completed = session.PrepareSnapshot();
        JsonObject saved = JsonSerializer.SerializeToNode(completed)!.AsObject();
        JsonObject[] players = saved["Battle"]!["Definition"]!["Combatants"]!.AsArray().Where(actor => actor!["IsPlayer"]!.GetValue<bool>()).Select(actor => actor!.AsObject()).ToArray();
        Assert.Equal(3, players.Length);
        string actorId = players[0][nameof(BattleCombatantDefinition.Id)]!.GetValue<string>();
        int originalRow = players[0][nameof(BattleCombatantDefinition.MagicRecordId)]!.GetValue<int>();
        Assert.True(players[0].Remove(nameof(BattleCombatantDefinition.MagicRecordId)));
        using (GameWorld legacy = new(new GameSession(saved.Deserialize<GameState>()!), directory, expandedBattleAllyCount: 3))
        {
            BattleSession restored = Assert.IsType<BattleSession>(legacy.Battle);
            Assert.NotNull(restored.NativeGraphics);
            Assert.Null(Assert.Single(restored.Combatants, actor => actor.Id == actorId).MagicRecordId);
            AssertJsonEqual(expected, legacy.PreviewBattleRewards());
        }

        int conflictingRow = Enumerable.Range(0, completed.CharacterVitals!.Records.Length).First(row => !completed.CharacterVitals.CombatParty.Contains((byte)row));
        Assert.NotEqual(originalRow, conflictingRow);
        players[0][nameof(BattleCombatantDefinition.MagicRecordId)] = conflictingRow;
        Assert.Throws<InvalidDataException>(() =>
        {
            using GameWorld conflicting = new(new GameSession(saved.Deserialize<GameState>()!), directory, expandedBattleAllyCount: 3);
            conflicting.PreviewBattleRewards();
        });
    }

    [Fact]
    public void MagicalLampEntryKeepsNativeSlots()
    {
        string path = TestResources.FixturePath("story-menu-battle-field-entry");
        string directory = TestResources.GameDirectory;
        GameSession original = new(GameSession.Load(path));
        GameSession expanded = new(GameSession.Load(path));
        using GameWorld originalWorld = new(original, directory) { UsePhysicalBattlePresentation = true };
        using GameWorld expandedWorld = new(expanded, directory) { UsePhysicalBattlePresentation = true };
        BattleSession originalBattle = EnterMagicalLampBattle(originalWorld, original, 3);
        BattleSession expandedBattle = EnterMagicalLampBattle(expandedWorld, expanded, 11);
        Assert.Equal(originalWorld.Events.PendingMenuBattle, expandedWorld.Events.PendingMenuBattle);
        AssertOriginalStoryState(originalBattle, expandedBattle, original.State.CharacterVitals!.CombatParty);
        AssertJsonEqual(originalBattle.NativeCamera, expandedBattle.NativeCamera);

        const int callbacks = 120;
        BattleInput tick = new(BattleInputKind.Tick, Frames: callbacks);
        originalWorld.ApplyBattleInput(tick);
        expandedWorld.ApplyBattleInput(tick);
        Assert.Equal(callbacks, originalBattle.NativeHostCallbacks);
        Assert.Equal(1, originalWorld.Events.Frame);
        Assert.Equal(originalWorld.Events.Frame, expandedWorld.Events.Frame);
        Assert.Equal(originalWorld.Events.PendingMenuBattle, expandedWorld.Events.PendingMenuBattle);
        AssertOriginalStoryState(originalBattle, expandedBattle, original.State.CharacterVitals.CombatParty);
        AssertJsonEqual(originalBattle.NativeCamera, expandedBattle.NativeCamera);

        BattleSession originalRestored = RestoreAndAdvance(originalWorld, originalBattle);
        BattleSession expandedRestored = RestoreAndAdvance(expandedWorld, expandedBattle);
        Assert.Equal(callbacks + 1, originalRestored.NativeHostCallbacks);
        AssertOriginalStoryState(originalRestored, expandedRestored, original.State.CharacterVitals.CombatParty);
        AssertJsonEqual(originalRestored.NativeCamera, expandedRestored.NativeCamera);
    }

    private static BattleSession EnterMagicalLampBattle(GameWorld world, GameSession session, int battleAllyCount)
    {
        world.EnsureField();
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = battleAllyCount });
        Assert.Equal(battleAllyCount, world.ConfiguredBattleAllyCount);
        byte[] combatParty = session.State.CharacterVitals!.CombatParty.ToArray();

        Assert.True(world.Apply(new(PlayerActionKind.UseFieldItem, 168, Quantity: 0)).Accepted);
        Assert.Equal((ushort)811, world.Events.PendingMenuBattle!.Formation);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal(811, battle.EncounterId);
        Assert.False(battle.UsesExpandedRoster);
        Assert.NotNull(battle.NativeGraphics);
        Assert.Equal(combatParty, session.State.CharacterVitals!.CombatParty);
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer).ToArray();
        Assert.Equal(combatParty.Select(row => $"party-{row}"), allies.Select(actor => actor.Id));
        Assert.Equal(new[] { 0, 1, 2 }, allies.Select(actor => actor.RequireNativeActorIndex()));
        Assert.Equal(3, Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive).RequireNativeActorIndex());
        return battle;
    }

    private static void AssertOriginalStoryState(BattleSession original, BattleSession candidate, IEnumerable<byte> combatParty)
    {
        Assert.False(original.UsesExpandedRoster);
        Assert.False(candidate.UsesExpandedRoster);
        Assert.NotNull(original.NativeGraphics);
        Assert.NotNull(candidate.NativeGraphics);
        Assert.Equal(original.EncounterId, candidate.EncounterId);
        BattleCombatant[] allies = candidate.Combatants.Where(actor => actor.IsPlayer).ToArray();
        Assert.Equal(combatParty.Where(row => row != byte.MaxValue).Select(row => $"party-{row}"), allies.Select(actor => actor.Id));
        Assert.All(allies, actor => Assert.InRange(actor.RequireNativeActorIndex(), 0, 2));
        Assert.All(candidate.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive), actor => Assert.InRange(actor.RequireNativeActorIndex(), 3, 6));
        BattleSnapshot expected = original.Capture();
        BattleSnapshot actual = candidate.Capture();
        Assert.Equal(expected.NativeHostCallbacks, actual.NativeHostCallbacks);
        Assert.Equal(expected.Frame, actual.Frame);
        AssertJsonEqual(expected.Random, actual.Random);
        AssertJsonEqual(expected.NativeExamTimer, actual.NativeExamTimer);
        AssertJsonEqual(expected.Combatants, actual.Combatants);
        AssertJsonEqual(expected.NativeAllocations, actual.NativeAllocations);
    }

    private static void AssertJsonEqual<T>(T expected, T actual)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)));
    }

    private static BattleSession RestoreAndAdvance(GameWorld world, BattleSession battle)
    {
        BattleSession restored = Restore(battle);
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.Tick();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(restored.Capture())));
        return restored;
    }

    private static BattleSession Restore(BattleSession battle)
    {
        BattleSnapshot snapshot = NativeBattleStorageSnapshots.Capture(battle.Capture());
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(snapshot), JsonSerializer.SerializeToElement(restored.Capture())));
        return restored;
    }

    private static void AssertTowerSlots(BattleSession battle)
    {
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(29, battle.EncounterId);
        Assert.Equal(new[] { ("party-1", 0), ("party-0", 1), ("party-5", 2) }, snapshot.Combatants.Where(actor => actor.Id.StartsWith("party-", StringComparison.Ordinal)).Select(actor => (actor.Id, actor.NativeParticipant!.ActorIndex)));
        Assert.Equal(3, snapshot.Combatants.Single(actor => actor.Id == "enemy-0").NativeParticipant!.ActorIndex);
        NativeBattleParticipantSnapshot wedge = snapshot.Combatants.Single(actor => actor.Id == "enemy-1").NativeParticipant!;
        Assert.Equal(4, wedge.ActorIndex);
        Assert.True(wedge.Active);
        Assert.True(wedge.Hidden);
        Assert.True(wedge.Suspended);
        NativeBattleParticipantSnapshot elvoret = snapshot.Combatants.Single(actor => actor.Id == "enemy-2").NativeParticipant!;
        Assert.Equal(-1, elvoret.ActorIndex);
        Assert.False(elvoret.Active);
    }
}
