using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMegaSpark279CurrentBonesTests
{
    [Fact]
    public void Original_caster_current_bones_compose_and_restore_through_physical_temporary_memory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEGASPARK279_BONES_EVIDENCE");
        Assert.SkipUnless(directory is not null && output is not null, "Select original installed resources and a distinct evidence directory for the current-bone prerequisite.");
        using BattleAssets assets = new(directory!);
        Assert.False(Directory.Exists(output));
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 106 && row.MagicId == 279 && row.KernelSection == 3 && row.KernelIndex == 106);
        BattleEncounter encounter = assets.LoadEncounter(377);
        Assert.Equal(127, encounter.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(encounter, true));
        Assert.Equal(30, enemy.ModelId);
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(151, enemy.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -4600), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        NativeEffectFixtureRequest request = new("mega-spark279-current-bones", 279, 8, 106, 0, 1200, 240) { EncounterId = encounter.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, Path.Combine(output!, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot before = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "fixture", "queued-action104.battle.json"));
        BattleSession original = BattleSession.Restore(before);
        NativeBattleGraphicsState graphics = before.NativeGraphics ?? throw new InvalidDataException("The original component requires its current graphics owner.");
        const int caster = 3;
        BattleCombatantDefinition definition = original.GetCombatantDefinition(graphics.ActorIds[caster] ?? throw new InvalidDataException("The original caster requires its current allocation identity."));
        Assert.Equal(30, definition.ModelId);
        NativeBattleGraphicsModel model = graphics.Models[caster];
        Assert.Equal(caster, model.Index);
        Assert.Equal((byte)46, model.ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(definition).PoseSource ?? throw new InvalidDataException("The selected original caster requires its actual skeleton.");
        Assert.True(pose.BoneCount > 21);
        string skeletonHash = Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan()));
        ImmutableArray<NativeBattleActionMatrix> local = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.Equal(pose.BoneCount, local.Length);
        Assert.Equal(skeletonHash, model.CurrentBodyBoneMatrices!.SkeletonSha256);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("The caster requires its actual published world matrix.");
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new InvalidDataException("The caster requires its actual decoded pose."), pose);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(pose, decoded)), JsonSerializer.Serialize(local));
        Directory.CreateDirectory(output!);
        Save("before", before);

        BattleSnapshot composed = Compose(before);
        ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.ComposeStageCamera(local, world, 0);
        ImmutableArray<NativeBattleActionMatrix> actual = NativeBattleBodyBoneMatrices.Require(composed.NativeGraphics!.Models[caster], pose);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.NotEqual(JsonSerializer.Serialize(local[21]), JsonSerializer.Serialize(actual[21]));
        Save("composed", composed);
        BattleSnapshot restored = Restore(composed);
        Assert.Equal(JsonSerializer.Serialize(local), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(restored.NativeGraphics!.Models[caster], pose)));
        Assert.Equal(JsonSerializer.Serialize(world), JsonSerializer.Serialize(restored.NativeGraphics.Models[caster].GeometryWorldMatrix));
        Save("restored", restored);

        BattleSnapshot coldBefore = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "before.battle.json"));
        BattleSnapshot coldComposed = Compose(coldBefore);
        Assert.Equal(JsonSerializer.Serialize(composed), JsonSerializer.Serialize(coldComposed));
        BattleSnapshot coldAtCompose = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "composed.battle.json"));
        Assert.Equal(JsonSerializer.Serialize(restored), JsonSerializer.Serialize(Restore(coldAtCompose)));
        Assert.Equal(JsonSerializer.Serialize(restored), JsonSerializer.Serialize(BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, "restored.battle.json"))).Capture()));
        File.WriteAllText(Path.Combine(output!, "current-bones.json"), JsonSerializer.Serialize(new { Status = "passed", Caster = caster, ResourceModel = definition.ModelId, NativeModel = model.ModelId4, SkeletonSha256 = skeletonHash, CurrentWorld = world, Before = Pack(local), Composed = Pack(actual), Restored = Pack(NativeBattleBodyBoneMatrices.Require(restored.NativeGraphics.Models[caster], pose)), SelectedBones = new[] { 10, 17, 18, 21 } }, new JsonSerializerOptions { WriteIndented = true }));

        BattleSnapshot Compose(BattleSnapshot snapshot)
        {
            BattleSnapshot current = BattleSession.Restore(snapshot).Capture();
            NativeBattleGraphicsState owner = current.NativeGraphics!;
            NativeBattleGraphicsModel actor = owner.Models[caster];
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, actor.GeometryWorldMatrix ?? throw new InvalidDataException("The current world owner is missing."));
            return current with { NativeGraphics = owner with { Models = owner.Models.SetItem(caster, actor) } };
        }

        BattleSnapshot Restore(BattleSnapshot snapshot)
        {
            BattleSnapshot current = BattleSession.Restore(snapshot).Capture();
            NativeBattleFileStaging staging = current.NativeFileStaging ?? throw new InvalidDataException("The original component requires physical temporary memory.");
            byte[] cursor = staging.Read(NativeBattleTemporaryMemory.Cursor, 4);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88);
            NativeBattleGraphicsState owner = current.NativeGraphics!;
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(owner.Models[caster], pose, temporary);
            staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            Assert.Equal(prepared.Temporary58.ReadSnapshot(0, 88).Data, staging.ReadSnapshot(allocation.Address, 88).Data);
            Assert.Equal(prepared.Temporary58.ReadSnapshot(0, 88).Known, staging.ReadSnapshot(allocation.Address, 88).Known);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            Assert.Equal(cursor, staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
            return current with { NativeGraphics = owner with { Models = owner.Models.SetItem(caster, prepared.Model) }, NativeFileStaging = staging };
        }

        void Save(string name, BattleSnapshot snapshot)
        {
            File.WriteAllText(Path.Combine(output!, name + ".battle.json"), JsonSerializer.Serialize(snapshot));
        }

        static NativeBattlePackedMatrix32[] Pack(ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            return matrices.Select(matrix =>
            {
                NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
                Assert.False(packed.Known[18]);
                Assert.False(packed.Known[19]);
                Assert.All(packed.Known.Where((_, index) => index is not (18 or 19)), known => Assert.True(known));
                return packed;
            }).ToArray();
        }
    }
}
