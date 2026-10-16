using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAiInstalledInvocationTests
{
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
