using System.Collections.Immutable;
using Memoria.FF8.Tests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExpandedActionHeaderTests
{
    private static readonly BattleNativeAction Fire = Action(2, 1, 1, 2, "Fire", []);

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    public void ExtraPartyOwnersUseTheOriginalPlayerSequenceTable(int ally)
    {
        BattleSession session = Fixture();
        BattleActionPresentation command = Magic(ally);

        BattleModelOwner owner = new BattlePartyActorModel(new($"ally-{ally}"));
        Assert.False(session.ReadBattlePresentationPacketBindings().Aliases.TryEncodeExact(owner, out _));
        NativeBattleActionHeaderPreparation expected = new(owner, 2, NativeBattleCommandSequenceFixture.Values[2], -1, 1, 2, new(0x47E970, 1, "Fire"));
        Assert.Equal(expected, session.BuildExpandedActionHeaderPreparation(command, Fire));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddedEnemyKeepsSequenceSignedCameraAndRawTitleFlag(bool showTitle)
    {
        BattleSession session = Fixture();
        ImmutableArray<byte> raw = new byte[20].ToImmutableArray().SetItem(4, 0xFE).SetItem(9, showTitle ? (byte)0x80 : (byte)0x00);
        BattleNativeAction attack = Action(8, 9, 3, 44, "Enemy attack", raw);
        BattleActionPresentation command = new("enemy-4", "ally-0", BattleActionPhase.Windup, Request(new BattlePartyActorReference("ally-0")), BattleAbility.EnemyAbility, 9) { NativeSequence = 17 };

        NativeBattleActionDataReference? data = showTitle ? new(0x4950A0, 9, "Enemy attack") : null;
        Assert.Equal(new NativeBattleActionHeaderPreparation(new BattleEnemyActorModel(new("enemy-4")), 8, 17, -2, 9, 44, data), session.BuildExpandedActionHeaderPreparation(command, attack));
    }

    [Fact]
    public void ExtraPartyReflectionUsesNative247SequenceRatherThanSuppliedZero()
    {
        BattleSession session = Fixture();
        BattleActionPresentation command = Magic(3) with { NativeQueuedCommand = 247, NativeSequence = 0 };

        Assert.Equal(new NativeBattleActionHeaderPreparation(new BattlePartyActorModel(new("ally-3")), 247, 11, -1, 1, 2, null), session.BuildExpandedActionHeaderPreparation(command, Fire with { Command = 247 }));
    }

    [Fact]
    public void Graphic252UsesItsOwnActionEffectAndNoTitle()
    {
        BattleSession session = Fixture();
        BattleActionPresentation command = new("enemy-4", "ally-0", BattleActionPhase.Windup, Request(new BattlePartyActorReference("ally-0")), BattleAbility.EnemyAbility, 9)
        {
            NativeSequence = 17,
            NativeGraphic = new(81)
        };

        Assert.Equal(new NativeBattleActionHeaderPreparation(new BattleEnemyActorModel(new("enemy-4")), 252, 17, -1, 1, 81, null), session.BuildExpandedActionHeaderPreparation(command, Fire));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparedDrawSelectsOriginalStockOrCastHeaderWithoutLogicalWrites(bool cast)
    {
        BattleSession session = Fixture();
        BattleActionPresentation command = new("ally-3", "enemy-0", BattleActionPhase.Windup, Request(new BattleEnemyActorReference("enemy-0")), BattleAbility.Draw, 1, cast)
        {
            DrawCastTargetId = cast ? "enemy-1" : null,
            NativeDraw = new(1, 4, cast ? (byte)0 : (byte)4, false),
            NativeSequence = 3
        };
        string before = NativeBattleTestSnapshots.Json(session.Capture());

        NativeBattleActionHeaderPreparation header = session.BuildExpandedActionHeaderPreparation(command, Fire);

        NativeBattleActionDataReference data = cast ? new(0x495280, 0, "Cast Fire") : new(0x47EBD0, 6, "Draw");
        NativeBattleActionHeaderPreparation expected = new(new BattlePartyActorModel(new("ally-3")), 6, NativeBattleCommandSequenceFixture.Values[cast ? 9 : 10], -1, 1, cast ? (ushort)2 : (ushort)15, data);
        Assert.Equal(expected, header);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(session.Capture()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachOriginalPartyRowKeepsTheFullNativeHeader(int ally)
    {
        BattleSession native = Fixture(expanded: false);
        BattleSession expanded = Fixture();
        BattleActionPresentation command = Magic(ally);
        NativeBattleActionHeaderPreparation expected = new(new BattlePartyActorModel(new(command.ActorId)), 2, NativeBattleCommandSequenceFixture.Values[2], -1, 1, 2, new(0x47E970, 1, "Fire"));

        Assert.Equal(expected, native.BuildExpandedActionHeaderPreparation(command, Fire));
        Assert.Equal(expected, expanded.BuildExpandedActionHeaderPreparation(command, Fire));
    }

    private static BattleActionPresentation Magic(int ally) => new($"ally-{ally}", "enemy-0", BattleActionPhase.Windup, Request(new BattleEnemyActorReference("enemy-0")), BattleAbility.Magic, 1) { NativeSequence = 3 };

    private static BattleTargetRequest Request(BattleActorReference actor) => new(new BattleTargetSelection.Explicit([actor]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);

    private static BattleNativeAction Action(byte command, ushort action, int section, ushort effect, string name, ImmutableArray<byte> raw) => new(command, action, section, action)
    {
        Name = name,
        MagicId = effect,
        AttackType = 2,
        Power = 16,
        HitCount = 1,
        TargetFlags = 0x50,
        AttackFlags = 0,
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
        RawRecord = raw,
        SupplementalRecord = []
    };

    private static BattleSession Fixture(bool expanded = true)
    {
        string[] names = Enumerable.Repeat(string.Empty, 39).ToArray();
        names[6] = "Draw";
        names[9] = "Cast";
        string[] messages = Enumerable.Repeat(string.Empty, 119).ToArray();
        messages[11] = " 0123456789";
        BattleCombatantDefinition[] party = Enumerable.Range(0, expanded ? 11 : 3).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = "Ally", IsPlayer = true }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, expanded ? 5 : 1).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = "Enemy" }).ToArray();
        BattleDefinition definition = new()
        {
            Combatants = [.. party, .. enemies],
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeCommandNames = names,
            NativeGlobalMessages = messages,
            NativeGlobalRandom = new(123),
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 0, false) { NativeAction = Fire }]
        };
        return new BattleSession(definition, BattleAtbMode.TurnBased);
    }
}
