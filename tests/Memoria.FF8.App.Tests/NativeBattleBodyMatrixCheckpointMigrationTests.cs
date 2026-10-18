using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleBodyMatrixCheckpointMigrationTests
{
    [Fact]
    public void Explicit_end_of_graphics_callback_checkpoint_adds_only_proved_local_body_publications()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_BODY_MATRIX_SOURCE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "Set MEMORIA_FF8_BODY_MATRIX_SOURCE to the explicitly selected end-of-callback battle snapshot.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BODY_MATRIX_OUTPUT") ?? throw new InvalidDataException("A separately named output is required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output));
        Assert.False(File.Exists(output));
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(input!);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.True(graphics.Callback > 0);
        Assert.Null(graphics.OriginalEffect);
        ImmutableArray<NativeBattleGraphicsModel>.Builder models = graphics.Models.ToBuilder();
        int changed = 0;
        for (int index = 0; index < models.Count; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            Assert.NotEqual((byte?)0x8F, model.ModelId4);
            Assert.Null(model.CurrentBodyBoneMatrices);
            Assert.Null(model.ActiveStagedResourceRow100);
            Assert.Null(model.ResourceMagicId);
            Assert.NotNull(model.SequenceState?.DecodedPose);
            NativeBattleGraphicsResource resource = Assert.Single(source.Definition.NativeGraphics!.Bindings, binding => binding.ActorId == graphics.ActorIds[index]).Resource;
            if (model.ModelId4 >= 16)
            {
                resource = NativeBattleEnemyResourceView.Resolve(source.NativeModelCpuResources!, source.NativeFileStaging!, model.ActiveResourceRow100!.Value, resource);
            }
            models[index] = NativeBattleBodyBoneMatrices.Publish508C90(model, resource.PoseSource!);
            changed++;
        }
        Assert.True(changed > 0);
        BattleSnapshot migrated = source with { NativeGraphics = graphics with { Models = models.ToImmutable() } };
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(migrated with { NativeGraphics = graphics }));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(migrated));
        BattleSnapshot saved = NativeBattleEffectFamilyRunner.LoadSnapshot(output);
        BattleSession battle = BattleSession.Restore(saved);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(changed, saved.NativeGraphics!.Models.Count(model => model.CurrentBodyBoneMatrices is not null));
    }
}
