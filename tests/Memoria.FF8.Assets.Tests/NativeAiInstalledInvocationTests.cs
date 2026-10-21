using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAiInstalledInvocationTests
{
    [Theory]
    [InlineData(BattleDifficulty.Normal)]
    [InlineData(BattleDifficulty.Hard)]
    public void OriginalCavernEncounterTablesInitializeAndRunEachParticipantAfterJsonRestore(BattleDifficulty difficulty)
    {
        using GameAssets fields = new(TestResources.GameDirectory);
        using BattleAssets assets = new(TestResources.GameDirectory);
        int[] formations = Enumerable.Range(131, 5).SelectMany(field => fields.LoadField(field).RandomEncounters!.Formations).Select(formation => (int)formation).Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(87, 7), formations);
        foreach (int formation in formations)
        {
            List<BattleCombatantDefinition> actors = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, Level = 7, MaximumHp = 1_000_000, CurrentHp = 1_000_000, Speed = 1, NativeStatusResistances = Enumerable.Repeat((byte)100, 40).ToArray() }];
            foreach (BattleEnemyPlacement placement in assets.LoadEncounter(formation).Enemies)
            {
                int level = NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [7], 0, 0);
                BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
                actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), SceneSlot = placement.Slot, ModelId = placement.ModelId, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 1, InitialAtb = 0, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(placement.ModelId, level), NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, (byte)placement.Level) });
            }
            BattleSession initialized = new(new()
            {
                EncounterId = formation,
                PersistentAiVariables = new uint[8],
                Combatants = actors.ToArray(),
                NativeActions = assets.NativeActions.ToArray(),
                ActionDefinitions = assets.ActionDefinitions.ToArray(),
                NativeStatusRules = assets.ReadNativeStatusRules(0),
                NativeMessageSpeed = 1,
                NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages()
            }, BattleAtbMode.Normal);
            initialized.SetDifficulty(difficulty);
            for (int admission = 1; admission < actors.Count; admission++)
            {
                initialized.Tick();
            }
            BattleSnapshot initial = initialized.Capture();
            Assert.Empty(initialized.QueuedActions);
            for (int index = 1; index < actors.Count; index++)
            {
                BattleCombatantSnapshot[] combatants = initial.Combatants.ToArray();
                combatants[index] = combatants[index] with { Atb = BattleSession.AtbMaximum };
                BattleSnapshot ready = initial with { Combatants = combatants };
                BattleSession original = BattleSession.Restore(ready);
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(ready))!);
                original.Tick();
                restored.Tick();
                Assert.Equal((byte)1, original.Capture().Combatants[index].NativeParticipant!.TurnCount);
                Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
        }
    }

    [Theory]
    [InlineData(39, BattleDifficulty.Normal)]
    [InlineData(88, BattleDifficulty.Normal)]
    [InlineData(91, BattleDifficulty.Normal)]
    [InlineData(92, BattleDifficulty.Normal)]
    [InlineData(39, BattleDifficulty.Hard)]
    [InlineData(88, BattleDifficulty.Hard)]
    [InlineData(91, BattleDifficulty.Hard)]
    [InlineData(92, BattleDifficulty.Hard)]
    public void InstalledRandomTurnProgramsRespectDifficultyAndContinueAfterJsonRestore(int encounter, BattleDifficulty difficulty)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        foreach (BattleEnemyPlacement placement in assets.LoadEncounter(encounter).Enemies.Where(enemy => enemy.ModelId == 43))
        {
            for (byte seed = 0; seed < 12; seed++)
            {
                BattleDefinition definition = Definition(assets, placement.ModelId, assets.ReadEnemyAi(placement.ModelId, 7)) with
                {
                    EncounterId = encounter,
                    NativeSeed = seed,
                    PersistentAiVariables = new uint[8],
                    NativeStatusRules = assets.ReadNativeStatusRules(0)
                };
                definition = definition with { Combatants = [definition.Combatants[0] with { MaximumHp = 1_000_000, CurrentHp = 1_000_000 }, definition.Combatants[1] with { InitialAtb = BattleSession.AtbMaximum }] };
                BattleSession original = new(definition, BattleAtbMode.Normal);
                original.Tick();
                original.SetDifficulty(difficulty);
                string json = JsonSerializer.Serialize(original.Capture());
                BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
                original.Tick();
                restored.Tick();
                if (difficulty == BattleDifficulty.Hard)
                {
                    // Keep the original random condition and its draw, but route both edges past its hidden return.
                    NativeBattleAiDefinition ai = definition.Combatants[1].NativeAi!;
                    byte[][] scripts = ai.Scripts.Select(script => script.ToArray()).ToArray();
                    Assert.Equal((byte)2, scripts[1][0]);
                    Assert.Equal((byte)2, scripts[1][1]);
                    Assert.Equal((byte)0, scripts[1][8]);
                    int falsePc = 8 + System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(scripts[1].AsSpan(6, 2));
                    scripts[1][8] = 35;
                    System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(scripts[1].AsSpan(9, 2), checked((short)(falsePc - 11)));
                    BattleDefinition controlDefinition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { NativeAi = ai with { Scripts = scripts } }] };
                    BattleSession control = new(controlDefinition, BattleAtbMode.Normal);
                    control.Tick();
                    control.Tick();
                    AssertRandomEqual(control.Capture().Random!, original.Capture().Random!);
                    Assert.Equal(JsonSerializer.Serialize(control.QueuedActions), JsonSerializer.Serialize(original.QueuedActions));
                }
                Assert.Equal(JsonSerializer.Serialize(original.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
        }
    }

    [Theory]
    [InlineData(0, 0, 4)]
    [InlineData(61, 1, 6)]
    [InlineData(61, 2, 6)]
    public void InstalledZeroAbilityRowsSkipDispatchWithoutInventingAnActionOrDrawingRandom(int model, int band, byte slot)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleAiDefinition installed = assets.ReadEnemyAi(model, 7);
        Assert.Equal((ushort)0, installed.Abilities[16 * band + slot].Id);
        NativeBattleAiDefinition program = installed with { LevelBand = band, Scripts = [[12, slot, 0], [0], [0], [0], [0]] };
        BattleSession battle = AdmitInitial(Definition(assets, model, program));

        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        AssertRandomEqual(NativeBattleRandomState.Initialize(0), battle.Capture().Random!);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(3)]
    [InlineData(7)]
    public void DirectZeroPreparedIdUsesTheSameNativeGuard(byte opcode)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        byte[] script = opcode == 6 ? [6, 0] : [opcode, 0, 6, 0];
        NativeBattleAiDefinition program = assets.ReadEnemyAi(0, 7) with { Scripts = [script, [0], [0], [0], [0]] };

        BattleSession battle = AdmitInitial(Definition(assets, 0, program));

        Assert.Empty(battle.QueuedActions);
        AssertRandomEqual(NativeBattleRandomState.Initialize(0), battle.Capture().Random!);
    }

    [Fact]
    public void RandomZeroAbilityStillConsumesItsOriginalSelectionByteButNoDispatchRandom()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleAiDefinition program = assets.ReadEnemyAi(0, 7) with { Scripts = [[11, 4, 4, 4, 0], [0], [0], [0], [0]] };
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0).Draw(out _);

        BattleSession battle = AdmitInitial(Definition(assets, 0, program));

        Assert.Empty(battle.QueuedActions);
        AssertRandomEqual(expected, battle.Capture().Random!);
    }

    [Theory]
    [InlineData(1, 329)]
    [InlineData(2, 330)]
    public void InstalledCommand236PreservesItsWordIdAndOriginalEnemyRecordAcrossRestore(byte slot, ushort id)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleAiDefinition program = assets.ReadEnemyAi(128, 7) with { Scripts = [[4, 200, 12, slot, 0], [0], [0], [0], [0]] };
        BattleSession battle = AdmitInitial(Definition(assets, 128, program));
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);

        Assert.Equal((byte)236, queued.NativeQueuedCommand);
        Assert.Equal(id, queued.NativeAction!.ActionId);
        Assert.Equal(assets.NativeActions.Single(action => action.Command == 8 && action.ActionId == id) with { Command = 236 }, queued.NativeAction);
        Assert.Equal(id, NativeBattleActionGraphics.Word(battle.NativeActionPackets.Headers[0], 4));
        string json = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(json, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!).Capture()));
    }

    private static BattleSession AdmitInitial(BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.Tick();
        return battle;
    }

    private static void AssertRandomEqual(NativeBattleRandomState expected, NativeBattleRandomState actual)
    {
        Assert.Equal(expected.Indexes.ToArray(), actual.Indexes.ToArray());
        Assert.Equal(expected.Lane, actual.Lane);
    }

    private static BattleDefinition Definition(BattleAssets assets, int model, NativeBattleAiDefinition program)
    {
        return new()
        {
            NativeActions = assets.NativeActions.ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", ModelId = model, NativeAi = program }
            ]
        };
    }
}
