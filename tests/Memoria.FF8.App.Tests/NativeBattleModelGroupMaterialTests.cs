using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleModelGroupMaterialTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void Installed_group_callbacks_preserve_current_enemy_body_and_authored_player_materials(bool deformation, int selection)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 0, 116);
        BattleEncounter formation = assets.LoadEncounter(594);
        BattleEnemyPlacement placement = formation.Enemies.Single(enemy => enemy.Slot == 0);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleCombatantDefinition enemy = definition.Combatants.Single(actor => !actor.IsPlayer) with { ModelId = placement.ModelId, SceneSlot = placement.Slot, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(placement.ModelId), NativeStatusResistances = assets.ReadEnemyNativeStatusData(placement.ModelId).Resistances.ToArray() };
        BattleCombatantDefinition party = definition.Combatants.Single(actor => actor.IsPlayer);
        if (selection == 2)
        {
            party = party with { ModelId = 1, WeaponId = 7 };
        }
        BattleCombatantDefinition[] actors = [party, enemy];
        definition = definition with
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        BattleSnapshot initial = battle.Capture();
        NativeBattleGraphicsState graphics = initial.NativeGraphics!;
        bool enemyBody = selection == 0;
        int actor = graphics.ActorIds.IndexOf(enemyBody ? "enemy" : "party");
        Assert.InRange(actor, 0, 6);
        NativeBattleGraphicsResource resource = Invoke<NativeBattleGraphicsResource>(battle, "NativeGraphicsPoseResource", graphics, actor);
        byte group = selection == 2 ? (byte)1 : (byte)0;
        bool weapon = selection == 2;
        Assert.Equal(enemyBody, resource.GeometryRelocated507550);
        if (weapon)
        {
            Assert.True(resource.LinkedGeometryUsesBodySkeleton);
        }
        NativeBattleRawMesh mesh = weapon ? resource.LinkedGeometry!.Value.Single() : resource.Geometry!.Value[group];
        NativeBattleModelTextureBinding binding = (weapon ? graphics.Models[actor].LinkedTextureSlots : graphics.Models[actor].BodyTextureSlots)!;
        NativeBattleRawMesh relocated = resource.GeometryRelocated507550 && !weapon ? mesh : mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToImmutableArray() };
        if (enemyBody)
        {
            // Current CPU bytes require no original-row lookup, even when that lookup is missing.
            binding = new(Enumerable.Repeat<byte?>(null, 16).ToImmutableArray());
            NativeBattleRawPolygon textured = mesh.Polygons.First(polygon => polygon.Bytes.Length == (polygon.Quad ? 20 : 16) && (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(10)) >> 6) is >= 224 and < 240 && (polygon.Bytes[10] & 63) < 16);
            Assert.Throws<NotSupportedException>(() => NativeBattleRawGeometryPackets.Relocate507550(textured, binding));
        }
        graphics = deformation ? Invoke<NativeBattleGraphicsState>(battle, "ScheduleNativeModelDeformation50F250", graphics, actor, group, new NativeBattleVector(0, 0, 0), (short)0) : graphics with { Auxiliary = graphics.Auxiliary.AppendModelGroupFade50F0B0(actor, group) };
        graphics = graphics with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() };
        PublishGraphics(battle, graphics);
        battle = Restore(battle.Capture());
        int slot = graphics.Auxiliary.Tail!.Value;
        int emitted = 0;
        for (int callback = 0; callback < 15; callback++)
        {
            BattleSnapshot before = battle.Capture();
            NativeBattleGraphicsModel model = before.NativeGraphics!.Models[actor];
            NativeBattleCameraProjection projection = before.NativeCamera!.Projection!;
            NativeBattleAuxiliaryTask task = before.NativeGraphics.Auxiliary.Slots[slot];
            ImmutableArray<NativeBattleActionMatrix> matrices;
            if (deformation)
            {
                byte bone = task.Payload[5];
                NativeBattleActionMatrix retained = callback == 0 ? NativeBattlePoseMatrices.ReadDeformationBone50F2E0(model, resource.PoseSource!, bone) : before.NativeEffectMemory!.SequenceDeformationMatrix1D99BF8!;
                matrices = NativeBattlePoseMatrices.DeformedGroupMatrices50F2E0(model, resource.PoseSource!, projection.View!, bone, retained);
            }
            else
            {
                model = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(model, resource.PoseSource!, projection.View!);
                matrices = NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource!);
            }
            uint sourceColor = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(16));
            uint color = (definition.NativeGraphics!.Rules.SummonColors!.Interpolate(sourceColor, (callback << 12) / 15, 0) & 0xFFFFFFu) | 0x02000000u;
            ImmutableArray<NativeBattleGeometryPrimitive> expected = NativeBattleRawGeometryPackets.Emit5099D0([relocated], matrices, projection, color, 1, null, 17, 4, null);
            BattleSession? clone = callback == 7 ? BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!) : null;
            int cursor = before.NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value;
            battle = Advance(battle);
            if (clone is not null)
            {
                clone = Advance(clone);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
            }
            NativeBattleLogicalPacket[] actual = battle.NativeGraphics!.PrimitiveFrame!.Pending.Packets.Where(packet => packet.Reference.ByteOffset >= cursor).OrderBy(packet => packet.Reference.ByteOffset).ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (int packet = 0; packet < expected.Length; packet++)
            {
                Assert.Equal(expected[packet].Words.Skip(1), actual[packet].Words.Skip(1));
            }
            emitted += actual.Length;
            Assert.Equal(callback < 14, (battle.NativeGraphics.Auxiliary.Slots[slot].Flags & 1) != 0);
            Assert.Equal((byte)(callback + 1), battle.NativeGraphics.Auxiliary.Slots[slot].Payload[7]);
        }
        Assert.True(emitted > 0);
    }

    private static BattleSession Advance(BattleSession battle)
    {
        NativeBattleGraphicsState graphics = Invoke<NativeBattleGraphicsState>(battle, "AdvanceNativeAuxiliary", battle.NativeGraphics!);
        PublishGraphics(battle, graphics);
        return battle;
    }

    private static void PublishGraphics(BattleSession battle, NativeBattleGraphicsState graphics)
    {
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, graphics);
    }

    private static BattleSession Restore(BattleSnapshot snapshot)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
    }

    private static T Invoke<T>(BattleSession battle, string name, params object[] arguments)
    {
        return (T)typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments)!;
    }
}
