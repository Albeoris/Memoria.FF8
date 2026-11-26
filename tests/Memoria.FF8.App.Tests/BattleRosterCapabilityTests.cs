using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleRosterCapabilityTests
{
    [Theory]
    [InlineData(3, 1, true, false)]
    [InlineData(4, 1, true, false)]
    [InlineData(3, 2, true, false)]
    [InlineData(3, 1, false, true)]
    [InlineData(4, 1, false, true)]
    [InlineData(3, 2, false, true)]
    [InlineData(3, 1, true, false, true)]
    [InlineData(4, 1, true, false, true)]
    [InlineData(3, 2, true, false, true)]
    public void GameWorldAttackHonorsThePreviewAndExplicitPhysicalInput(int allies, int enemies, bool confirm, bool forcePhysical, bool draw = false)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        vitals = vitals.ChangeMagicStock(0, 1, 10, MagicOperationReason.ScriptReward, out _);
        vitals.MagicSystem.AttackModes[0] = MagicAttackMode.SmartMagic;
        BattleNativeAction spell = new(2, 1, 1, 1)
        {
            Name = "Fire", MagicId = 1, AttackType = 2, Power = 80, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0,
            Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleNativeAction attack = spell with { Command = 1, ActionId = 0, AttackType = 1, Power = 1, Hit = 255, Critical = 0, Name = "Attack" };
        BattleCombatantDefinition actor = new()
        {
            Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 1, MagicRecordId = 0, InitialAtb = BattleSession.AtbMaximum,
            Attack = 1, Magic = 100, Abilities = [BattleAbility.Attack, draw ? BattleAbility.Draw : BattleAbility.Magic], NativeAttack = attack,
            NativeGrowth = new(0, new byte[6].ToImmutableArray()), NativeCommandSlots = [1, draw ? (byte)6 : (byte)2, 0, 0]
        };
        BattleCombatantDefinition target = new() { Id = "target", Name = "Target", ModelId = 5, MaximumHp = 9999, CurrentHp = 9999 };
        BattleCombatantDefinition[] extraAllies = Enumerable.Range(0, allies - 1).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", ModelId = index + 2, IsPlayer = true }).ToArray();
        BattleCombatantDefinition[] extraEnemies = Enumerable.Range(0, enemies - 1).Select(index => target with { Id = $"extra-{index}" }).ToArray();
        BattleCombatantDefinition[] drawSources = draw ? [target with { Id = "draw-source", NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }] : [];
        NativeKnownMagic known = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        BattleSession battle = new(new()
        {
            MagicVitals = vitals,
            NativeKnownMagic = known.Add(1),
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [spell],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 0, false) { NativeAction = spell }],
            Combatants = [actor, .. extraAllies, target, .. drawSources, .. extraEnemies]
        }, BattleAtbMode.TurnBased);

        BattleSmartAttackPreview preview = battle.PreviewSmartAttack(actor.Id, target.Id);
        BattleAbility previewAbility = draw ? BattleAbility.Draw : BattleAbility.Magic;
        Assert.Equal(previewAbility, preview.Ability);
        Assert.Equal(draw ? "draw-source" : null, preview.DrawSourceId);
        Assert.Equal(1, preview.ActionId);
        int initialStock = battle.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count;
        BattleInput input = new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { SmartAttackPreview = confirm ? preview : null, ForcePhysical = forcePhysical };
        GameWorld.ExecuteBattleCommand(battle, input);

        BattleAttackCommand command = Assert.Single(battle.QueuedActions);
        Assert.Equal(forcePhysical ? BattleAbility.Attack : previewAbility, command.Ability);
        Assert.Equal(draw ? "draw-source" : target.Id, command.TargetId);
        Assert.Equal(draw ? target.Id : null, command.DrawCastTargetId);
        Assert.Equal(forcePhysical ? 0 : 1, command.ActionId);
        Assert.Equal(forcePhysical ? null : preview, command.SmartAttack);
        for (int frame = 0; frame < 250 && battle.Combatants[0].IsBusy; frame++)
        {
            battle.Tick();
        }
        Assert.False(battle.Combatants[0].IsBusy);
        Assert.Equal(forcePhysical || draw ? initialStock : initialStock - 1, battle.Combatants[0].MagicStocks.Single(stock => stock.Id == 1).Count);
        if (draw)
        {
            Assert.True(battle.Combatants.Single(recipient => recipient.Id == target.Id).CurrentHp < target.CurrentHp);
            Assert.Equal(target.CurrentHp, battle.Combatants.Single(recipient => recipient.Id == "draw-source").CurrentHp);
        }
    }

    [Fact]
    public void GuardianForceAlertAndRareItemApplyRegardlessOfAdmittedPartyRows()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        ExpandedBattleEntryAdmissionTests.SelectFourAllies(world, session);
        NativeCharacterVitals vitals = session.State.CharacterVitals!.DeepCopy().WithBattleParty([0, 1, 2, 3], 4);
        vitals.Records[3] = vitals.Records[3] with { Junction = vitals.Records[3].Junction with { Abilities = 78u | (82u << 8) } };
        session.SetCharacterVitals(vitals);
        Assert.Equal(0u, session.State.CharacterVitals!.Records[3].Junction.Abilities);
        Assert.Equal(0, session.State.CharacterVitals.GetEncounterAbilityFlags() & 3);

        const int gf = 15;
        vitals = session.State.CharacterVitals.AcquireGuardianForce(gf)
            .TeachGuardianForceAbility(gf, 78)
            .TeachGuardianForceAbility(gf, 82);
        session.SetCharacterVitals(vitals);

        BattleDefinition admitted = world.CreateEncounterBattle(87, 4);
        Assert.Contains(admitted.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 3);
        Assert.Equal(3, Assert.IsType<NativeBattleEntryInput>(admitted.NativeEntry).EncounterAbilityFlags & 3);
        Assert.True(admitted.RareItem);

        BattleDefinition final = world.CreateEncounterBattle(511, 4);
        Assert.Contains(final.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 3);
        Assert.Equal(3, Assert.IsType<NativeBattleEntryInput>(final.NativeEntry).EncounterAbilityFlags & 3);
        Assert.True(final.RareItem);

        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2], 4));
        BattleDefinition removed = world.CreateEncounterBattle(87, 4);
        Assert.DoesNotContain(removed.Combatants, actor => actor.IsPlayer && actor.MagicRecordId == 3);
        Assert.Equal(3, Assert.IsType<NativeBattleEntryInput>(removed.NativeEntry).EncounterAbilityFlags & 3);
        Assert.True(removed.RareItem);

        session.SetCharacterVitals(session.State.CharacterVitals!
            .SetGuardianForcePassiveAbilityEnabled(gf, 78, false)
            .SetGuardianForcePassiveAbilityEnabled(gf, 82, false));
        BattleDefinition disabled = world.CreateEncounterBattle(87, 4);
        Assert.Equal(0, Assert.IsType<NativeBattleEntryInput>(disabled.NativeEntry).EncounterAbilityFlags & 3);
        Assert.False(disabled.RareItem);

        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(state =>
        {
            state.Draw(out byte roll);
            return roll >= 236;
        });
        NativeBattleRandomState protectedRandom = random;
        NativeBattleRandomState unprotectedRandom = random;
        NativeBattleEntryEnemy[] enemies = [new(0, 0)];
        Assert.Equal((byte)0, NativeBattleEntryRules.ResolveExpanded(0, admitted.NativeEntry!.EncounterAbilityFlags, enemies, ref protectedRandom));
        Assert.Contains(NativeBattleEntryRules.ResolveExpanded(0, disabled.NativeEntry!.EncounterAbilityFlags, enemies, ref unprotectedRandom), new byte[] { 1, 2 });

        RareItemProbabilities rareWeights = new() { First = 0, Second = 0, Third = 0, Fourth = 100 };
        Assert.Equal((sbyte)3, BattleEnemyRewardDefinition.SelectDropSlot(0, admitted.RareItem, rareWeights));
        Assert.Equal((sbyte)0, BattleEnemyRewardDefinition.SelectDropSlot(0, disabled.RareItem, rareWeights));
    }
}
