using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRingWorldMatrixTests
{
    [Fact]
    public void Original_Seifer_ring_copies_body_displacement_to_the_independent_Gunblade_across_restore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter encounter = assets.LoadEncounter(119);
        BattleDefinition seed = NativeBattleGraphicsEntryTests.Create(game, assets, 86, formation: encounter).Capture().Definition;
        List<BattleCombatantDefinition> actors = seed.Combatants.Where(actor => actor.IsPlayer).ToList();
        foreach (BattleEnemyPlacement placement in encounter.Slots.Where(slot => slot.NativeModelId != 255 && (slot.Enabled || slot.NativeModelId != 0)))
        {
            BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(placement.ModelId).Project(1);
            NativeBattleStatusAllocation statuses = assets.ReadEnemyNativeStatusData(placement.ModelId);
            actors.Add(new()
            {
                Id = $"enemy-{placement.Slot}",
                Name = assets.ReadEnemyName(placement.ModelId),
                ModelId = placement.ModelId,
                SceneSlot = placement.Slot,
                InitialActive = placement.Enabled,
                InitialHidden = placement.Hidden,
                InitialUntargetable = placement.Untargetable,
                InitialSuspended = placement.InitiallySuspended,
                NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(placement.ModelId),
                NativeStatus0 = statuses.Saved,
                NativeStatus1 = statuses.Battle,
                NativeStatusResistances = statuses.Resistances.ToArray(),
                Information246 = assets.ReadEnemyNativeInformation246(placement.ModelId),
                NativeTemplate = assets.ReadEnemyTemplate(placement.ModelId, checked((byte)placement.Level)),
                NativeAi = assets.ReadEnemyAi(placement.ModelId, 1) with { Scripts = [[0], [0], [0], [0], [0]] },
                MaximumHp = stats.MaximumHp,
                CurrentHp = stats.MaximumHp,
                Level = 1,
                Speed = 1,
                Attack = stats.Strength,
                Defense = stats.Vitality,
                Magic = stats.Magic,
                Spirit = stats.Spirit,
                ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray()
            });
        }
        BattleSession battle = new(seed with
        {
            Combatants = actors.ToArray(),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter),
            NativeActions = assets.NativeActions.ToArray(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 1200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        NativeBattleGraphicsModel body = Assert.Single(graphics.Models, model => (model.Flags0 & 2) != 0 && model.ModelId4 == 102);
        NativeBattleGraphicsModel sword = Assert.Single(graphics.Models, model => (model.Flags0 & 2) != 0 && model.ModelId4 == 98);
        Assert.Equal(sword.Index, body.ReadLinkedModelIndex140());
        Assert.Equal(body.Index, sword.ReadLinkedModelIndex140());
        Assert.NotEqual(body.CurrentBodyBoneMatrices!.Matrices.Length, sword.CurrentBodyBoneMatrices!.Matrices.Length);
        Assert.NotNull(body.GeometryWorldMatrix);
        int previousZ = body.GeometryWorldMatrix.Z;
        // Exercise the ordinary callback with different physical owners' resource displacements.
        // The ring world matrix is shared while their authored skeletons and decoded poses remain independent.
        BattleSnapshot snapshot = battle.Capture();
        graphics = graphics with
        {
            Models = graphics.Models.SetItem(body.Index, body with { SequenceState = body.SequenceState! with { Header40 = 512 } }).SetItem(sword.Index, sword with { SequenceState = sword.SequenceState! with { Header40 = 0 } })
        };
        battle = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(snapshot with { NativeGraphics = graphics }));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int callback = 0; callback < 2; callback++)
        {
            battle.Tick();
            restored.Tick();
            graphics = battle.NativeGraphics!;
            body = graphics.Models[body.Index];
            sword = graphics.Models[sword.Index];
            Assert.Equal((short)512, body.SequenceState!.Header40);
            Assert.Equal((short)0, sword.SequenceState!.Header40);
            Assert.NotNull(body.GeometryWorldMatrix);
            Assert.NotNull(sword.GeometryWorldMatrix);
            Assert.NotEqual(previousZ, body.GeometryWorldMatrix.Z);
            AssertMatrix(body.GeometryWorldMatrix, sword.GeometryWorldMatrix);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
    }

    private static void AssertMatrix(NativeBattleActionMatrix expected, NativeBattleActionMatrix actual)
    {
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
        Assert.Equal(expected.Scale, actual.Scale);
    }
}
