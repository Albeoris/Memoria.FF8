using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97ModelTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Original_model_retains_all_six_poses_and_projects_full_body_records_without_crossing_scratch_extent()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco97ModelResources();
        Assert.Same(resources, game.ReadNativeChoco97ModelResources());
        Assert.Equal(0x168354Cu, resources.Model.Address);
        Assert.Equal(0x4EE8, resources.Model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x688, 0x39FC, 0x4EDC, 0x4EE8 }, Enumerable.Range(0, 5).Select(resources.Model.Section).ToArray());
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(6, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).Length);
        NativeBattleRawMesh mesh = Assert.Single(resources.Model.Geometry);
        Assert.Equal(410, mesh.Vertices.Length);
        Assert.Equal(602, mesh.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(48, mesh.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(0x4220, resources.ModelTim.Data.Length);
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        int accepted = 0;
        for (short pose = 0; pose < 6; pose++)
        {
            NativeBattleEffectOwnedModelStep step = NativeBattleChoco97Model.Initialize(Task(), 0x257B018, resources);
            Assert.Equal((ushort)1, step.Task.ReadWord(0xDE));
            step = NativeBattleEffectOwnedModel.Select8DD190(step.Task.WriteDword(0x114, 0x1800).WriteDword(0x118, 0x1800).WriteDword(0x11C, 0x1800), step.State, resources.Model, pose);
            NativeBattleEffectOwnedModelDraw draw = Draw(step, Scratch(8), resources, rules);
            Assert.Equal(0xCD0, draw.ProjectedBytesWritten);
            Assert.Equal(0xCD8, draw.ProjectedScratch.Length);
            NativeBattleActionMatrix world = new([0x1800, 0, 0, 0, 0x1800, 0, 0, 0, 0x1800], 0, 0, 0, 4096);
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resources.Model.PoseSource, step.State.DecodedPose!, NativeBattlePoseMatrices.Compose56C2F0(View, world));
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                NativeBattleRawVertex vertex = mesh.Vertices[index];
                NativeBattleProjectedVertex projected = NativeBattleGeometryProjection.Project50F900(bones[vertex.Bone], vertex.Position, Projection, new(0, 0, 320, 216));
                Assert.Equal(projected.Screen, BinaryPrimitives.ReadUInt32LittleEndian(draw.ProjectedScratch.AsSpan().Slice(index * 8)));
                Assert.Equal(projected.DepthAndClip, BinaryPrimitives.ReadUInt32LittleEndian(draw.ProjectedScratch.AsSpan().Slice(index * 8 + 4)));
            }
            Assert.All(draw.ProjectedScratch.Skip(0xCD0), value => Assert.Equal((byte)0xA5, value));
            accepted += draw.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(step))!;
            Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(Draw(restored, Scratch(8), resources, rules)));
            Assert.Equal(JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(step.Task, step.State, resources.Model)), JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resources.Model)));
        }
        Assert.True(accepted > 0);
    }

    [Fact]
    public void Ten_model_phases_keep_child_completion_independent_and_restore_body_texture_order()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco97ModelResources();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = NativeBattleChoco97Model.Initialize(Task(), 0x257B018, resources);
        Saved current = new(initialized.Task, initialized.State, Scratch());
        HashSet<byte> phases = [];
        List<NativeBattleChoco97ModelOperation> operations = [];
        int callbacks = 0;
        while (current.Task.ReadByte(0x29) != 9 && callbacks++ < 2048)
        {
            phases.Add(current.Task.ReadByte(0x29));
            StepResult next = Step(current);
            Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(current))!;
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Step(restored)));
            operations.AddRange(next.Operations);
            Assert.False(next.Advance.ReadyToComplete);
            if (current.Task.ReadByte(0x29) == 8)
            {
                Assert.Null(next.Advance.Drawing);
                Assert.Empty(next.Advance.TextureCopies);
                Assert.Equal(current.Scratch.ToArray(), next.Next.Scratch.ToArray());
            }
            current = next.Next;
        }
        Assert.Equal((byte)9, current.Task.ReadByte(0x29));
        Assert.Equal(Enumerable.Range(0, 9).Select(index => (byte)index).ToArray(), phases.Order().ToArray());
        NativeBattleChoco97ModelAppendAuxiliary auxiliary = Assert.Single(operations.OfType<NativeBattleChoco97ModelAppendAuxiliary>());
        Assert.Equal((0x730C10u, 0x70, 0x2575458u), (auxiliary.Callback, auxiliary.TaskBytes, auxiliary.PoolAddress));
        Assert.Equal(new uint[] { 0x152B5C0, 0x152B5C4 }, operations.OfType<NativeBattleChoco97ModelSound>().Select(sound => sound.DescriptorAddress).ToArray());
        StepResult held = Step(current);
        Assert.False(held.Advance.ReadyToComplete);
        Assert.Null(held.Advance.Drawing);
        StepResult complete = Step(held.Next with { Task = held.Next.Task.WriteByte(0x28, 0) });
        Assert.True(complete.Advance.ReadyToComplete);
        Assert.Null(complete.Advance.Drawing);

        StepResult Step(Saved saved)
        {
            List<NativeBattleChoco97ModelOperation> emitted = [];
            NativeBattleChoco97ModelInputs inputs = new([10, 0, 20, 0, 30, 0, 40, 0], _ => true, _ => true, Execute, step => Draw(step, saved.Scratch, resources, rules));
            NativeBattleChoco97ModelAdvance advance = NativeBattleChoco97Model.Advance7306A0(saved.Task, saved.State, resources, inputs);
            return new(advance, new(advance.Task, advance.State, advance.Drawing?.ProjectedScratch ?? saved.Scratch), emitted.ToImmutableArray());

            NativeBattleEffectOwnedModelStep Execute(NativeBattleEffectOwnedModelStep step, NativeBattleChoco97ModelOperation operation)
            {
                emitted.Add(operation);
                return operation is NativeBattleChoco97ModelAppendAuxiliary ? step with { Task = step.Task.WriteByte(0x28, 1) } : step;
            }
        }
    }

    [Fact]
    public void Original_texture_program_uses_its_model_section_atlas_and_stops_without_rehydration()
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = game.ReadNativeChoco97ModelResources();
        NativeBattleEffectOwnedModelStep initialized = NativeBattleChoco97Model.Initialize(Task(), 0x257B018, resources);
        NativeBattleModulePoolTask task = initialized.Task.WriteDword(0x110, resources.TextureProgram.Address);
        List<NativeBattleTextureCopy> copies = [];
        for (int callback = 0; task.ReadDword(0x110) != 0 && callback < 512; callback++)
        {
            NativeBattleMoogle96TextureStep next = NativeBattleMoogle96TextureMotion.Advance739890(task, resources.TextureProgram, resources.TextureTable);
            NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(NativeBattleMoogle96TextureMotion.Advance739890(restored, resources.TextureProgram, resources.TextureTable)));
            copies.AddRange(next.Copies);
            task = next.Task;
        }
        Assert.Equal(0u, task.ReadDword(0x110));
        Assert.Equal(7, copies.Count);
        Assert.Equal(new NativeBattleTextureCopy(865, 279, 5, 10, 863, 346), copies[0]);
        Assert.Equal(new NativeBattleTextureCopy(891, 302, 5, 10, 863, 346), copies[1]);
        Assert.Empty(NativeBattleMoogle96TextureMotion.Advance739890(task, resources.TextureProgram, resources.TextureTable).Copies);
    }

    private static NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep step, ImmutableArray<byte> scratch, NativeBattleChoco97ModelResources resources, NativeBattleGraphicsRules rules) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(step.Task, step.State, resources.Model, View, Projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, scratch);
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x7306A0, new byte[0x140 - 12].ToImmutableArray());
    private static ImmutableArray<byte> Scratch(int guardBytes = 0) => Enumerable.Repeat((byte)0xA5, 0xCD0 + guardBytes).ToImmutableArray();
    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        return new(directory);
    }
    private sealed record Saved(NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState State, ImmutableArray<byte> Scratch);
    private sealed record StepResult(NativeBattleChoco97ModelAdvance Advance, Saved Next, ImmutableArray<NativeBattleChoco97ModelOperation> Operations);
}
