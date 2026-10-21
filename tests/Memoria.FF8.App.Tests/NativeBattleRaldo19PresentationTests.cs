using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRaldo19PresentationTests
{
    [Fact]
    public void Original_Raldo_component_uses_its_actual_actor_and_hit_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([19], "MEMORIA_FF8_RALDO", SelectOriginalFixture);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RALDO_EVIDENCE")!;
        BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "queued.battle.json"));
        BattleSnapshot completed = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "completed.battle.json"));
        NativeBattleRaldo19State state = completed.NativeEffectMemory!.RequireRaldo19();
        short actor = BinaryPrimitives.ReadInt16LittleEndian(state.BallC4.Read(0xAC, 2));
        Assert.Equal((short)4, actor);
        Assert.Equal((short)1, state.Word(0x48));
        foreach (int index in new[] { 4, 5, 6 })
        {
            NativeBattleGraphicsModel before = queued.NativeGraphics!.Models[index];
            NativeBattleGraphicsModel after = completed.NativeGraphics!.Models[index];
            Assert.Equal((byte)0x70, after.ModelId4);
            Assert.Equal(before.Transform, after.Transform);
            Assert.Equal(before.Rotation12, after.Rotation12);
            Assert.Equal(before.Rotation14, after.Rotation14);
            Assert.Equal(before.Rotation16, after.Rotation16);
            Assert.Equal(before.RotationPadding18, after.RotationPadding18);
            Assert.Equal(before.Flags0 & 4, after.Flags0 & 4);
        }
        File.WriteAllText(Path.Combine(output, "raldo-restoration.json"), JsonSerializer.Serialize(new { SelectedActor = actor, Caster = 3, Formation = 62, Stage = 12, Command = 8, Action = 244, Sequence = 13, HitFlag = state.Word(0x48), RestoredActors = new[] { 4, 5, 6 } }));
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)19, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 244 && row.MagicId == 19 && row.KernelSection == 3 && row.KernelIndex == 244);
        BattleEncounter formation = assets.LoadEncounter(62);
        Assert.Equal(12, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 95, 96, 96, 96 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.All(enemies, enemy =>
        {
            Assert.False(enemy.Hidden);
            Assert.False(enemy.InitiallySuspended);
            Assert.False(enemy.Untargetable);
            Assert.NotNull(assets.ReadEnemyNativeGraphicsResource(enemy.ModelId).PoseSource);
        });
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(enemies[0].ModelId, enemies[0].Level);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(ai, action));
        return new("raldo19", effect, action.Command, action.ActionId, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PartyModelId = 0, CrisisLevel = action.CrisisLevel, PreserveOriginalFormationEnemies = true };
    }
}
