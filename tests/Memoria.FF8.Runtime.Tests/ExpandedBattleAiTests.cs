
namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleAiTests
{
    [Theory]
    [InlineData(4, 6, 201, 2, "ally-3")]
    [InlineData(4, 6, 202, 5, "enemy-5")]
    [InlineData(4, 6, 207, 6, "enemy-5")]
    [InlineData(11, 8, 201, 5, "ally-10")]
    [InlineData(11, 8, 202, 59, "enemy-7")]
    [InlineData(11, 8, 207, 7, "enemy-7")]
    public void RandomSelectorsReachTheLastRosterSlots(int allyCount, int enemyCount, byte selector, byte seed, string target)
    {
        BattleSession battle = Start([4, selector, 7, 1, 6, 0], allyCount, enemyCount, seed);
        Assert.Equal(new[] { target }, ExplicitActors(battle.ActiveAction!.TargetRequest));
    }

    [Fact]
    public void Selector208RetainsTheAllySideForRandomContactsAfterRestore()
    {
        BattleNativeAction native = Physical with { AttackType = 27, Power = 1, Hit = 99, HitCount = 10 };
        BattleSession battle = Start([4, 208, 7, 1, 6, 0], 11, 11, native: native);
        BattleSnapshot before = battle.Capture();
        BattleTargetSelection.RandomOnSide selection = Assert.IsType<BattleTargetSelection.RandomOnSide>(battle.ActiveAction!.TargetRequest.Selection);
        Assert.Equal(BattleSide.Allies, selection.Side);
        string[] targets = battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id).ToArray();
        Assert.Equal(11, targets.Length);
        BattleSession restored = DetachedExpandedAiFixture.Restore(before);
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        string?[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4").Select(entry => entry.TargetId).ToArray();
        Assert.Equal(10, contacts.Length);
        Assert.All(contacts, target => Assert.Contains(target, targets));
        Assert.True(contacts.Distinct().Count() > 1);
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }

    [Theory]
    [InlineData(204, 4, 6)]
    [InlineData(205, 4, 6)]
    [InlineData(206, 4, 6)]
    [InlineData(204, 11, 8)]
    [InlineData(205, 11, 8)]
    [InlineData(206, 11, 8)]
    public void GroupTargetingAndPendingTurnSurviveSnapshot(byte selector, int allyCount, int enemyCount)
    {
        BattleSession battle = Start([4, selector, 7, 1, 6, 0], allyCount, enemyCount);
        BattleSnapshot snapshot = battle.Capture();
        string[] targets = battle.Combatants.Where(actor => selector == 206 || actor.IsPlayer == (selector == 204)).Select(actor => actor.Id).ToArray();
        BattleTargetSelection.SideGroup selection = Assert.IsType<BattleTargetSelection.SideGroup>(battle.ActiveAction!.TargetRequest.Selection);
        Assert.Equal(selector == 204 ? BattleTargetSide.Allies : selector == 205 ? BattleTargetSide.Enemies : BattleTargetSide.Both, selection.Side);
        BattleSession restored = DetachedExpandedAiFixture.Restore(snapshot);
        for (int i = 0; i < BattleSession.AttackWindupFrames + 1; i++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(targets, battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4").Select(entry => entry.TargetId));
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }

    [Theory]
    [InlineData(4, 6)]
    [InlineData(11, 8)]
    public void HighestHpAndLivingCountIncludeLastAlly(int allyCount, int enemyCount)
    {
        // Continue only if the expected number of allies is alive. Then choose the highest HP.
        BattleSession battle = Start([2, 6, 200, 0, (byte)allyCount, 0, 9, 0, 38, 0, 200, 0, 203, 7, 1, 6, 0, 0], allyCount, enemyCount);
        Assert.Equal(new[] { $"ally-{allyCount - 1}" }, ExplicitActors(battle.ActiveAction!.TargetRequest));
    }

    [Fact]
    public void ModelSelectorReadsAndTargetsTheEleventhAlly()
    {
        // Subject 0 reads model 10's HP. Opcode 4 targets the same model by identity.
        byte[] turn = [2, 0, 10, 0, 110, 0, 6, 0, 4, 10, 7, 1, 6, 0, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [Physical], 0));
        BattleSession battle = Start(turn, 11, 8);
        Assert.Equal(new[] { "ally-10" }, ExplicitActors(battle.ActiveAction!.TargetRequest));
    }

    [Fact]
    public void DirectOperandsAndAllocationAreRejectedUnlessUnreachableInTheFormation()
    {
        NativeBattleAiDefinition program = Ai([2, 3, 0, 0, 62, 0, 3, 0, 43, 4, 0, 4, 201, 7, 1, 6, 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(program, [Physical], 62));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [Physical], 62, originalEnemySlots: true));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [Physical], 344));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([43, 6, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([2, 15, 0, 0, 6, 0, 0, 0, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([31, 0, 0]), [Physical], 236));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([4, 220, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([14, 96, 0, 0]), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai([27, 1, 0, 0]), [Physical], 0, originalEnemySlots: true));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([27, 4, 0, 0]), [Physical], 0, originalEnemySlots: true));
    }

    [Fact]
    public void StaticModelReferencesAreReportedIndependentlyOfExecutionAdmission()
    {
        List<string> references = [];
        byte[] turn = [2, 3, 0, 0, 62, 0, 3, 0, 4, 32, 0, 4, 201, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [Physical], 62, explicitModelReferences: references));
        Assert.Equal("target-model:32", Assert.Single(references));
        references.Clear();
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [Physical], 344, explicitModelReferences: references));
        Assert.Empty(references);
    }

    [Fact]
    public void Donor_action_capability_does_not_restrict_original_slot_AI()
    {
        NativeBattleAiDefinition program = Ai([4, 201, 12, 1, 0]);
        NativeBattleEffectBinding effect = new(17, 0x1234, 0x1238) { RequiresOriginalCasterCompanion = true };
        BattleNativeAction action = Physical with { MagicId = effect.Effect };

        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(program, [action], 0, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [action], 0, originalEnemySlots: true, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [Physical], 0, admitsDonorEffect: CanBind));

        NativeBattleAiDefinition drawn = Ai([42, 0]) with { Abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray() };
        BattleNativeAction spell = action with { Command = 2, ActionId = 17, KernelSection = 1, TargetFlags = 0x48 };
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(drawn, [spell], 0, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(drawn, [spell], 0, originalEnemySlots: true, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(drawn, [spell with { MagicId = 0 }], 0, admitsDonorEffect: CanBind));

        NativeBattleAiDefinition direct = Ai([30, checked((byte)effect.Effect), 0]);
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(direct, [Physical], 0, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(direct, [Physical], 0, originalEnemySlots: true, admitsDonorEffect: CanBind));
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai([30, 18, 0]), [Physical], 0, admitsDonorEffect: CanBind));

        bool CanBind(ushort candidate) => candidate != effect.Effect || !effect.RequiresOriginalCasterCompanion;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DirectGraphicsPreservesTheByteOperandAndCommitsOnlyReachedZeroContacts(bool selectsTargets)
    {
        byte[] turn = selectsTargets ? [4, 204, 5, 17, 30, 248, 0] : [5, 17, 30, 248, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(Ai([30]), [Physical], 0));
        BattleSession battle = Start(turn, 11, 8);
        BattleActionPresentation windup = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal((byte)252, windup.NativeQueuedCommand);
        Assert.Equal(1, windup.ActionId);
        Assert.Equal((byte)17, windup.NativeSequence);
        Assert.Equal(new BattleNativeGraphicCommand(248), windup.NativeGraphic);
        Assert.Null(windup.NativeAction);
        if (selectsTargets)
        {
            Assert.Equal(BattleTargetSide.Allies, Assert.IsType<BattleTargetSelection.SideGroup>(windup.TargetRequest.Selection).Side);
        }
        else
        {
            Assert.IsType<BattleTargetSelection.None>(windup.TargetRequest.Selection);
        }
        string[] targets = selectsTargets ? battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id).ToArray() : [];
        Assert.Equal(selectsTargets ? 11 : 0, targets.Length);
        BattleSnapshot saved = battle.Capture();
        BattleSession restored = DetachedExpandedAiFixture.Restore(saved);
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4").ToArray();
        Assert.Equal(targets, contacts.Select(contact => contact.TargetId));
        Assert.All(contacts, contact => Assert.Equal(0, contact.Amount));
        Assert.All(battle.Combatants.Where(actor => actor.IsPlayer), ally =>
        {
            Assert.Equal(ally.MaximumHp, ally.CurrentHp);
            if (selectsTargets)
            {
                Assert.Equal("enemy-4", ally.LastAttackerId);
                Assert.Equal((byte)252, ally.LastNativeAction!.Command);
                Assert.Equal((byte)1, ally.LastNativeAction.ActionId);
                Assert.Equal((byte)3, ally.LastNativeAction.AttackType);
            }
        });
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == "enemy-4");
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnemyAttackBit80ContactsTheSelectedKnockedOutAlly(bool ejection)
    {
        BattleNativeAction action = Physical with { Power = 0, AttackFlags = 0x83, StatusAttack = ejection ? (byte)255 : (byte)0, Statuses1 = ejection ? 0x10000u : 0u };
        byte[] turn = [4, 3, 7, 1, 6, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(turn), [action], 0));
        BattleSession battle = Start(turn, 4, 6, native: action);
        Assert.Equal("ally-3", battle.ActiveAction!.TargetId);
        BattleCombatant target = Assert.Single(battle.Combatants, actor => actor.Id == "ally-3");
        target.NativeDeathCount = 0;
        target.CurrentHp = 0;
        target.NativeStatus0 |= 1;
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
        }
        Assert.Equal(ejection, target.IsEjected);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4" && entry.TargetId == target.Id);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
    }

    [Fact]
    public void EnemyStatisticOpcodeUsesOriginalSignedOperandAfterRestore()
    {
        BattleSession battle = Start([40, 0, 20, 0], 11, 11);
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-4");
        Assert.Equal(216, enemy.Attack);
        BattleSession restored = DetachedExpandedAiFixture.Restore(battle.Capture());
        Assert.Equal(216, Assert.Single(restored.Combatants, actor => actor.Id == "enemy-4").Attack);
    }

    [Fact]
    public void SelfRemovalKeepsTheLogicalRosterPositionAcrossRestore()
    {
        byte[] script = [29, 200, 0];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai(script), [Physical], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresOriginalSlots, NativeBattleExpandedAi.Evaluate(Ai([29, 0, 0]), [Physical], 0));
        BattleSession battle = Start(script, 11, 11);
        BattleCombatant removed = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-4");
        Assert.False(removed.IsActive);
        Assert.True(removed.Untargetable);
        Assert.Empty(battle.EnemyDefeats);
        BattleSession restored = DetachedExpandedAiFixture.Restore(battle.Capture());
        BattleCombatant restoredActor = Assert.Single(restored.Combatants, actor => actor.Id == removed.Id);
        Assert.Equal(removed.Slot, restoredActor.Slot);
        Assert.False(restoredActor.IsActive);
        Assert.All(restored.Combatants.Where(actor => actor.Id != removed.Id), actor => Assert.True(actor.IsActive));
    }

    [Theory]
    [InlineData(1, 11)]
    [InlineData(2, 22)]
    public void InstalledActionShapeRepeatsTheWholeAllySideInOrder(byte hitCount, int contactCount)
    {
        BattleNativeAction native = Physical with { AttackType = 27, Power = 1, Hit = 99, HitCount = hitCount };
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(Ai([4, 204, 7, 1, 6, 0]), [native], 0));
        BattleSession battle = Start([4, 204, 7, 1, 6, 0], 11, 11, native: native);
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
        }
        string?[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-4").Select(entry => entry.TargetId).ToArray();
        Assert.Equal(contactCount, contacts.Length);
        Assert.Equal(Enumerable.Range(0, hitCount).SelectMany(_ => Enumerable.Range(0, 11).Select(slot => $"ally-{slot}")), contacts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HitCallbackQueuesOneCounterAgainstTheEleventhAllyAndSurvivesRestore(bool retainAttacker)
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 11).Select(i => new BattleCombatantDefinition
        {
            Id = $"ally-{i}", Name = $"Ally {i}", IsPlayer = true, ModelId = i, CurrentHp = 1000, MaximumHp = 1000, Attack = 30, Hit = 255, Speed = 1,
            Abilities = [BattleAbility.Attack], InitialAtb = i == 10 ? BattleSession.AtbMaximum : 0,
            NativeAttack = Physical with { Command = 1, ActionId = 0 }
        }).ToArray();
        byte[] reaction = retainAttacker
            ? [14, 220, 203, 2, 220, 200, 3, 240, 0, 5, 0, 4, 220, 7, 1, 6, 0]
            : [2, 10, 203, 0, 10, 0, 5, 0, 4, 203, 7, 1, 6, 0];
        NativeBattleAiDefinition counter = new([[14, 220, 240, 0], [0], reaction, [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(counter, [Physical], 0));
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(i => new BattleCombatantDefinition
        {
            Id = $"enemy-{i}", Name = $"Enemy {i}", ModelId = 28, Level = 10, CurrentHp = 1000, MaximumHp = 1000, Speed = 1, Attack = 30,
            NativeAi = i == 10 ? counter : null
        }).ToArray();
        BattleSession battle = new(new() { Combatants = [.. allies, .. enemies], NativeActions = [Physical], NativeSeed = 0 }, BattleAtbMode.Normal);
        DetachedExpandedAiFixture.DispatchCallback(battle, "enemy-10", 0);
        battle.QueueAttack("ally-10", "enemy-10");
        DetachedExpandedAiFixture.AdvanceTo(battle, () => battle.QueuedActions.Any(command => command.ExpandedAiCallback is { Stage: 2 }), "The native hit must publish its enemy callback.");
        BattleExpandedAiCallbackRequest callback = Assert.IsType<BattleExpandedAiCallbackRequest>(Assert.Single(battle.QueuedActions).ExpandedAiCallback);
        Assert.Equal(new BattleEnemyActorReference("enemy-10"), callback.Actor);
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = DetachedExpandedAiFixture.Restore(snapshot);
        foreach (BattleSession owner in new[] { battle, restored })
        {
            DetachedExpandedAiFixture.AdvanceTo(owner, () => owner.ActiveAction is null, "The original hit must finish before its queued callback runs.");
            DetachedExpandedAiFixture.DispatchCallback(owner, "enemy-10", 2);
            BattleAttackCommand queued = Assert.Single(owner.QueuedActions);
            Assert.Equal((byte)2, queued.NativeAiCallbackStage);
            Assert.Equal("ally-10", Assert.Single(ExplicitActors(queued.TargetRequest)));
        }
        NativeBattleParticipantSnapshot saved = Assert.IsType<NativeBattleParticipantSnapshot>(Assert.Single(snapshot.Combatants, actor => actor.Id == "enemy-10").NativeParticipant);
        if (retainAttacker)
        {
            BattleCombatant reopened = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-10");
            Assert.Equal("ally-10", Assert.Single(reopened.AiActorReferences).Value);
            Assert.Equal(0u, reopened.AiVariables[0]);
        }
        else
        {
            Assert.Null(saved.ActorReferences);
        }
        DetachedExpandedAiFixture.AdvanceTo(battle, CounterFinished, "The exact enemy hit callback must complete its counter.");
        DetachedExpandedAiFixture.AdvanceTo(restored, () => restored.Events.Any(entry => entry is { Kind: "ActionComplete", ActorId: "enemy-10" }), "The copied hit callback must complete its counter.");
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy-10" && entry.TargetId == "ally-10");
        DetachedExpandedAiFixture.AssertSameContinuation(battle, restored);

        bool CounterFinished() => battle.Events.Any(entry => entry is { Kind: "ActionComplete", ActorId: "enemy-10" });
    }

    private static BattleSession Start(byte[] turn, int allyCount, int enemyCount, byte seed = 0, BattleNativeAction? native = null)
    {
        BattleCombatantDefinition[] allies = Enumerable.Range(0, allyCount).Select(i => new BattleCombatantDefinition { Id = $"ally-{i}", Name = $"Ally {i}", IsPlayer = true, ModelId = i, CurrentHp = 100 + i, MaximumHp = 100 + i, Speed = 1 }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, enemyCount).Select(i => new BattleCombatantDefinition
        {
            Id = $"enemy-{i}", Name = $"Enemy {i}", ModelId = 28, Level = 10, CurrentHp = 100, MaximumHp = 100, Speed = 1, Attack = 30,
            InitialAtb = i == 4 ? BattleSession.AtbMaximum : 0, NativeAi = i == 4 ? Ai(turn) : null,
            ExpandedEnemyBaseStatistics = turn[0] == 40 ? [-20, 0, 0, 0, 1, 0] : null
        }).ToArray();
        BattleSession battle = new(new() { Combatants = [.. allies, .. enemies], NativeActions = [native ?? Physical], NativeSeed = seed }, BattleAtbMode.Normal);
        DetachedExpandedAiFixture.DispatchCallback(battle, "enemy-4", 0);
        BattleCombatant callbackOwner = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-4");
        DetachedExpandedAiFixture.AdvanceTo(battle, () => callbackOwner.AiTurnCount == 1, "The ready enemy must interpret its typed turn callback.");
        if (battle.QueuedActions.FirstOrDefault() is { ActorId: "enemy-4", NativeAiCallbackStage: 1 })
        {
            DetachedExpandedAiFixture.AdvanceTo(battle, () => battle.ActiveAction is { ActorId: "enemy-4", Phase: BattleActionPhase.Windup }, "The turn's produced command must reach its windup.");
        }
        return battle;
    }

    private static NativeBattleAiDefinition Ai(byte[] turn)
    {
        return new([[0], turn, [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
    }

    private static BattleNativeAction Physical => new(8, 1, 3, 0)
    {
        Name = "Physical", MagicId = 0, AttackType = 1, Power = 10, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = null,
        StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
    };
    private static IEnumerable<string> ExplicitActors(BattleTargetRequest request)
    {
        return Assert.IsType<BattleTargetSelection.Explicit>(request.Selection).Actors.Select(actor => actor.ActorId);
    }

}
