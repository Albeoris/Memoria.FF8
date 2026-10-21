using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCinematicCasterFixtureTests
{
    [Fact]
    public void Default_fixture_retains_formation94_and_party_caster()
    {
        NativeEffectFixtureRequest request = Request();
        Assert.Equal(94, request.EncounterId);
        Assert.Equal((byte)0, request.CasterActorIndex);
        NativeBattleEffectFixtureExporter.Validate(request);
        NativeBattleEffectFixtureExporter.Validate(request with { EncounterId = 462, CasterActorIndex = 3 });
        foreach (NativeEffectFixtureRequest invalid in new[] { request with { EncounterId = -1 }, request with { EncounterId = 1024 }, request with { CasterActorIndex = 1 }, request with { CasterActorIndex = 3, OrdinaryMagicAdmission = true } })
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(invalid));
        }
    }

    [Fact]
    public void Enemy_selection_retains_the_original_enabled_placement()
    {
        BattleEnemyPlacement placement = new(2, 17, Vector3.Zero, 35, false, false);
        BattleEncounter formation = new(462, 0, 0, 0, 0, [placement, placement with { Slot = 1, Enabled = false }]);
        Assert.Same(placement, NativeBattleEffectFixtureExporter.SelectEnemy(formation));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectEnemy(formation with { Enemies = [] }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectEnemy(formation with { Enemies = [placement, placement with { Slot = 1 }] }));
    }

    [Fact]
    public void Selected_original_formation462_caster_has_bone60_and_references_effect219()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? effect = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EFFECT");
        Assert.SkipUnless(directory is not null && effect == "219", "Select installed original resources and cinematic effect219 for this original caster fixture check.");
        using BattleAssets assets = new(directory!);
        BattleEncounter formation = assets.LoadEncounter(462);
        BattleEnemyPlacement enemy = NativeBattleEffectFixtureExporter.SelectEnemy(formation);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(enemy.ModelId);
        NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(resource.PoseSource);
        Assert.True(pose.BoneCount >= 61, $"The original formation caster supplies only {pose.BoneCount} body bones.");
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
        BattleNativeAction[] referenced = assets.NativeActions.Where(action => action.MagicId == 219 && ai.Abilities.Any(ability => ability.Command == action.Command && ability.Id == action.ActionId)).ToArray();
        Assert.NotEmpty(referenced);
        foreach (BattleNativeAction action in referenced)
        {
            byte sequence = NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action);
            Assert.Contains(ai.Abilities, ability => ability.Command == action.Command && ability.Id == action.ActionId && ability.Sequence == sequence);
        }
    }

    [Fact]
    public void Selected_original_formation981_caster_supplies_effect77_terminal_ability()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? effect = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EFFECT");
        Assert.SkipUnless(directory is not null && effect == "77", "Select installed original resources and cinematic effect77 for this original caster fixture check.");
        using BattleAssets assets = new(directory!);
        BattleEnemyPlacement enemy = NativeBattleEffectFixtureExporter.SelectEnemy(assets.LoadEncounter(981));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(126, enemy.ModelId);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.Untargetable);
        Assert.False(enemy.InitiallySuspended);
        NativeBattlePoseSource pose = Assert.IsType<NativeBattlePoseSource>(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        Assert.Equal(71, pose.BoneCount);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 356);
        Assert.Equal((ushort)77, action.MagicId);
        Assert.Equal(3, action.KernelSection);
        Assert.Equal(356, action.KernelIndex);
        Assert.Equal(assets.NativeKernelRecords.Single(row => row.Section == 3 && row.Index == 356).Bytes, action.RawRecord);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemy.ModelId, enemy.Level);
        Assert.Equal((byte)18, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        foreach (int slot in new[] { 2, 18, 34 })
        {
            Assert.Equal((byte)8, ai.Abilities[slot].Command);
            Assert.Equal((byte)18, ai.Abilities[slot].Sequence);
            Assert.Equal((ushort)356, ai.Abilities[slot].Id);
        }
        Assert.Equal((byte)0x0C, ai.Scripts[3][21]);
        Assert.Equal((byte)2, ai.Scripts[3][22]);
    }

    private static NativeEffectFixtureRequest Request()
    {
        return new("caster", 219, 8, 0, 0, 200, 60);
    }
}
