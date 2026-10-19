using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRegionalAiTests
{
    public static IEnumerable<object[]> OriginalRegionalFormations()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using WorldAssets world = new(TestResources.GameDirectory);
        SortedSet<int> formations = [9, 10, 13, 26, 27, 28, 29, 94];
        int[] fields = [131, 132, 133, 134, 135, 215, 217, 309, 310, 315, 321, 322, 323, 325, 327, 328, 330, 332, 340, 342, 344];
        foreach (int field in fields)
        {
            NativeFieldEncounterDefinition source = game.LoadField(field).RandomEncounters!;
            Assert.True(source.Rate > 0);
            formations.UnionWith(source.Formations.Select(value => (int)value));
        }
        AddWorld(24576, -29406, [6, 4, 10, 24]);
        AddWorld(-16384, -40960, [0, 6, 10, 14, 15]);
        Assert.Equal(64, formations.Count);
        foreach (int formation in formations)
        {
            foreach (int partyLevel in new[] { 7, 25, 40 })
            {
                yield return [formation, partyLevel];
            }
        }

        void AddWorld(int x, int y, byte[] grounds)
        {
            foreach (byte ground in grounds)
            {
                NativeWorldEncounterDefinition source = world.ReadRandomEncounterDefinition(x, y, ground, 1)!;
                Assert.True(source.Rate > 0);
                formations.UnionWith(source.Formations.Select(value => (int)value));
            }
        }
    }

    [Theory]
    [MemberData(nameof(OriginalRegionalFormations))]
    public void Original_regional_initialization_and_turns_preserve_independent_json_continuation(int encounter, int partyLevel)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(encounter);
        List<BattleCombatantDefinition> actors = [];
        for (int party = 0; party < 3; party++)
        {
            actors.Add(new() { Id = $"party-{party}", Name = "Party", ModelId = party, IsPlayer = true, NativeCharacterFlag100 = assets.ReadCharacterNativeConditionFlag100(party), Level = partyLevel, MaximumHp = 1_000_000, CurrentHp = 1_000_000, NativeStatusResistances = Enumerable.Repeat((byte)100, 40).ToArray() });
        }
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(0);
        foreach (BattleEnemyPlacement placement in formation.Slots.Where(slot => slot.ModelId >= 0))
        {
            byte roll = 0;
            if (placement.Enabled)
            {
                random = random.Draw(out roll);
            }
            int level = placement.Enabled ? NativeBattleEnemyLevelRules.Resolve((byte)placement.Level, [checked((byte)partyLevel)], roll) : 0;
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(level);
            actors.Add(new() { Id = $"enemy-{placement.Slot}", Name = assets.ReadEnemyName(placement.ModelId), ModelId = placement.ModelId, SceneSlot = placement.Slot, InitialActive = placement.Enabled, InitialHidden = placement.Hidden, InitialSuspended = placement.InitiallySuspended, InitialUntargetable = placement.Untargetable, Level = level, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 10_000, InitialAtb = 10_000, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(), NativeAi = assets.ReadEnemyAi(placement.ModelId, level), NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, (byte)placement.Level) });
        }
        // An original-resource AI component with an explicit cold process and durable variable clear.
        // Graphical cast acceptance is exercised separately through the current game host.
        BattleDefinition definition = new() { Name = "Regional original AI component", EncounterId = encounter, StageId = formation.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), InitialRandom = random, PersistentAiVariables = new uint[8], Combatants = actors.ToArray(), ActionDefinitions = assets.ActionDefinitions.ToArray(), NativeActions = assets.NativeActions.ToArray(), NativeStatusRules = assets.ReadNativeStatusRules(0), NativeMessageSpeed = 1, NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.Tick();
        AssertContinuation(battle);
        for (int callback = 0; callback < 200; callback++)
        {
            battle.Tick();
        }
        Assert.Contains(battle.Capture().Combatants, actor => actor.Id.StartsWith("enemy-", StringComparison.Ordinal) && actor.NativeParticipant!.TurnCount > 0);
        AssertContinuation(battle);
    }

    private static void AssertContinuation(BattleSession battle)
    {
        BattleSnapshot snapshot = battle.Capture();
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        first.Tick();
        second.Tick();
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
    }
}
