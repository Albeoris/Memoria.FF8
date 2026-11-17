using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleEntryAdvantageTests
{
    [Theory]
    [InlineData(0x800000u, 0)]
    [InlineData(0x200000u, 1)]
    [InlineData(0x400000u, 2)]
    [InlineData(0xE00000u, 0)]
    public void ForcedAdvantageDoesNotReadEnemyOwnersOrConsumeRandom(uint flags, byte expected)
    {
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(1);
        NativeBattleRandomState initial = random;
        Assert.Equal(expected, NativeBattleEntryRules.ResolveExpanded(flags, 0, null, ref random));
        Assert.Same(initial, random);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 4, 2)]
    [InlineData(7, 0, 0, 0, 4, 2)]
    [InlineData(15, 0, 0, 0, 3, 2)]
    [InlineData(1, 0, 0, 4, 0, 1)]
    [InlineData(1, 0, 1, 0, 4, 2)]
    [InlineData(1, 0, 1, 1, 0, 1)]
    [InlineData(2, 0, 2, 0, 1, 2)]
    [InlineData(2, 0, 2, 2, 0, 1)]
    [InlineData(2, 1, 0, 0, 0, 1)]
    public void LogicalFifthEnemyParticipatesInEveryReachedDatFlagDecision(byte phase, byte fieldAbilities, byte firstFour, byte fifth, byte expected, int draws)
    {
        NativeBattleEntryEnemy[] enemies = [.. Enumerable.Repeat(new NativeBattleEntryEnemy(0, firstFour), 4), new(0, fifth)];
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(phase);
        Assert.Equal(expected, NativeBattleEntryRules.ResolveExpanded(0, fieldAbilities, enemies, ref random));
        Assert.Equal((byte)(phase + draws), random.Indexes[0]);
    }

    [Fact]
    public void UnknownDatFailsBeforeAnyDrawExceptForKoOrForcedEntry()
    {
        NativeBattleEntryEnemy[] enemies = [new(0, 0), new(0, null)];
        NativeBattleRandomState random = NativeBattleRandomState.ForecastPhase(1);
        NativeBattleRandomState initial = random;
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryRules.ResolveExpanded(0, 0, enemies, ref random));
        Assert.Same(initial, random);
        enemies[1] = new(1, null);
        Assert.Equal((byte)4, NativeBattleEntryRules.ResolveExpanded(0, 0, enemies, ref random));
        Assert.Equal((byte)3, random.Indexes[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryRules.ResolveExpanded(0, 0, [], ref random));
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryRules.ResolveExpanded(0, 0, Enumerable.Repeat(enemies[0], 15).ToArray(), ref random));
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryRules.Resolve(0, 0, enemies, ref random));
    }

    [Fact]
    public void EnemyAdvantageAppliesToExtraActorsAndKeepsThePassiveGauge()
    {
        BattleDefinition definition = Definition() with { NativeEntry = new(0x40, 0) };
        definition.Combatants[4] = definition.Combatants[4] with { Information254 = null };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Equal((byte)2, battle.NativeEntryAdvantage);
        Assert.Equal(new[] { 0, 0, 0, 1234 }, battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Atb));
        Assert.Equal(new[] { BattleSession.AtbMaximum, BattleSession.AtbMaximum, BattleSession.AtbMaximum, BattleSession.AtbMaximum, BattleSession.AtbMaximum }, battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.Atb));
        Assert.Equal(new[] { 0x800000u, 0u, 0x800000u, 0x800000u }, battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.NativeStatus1 & 0x800000u));
        Assert.Equal((byte)1, battle.Capture().Random!.Indexes[0]);
    }

    [Fact]
    public void NaturalEntrySkipsInactivePrototypesAndRestoresWithoutAnotherDraw()
    {
        BattleDefinition definition = Definition(includeReserve: true);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Equal((byte)4, battle.NativeEntryAdvantage);
        Assert.Equal(new[] { BattleSession.AtbMaximum, 0, 0, BattleSession.AtbMaximum }, battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Atb));
        BattleCombatant added = battle.Combatants.Single(actor => actor.Id == "added");
        Assert.Equal(0, added.Atb);
        Assert.Equal(0x800000u, added.NativeStatus1 & 0x800000u);
        BattleCombatant reserve = battle.Combatants.Single(actor => actor.Id == "reserve");
        Assert.False(reserve.IsActive);
        Assert.Equal(4321, reserve.Atb);
        Assert.Equal(0u, reserve.NativeStatus1 & 0x800000u);

        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        Assert.Equal((byte)3, snapshot.Random!.Indexes[0]);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeEntryAdvantage = null }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeEntryAdvantage = 5 }));
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(snapshot.NativeEntryAdvantage, restored.NativeEntryAdvantage);
        Assert.Equal(snapshot.Random.Indexes.ToArray(), restored.Capture().Random!.Indexes.ToArray());
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void RequiredExpandedResourceFailsAndLegacyDefinitionKeepsUnknownAdvantage()
    {
        BattleDefinition definition = Definition();
        definition.Combatants[^1] = definition.Combatants[^1] with { Information254 = null };
        Assert.Contains("informationBYTE254", Assert.Throws<NotSupportedException>(() => new BattleSession(definition, BattleAtbMode.Normal)).Message);
        BattleSession legacy = new(definition with { NativeEntry = null }, BattleAtbMode.TurnBased);
        Assert.Null(legacy.NativeEntryAdvantage);
        Assert.Null(BattleSession.Restore(legacy.Capture()).NativeEntryAdvantage);
    }

    private static BattleDefinition Definition(bool includeReserve = false)
    {
        NativeStatDefinitions stats = MagicSystemFixture.Create().Definitions;
        byte[] character = new byte[36];
        for (int offset = 12; offset < character.Length; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 40;
            character[offset + 3] = 1;
        }
        ImmutableArray<ImmutableArray<byte>> spells = Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> passives = Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray();
        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(index =>
        {
            BattleNativePlayerStatRules rules = new(index, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), spells, passives, new byte[20].ToImmutableArray(), 0, stats) { NativeStatusPassiveFlags = index == 3 ? 0x10000u : 0u };
            BattleNativeCharacterGrowth growth = new(100, new byte[6].ToImmutableArray());
            BattleNativePlayerStatistics projected = rules.Project([], growth);
            return new BattleCombatantDefinition
            {
                Id = $"ally-{index}",
                Name = "Ally",
                IsPlayer = true,
                InitialAtb = 1234,
                MaximumHp = projected.MaximumHp,
                CurrentHp = index == 1 ? 0 : projected.MaximumHp,
                Level = rules.Level,
                Attack = projected.Strength,
                Defense = projected.Vitality,
                Magic = projected.Magic,
                Spirit = projected.Spirit,
                Speed = Math.Max(1, projected.Speed),
                Luck = projected.Luck,
                Hit = projected.Hit,
                Evasion = projected.Evasion,
                ElementDefenses = projected.ElementDefenses.ToArray(),
                ElementAttack = projected.ElementAttack,
                ElementAttackPercent = projected.ElementAttackPercent,
                NativeStatus0 = index == 1 ? (ushort)1 : index == 2 ? (ushort)4 : (ushort)0,
                NativeStatusResistances = rules.ProjectStatusResistances([]).ToArray(),
                NativePlayerStatRules = rules,
                NativeGrowth = growth
            };
        }).ToArray();
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate template = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 4).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy", SceneSlot = index, InitialAtb = 1234, Information254 = includeReserve ? null : (byte)0, NativeTemplate = includeReserve ? template : null }).ToArray();
        BattleCombatantDefinition added = new() { Id = "added", Name = "Added enemy", SceneSlot = includeReserve ? 8 : 4, InitialAtb = 1234, Information254 = 0 };
        BattleCombatantDefinition[] roster = includeReserve ? [.. allies, .. enemies, new() { Id = "reserve", Name = "Reserve enemy", SceneSlot = 4, InitialActive = false, InitialAtb = 4321, NativeTemplate = template with { Information254 = null } }, added] : [.. allies, .. enemies, added];
        return new()
        {
            UsesOriginalEnemySlots = includeReserve,
            AddedEnemySources = includeReserve ? ImmutableSortedDictionary<int, BattleEnemyRosterSource>.Empty.Add(8, new(1, 0, new string('a', 64))) : null,
            NativeEntry = new(0, 0),
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants = roster
        };
    }
}
