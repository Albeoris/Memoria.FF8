using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleShotgun281CurrentBonesTests
{
    [Fact]
    public void Original_caster_current_bones_compose_and_restore_through_physical_temporary_memory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? snapshotPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_SHOTGUN281_BONES_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SHOTGUN281_BONES_EVIDENCE");
        Assert.SkipUnless(directory is not null && snapshotPath is not null && output is not null, "Select original installed resources, a queued original component and a distinct evidence directory.");
        using BattleAssets assets = new(directory!);
        byte[] before = File.ReadAllBytes(snapshotPath!);
        using JsonDocument archive = JsonDocument.Parse(before);
        JsonElement source = archive.RootElement;
        NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(NativeBattleFixtureState.ReadArchived<NativeBattleGraphicsState>(source, "NativeGraphics"));
        const int caster = 3;
        string actorId = Assert.IsType<string>(ReadOriginalModelActorId(graphics, caster));
        BattleCombatantDefinition[] actors = Assert.IsType<BattleCombatantDefinition[]>(NativeBattleFixtureState.ReadArchived<BattleCombatantDefinition[]>(source.GetProperty("Definition"), "Combatants"));
        BattleCombatantDefinition definition = Assert.Single(actors, actor => actor.Id == actorId);
        Assert.Equal(78, definition.ModelId);
        NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, caster);
        Assert.Equal(model, graphics.ActorModels.Get(RequireOriginalModelOwner(graphics, caster)));
        Assert.Equal((byte)94, model.ModelId4);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(definition).PoseSource ?? throw new InvalidDataException("The selected original caster requires its actual skeleton.");
        Assert.Equal(24, pose.BoneCount);
        const string skeletonHash = "5212EFFA062696DF080681A130D01AB510024A0D6742F235A1057BF031A24344";
        Assert.Equal(skeletonHash, Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan())));
        ImmutableArray<NativeBattleActionMatrix> local = NativeBattleBodyBoneMatrices.Require(model, pose);
        Assert.Equal(24, local.Length);
        Assert.Equal(skeletonHash, model.CurrentBodyBoneMatrices!.SkeletonSha256);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("The caster requires its actual published world matrix.");
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new InvalidDataException("The caster requires its actual decoded pose."), pose);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(pose, decoded)), JsonSerializer.Serialize(local));
        NativeBattleGraphicsModel composed = NativeBattleBodyBoneMatrices.Compose5095B0(model, world);
        ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.ComposeStageCamera(local, world, 0);
        ImmutableArray<NativeBattleActionMatrix> actual = NativeBattleBodyBoneMatrices.Require(composed, pose);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.NotEqual(JsonSerializer.Serialize(local[23]), JsonSerializer.Serialize(actual[23]));

        NativeBattleFileStaging staging = Assert.IsType<NativeBattleFileStaging>(NativeBattleFixtureState.ReadArchived<NativeBattleFileStaging>(source, "NativeFileStaging"));
        byte[] cursor = staging.Read(NativeBattleTemporaryMemory.Cursor, 4);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(composed, pose, temporary);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
        Assert.Equal(prepared.Temporary58.ReadSnapshot(0, 88).Data, staging.ReadSnapshot(allocation.Address, 88).Data);
        Assert.Equal(prepared.Temporary58.ReadSnapshot(0, 88).Known, staging.ReadSnapshot(allocation.Address, 88).Known);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        Assert.Equal(cursor, staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        ImmutableArray<NativeBattleActionMatrix> restored = NativeBattleBodyBoneMatrices.Require(prepared.Model, pose);
        Assert.Equal(JsonSerializer.Serialize(local), JsonSerializer.Serialize(restored));
        Assert.Equal(JsonSerializer.Serialize(world), JsonSerializer.Serialize(prepared.Model.GeometryWorldMatrix));
        Assert.Equal(before, File.ReadAllBytes(snapshotPath!));

        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "current-bones.json"), JsonSerializer.Serialize(new { Status = "passed", Caster = caster, ResourceModel = definition.ModelId, NativeModel = model.ModelId4, SkeletonSha256 = skeletonHash, CurrentWorld = world, Before = Pack(local), Composed = Pack(actual), Restored = Pack(restored), SelectedBone = 23 }, new JsonSerializerOptions { WriteIndented = true }));

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
