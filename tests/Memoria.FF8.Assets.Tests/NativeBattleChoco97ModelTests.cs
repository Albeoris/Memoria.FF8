using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco97ModelTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_model_retains_all_six_poses_and_projects_full_body_records_without_crossing_scratch_extent(bool choco98)
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = Resources(game, choco98);
        Assert.Same(resources, Resources(game, choco98));
        Assert.Equal(choco98 ? 0x167482Cu : 0x168354Cu, resources.Model.Address);
        Assert.Equal(0x4EE8, resources.Model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x688, 0x39FC, 0x4EDC, 0x4EE8 }, Enumerable.Range(0, 5).Select(resources.Model.Section).ToArray());
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(6, NativeBattlePoseDecoder.ReadFrameCounts(resources.Model.PoseSource.Animations.AsSpan()).Length);
        NativeBattleRawMesh mesh = Assert.Single(resources.Model.Geometry);
        Assert.Equal(410, mesh.Vertices.Length);
        Assert.Equal(602, mesh.Polygons.Count(polygon => !polygon.Quad));
        Assert.Equal(48, mesh.Polygons.Count(polygon => polygon.Quad));
        Assert.Equal(0x4220, NativeBattleStorageSnapshots.Capture(resources.ModelTim).Data.Length);
        Assert.Equal(choco98 ? 0x152B580u : 0x152BB58u, resources.TextureProgram.Address);
        Assert.Equal(resources.Model.Address + 0x4EDCu, resources.TextureTable.Address);
        if (choco98)
        {
            NativeBattleChoco97ModelResources previous = game.ReadNativeChoco97ModelResources();
            Assert.Equal(previous.Model.Bytes.ToArray(), resources.Model.Bytes.ToArray());
            Assert.Equal(NativeBattleStorageSnapshots.Capture(previous.ModelTim).Data.ToArray(), NativeBattleStorageSnapshots.Capture(resources.ModelTim).Data.ToArray());
            Assert.Equal(NativeBattleStorageSnapshots.Capture(previous.TextureProgram).Data.ToArray(), NativeBattleStorageSnapshots.Capture(resources.TextureProgram).Data.ToArray());
            Assert.NotEqual(previous.Model.Address, resources.Model.Address);
        }
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        int accepted = 0;
        for (short pose = 0; pose < 6; pose++)
        {
            NativeBattleEffectOwnedModelStep step = Initialize(resources, choco98);
            Assert.Equal((ushort)1, step.Task.ReadWord(0xDE));
            step = NativeBattleEffectOwnedModel.Select8DD190(step.Task.WriteDword(0x114, 0x1800).WriteDword(0x118, 0x1800).WriteDword(0x11C, 0x1800), step.State, resources.Model, pose);
            NativeBattleEffectOwnedModelStep restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectOwnedModelStep>(NativeBattleSnapshotJson.Serialize(step))!;
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
            Assert.Equal(NativeBattleSnapshotJson.Serialize(draw), NativeBattleSnapshotJson.Serialize(Draw(restored, Scratch(8), resources, rules)));
            Assert.Equal(NativeBattleSnapshotJson.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(step.Task, step.State, resources.Model)), NativeBattleSnapshotJson.Serialize(NativeBattleEffectOwnedModel.AdvanceOnce8DD220(restored.Task, restored.State, resources.Model)));
        }
        Assert.True(accepted > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ten_model_phases_keep_child_completion_independent_and_restore_body_texture_order(bool choco98)
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = Resources(game, choco98);
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, choco98);
        Saved current = new(initialized.Task, initialized.State, Scratch());
        HashSet<byte> phases = [];
        List<NativeBattleChoco97ModelOperation> operations = [];
        int callbacks = 0;
        while (current.Task.ReadByte(0x29) != 9 && callbacks++ < 2048)
        {
            phases.Add(current.Task.ReadByte(0x29));
            Saved restored = NativeBattleSnapshotJson.Deserialize<Saved>(NativeBattleSnapshotJson.Serialize(current))!;
            StepResult next = Step(current);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(Step(restored)));
            operations.AddRange(next.Operations);
            Assert.False(next.Advance.ReadyToComplete);
            if (current.Task.ReadByte(0x29) == 8 && next.Advance.Task.ReadByte(0x29) == 9)
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
        Assert.Equal((choco98 ? 0x728780u : 0x730C10u, 0x70, choco98 ? 0x256E548u : 0x2575458u), (auxiliary.Callback, auxiliary.TaskBytes, auxiliary.PoolAddress));
        Assert.Equal(choco98 ? new uint[] { 0x152AF38 } : new uint[] { 0x152B5C0, 0x152B5C4 }, operations.OfType<NativeBattleChoco97ModelSound>().Select(sound => sound.DescriptorAddress).ToArray());
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
            NativeBattleChoco97ModelAdvance advance = Advance(saved.Task, saved.State, resources, inputs, choco98);
            return new(advance, new(advance.Task, advance.State, advance.Drawing?.ProjectedScratch ?? saved.Scratch), emitted.ToImmutableArray());

            NativeBattleEffectOwnedModelStep Execute(NativeBattleEffectOwnedModelStep step, NativeBattleChoco97ModelOperation operation)
            {
                emitted.Add(operation);
                return operation is NativeBattleChoco97ModelAppendAuxiliary ? step with { Task = step.Task.WriteByte(0x28, 1) } : step;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_texture_program_uses_its_model_section_atlas_and_stops_without_rehydration(bool choco98)
    {
        using GameAssets game = Installed();
        NativeBattleChoco97ModelResources resources = Resources(game, choco98);
        NativeBattleEffectOwnedModelStep initialized = Initialize(resources, choco98);
        NativeBattleModulePoolTask task = initialized.Task.WriteDword(0x110, resources.TextureProgram.Address);
        List<NativeBattleTextureCopy> copies = [];
        for (int callback = 0; task.ReadDword(0x110) != 0 && callback < 512; callback++)
        {
            NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
            NativeBattleMoogle96TextureStep next = NativeBattleMoogle96TextureMotion.Advance739890(task, resources.TextureProgram, resources.TextureTable);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleMoogle96TextureMotion.Advance739890(restored, resources.TextureProgram, resources.TextureTable)));
            copies.AddRange(next.Copies);
            task = next.Task;
        }
        Assert.Equal(0u, task.ReadDword(0x110));
        Assert.Equal(7, copies.Count);
        Assert.Equal(new NativeBattleTextureCopy(865, 279, 5, 10, 863, 346), copies[0]);
        Assert.Equal(new NativeBattleTextureCopy(891, 302, 5, 10, 863, 346), copies[1]);
        Assert.Empty(NativeBattleMoogle96TextureMotion.Advance739890(task, resources.TextureProgram, resources.TextureTable).Copies);
    }

    [Fact]
    public void Profile98_terminal_uses_signed_loop_counter_while97_keeps_its_external_mode_predicate()
    {
        using GameAssets game = Installed();
        NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
        foreach (bool choco98 in new[] { false, true })
        {
            NativeBattleChoco97ModelResources resources = Resources(game, choco98);
            NativeBattleEffectOwnedModelStep initialized = Initialize(resources, choco98);
            NativeBattleModulePoolTask scaled = initialized.Task.WriteDword(0x114, 0x1800).WriteDword(0x118, 0x1800).WriteDword(0x11C, 0x1800);
            NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(scaled, initialized.State, resources.Model, 5);
            int predicates = 0;
            NativeBattleChoco97ModelInputs inputs = new([], AtLeast, _ => throw new InvalidDataException("Terminal phase must not request a mode."), (_, _) => throw new InvalidDataException("Terminal phase must not append or emit sound."), step => Draw(step, Scratch(), resources, rules));
            NativeBattleChoco97ModelAdvance next = Advance(selected.Task.WriteByte(0x29, 8).WriteWord(0x136, 19), selected.State, resources, inputs, choco98);
            Assert.Equal((byte)(choco98 ? 9 : 8), next.Task.ReadByte(0x29));
            Assert.Equal(choco98 ? 0 : 1, predicates);
            Assert.Equal(choco98, next.Drawing is null);
            Assert.Equal(choco98, next.ReadyToComplete);
            if (choco98)
            {
                NativeBattleChoco97ModelAdvance beforeThreshold = Advance(selected.Task.WriteByte(0x29, 8).WriteWord(0x136, 18), selected.State, resources, inputs, true);
                Assert.Equal((byte)8, beforeThreshold.Task.ReadByte(0x29));
                Assert.NotNull(beforeThreshold.Drawing);
                NativeBattleChoco97ModelAdvance signedWrap = Advance(selected.Task.WriteByte(0x29, 8).WriteWord(0x136, 0x7FFF), selected.State, resources, inputs, true);
                Assert.Equal((ushort)0x8000, signedWrap.Task.ReadWord(0x136));
                Assert.Equal((byte)8, signedWrap.Task.ReadByte(0x29));
                Assert.NotNull(signedWrap.Drawing);
                Assert.Equal(0, predicates);
            }

            bool AtLeast(int mode)
            {
                Assert.False(choco98);
                Assert.Equal(6, mode);
                predicates++;
                return false;
            }
        }
    }

    private static NativeBattleEffectOwnedModelDraw Draw(NativeBattleEffectOwnedModelStep step, ImmutableArray<byte> scratch, NativeBattleChoco97ModelResources resources, NativeBattleGraphicsRules rules) => NativeBattleEffectOwnedModelDrawing.Draw7558A0(step.Task, step.State, resources.Model, View, Projection, rules.ShadowSource!, NativeBattlePrimitiveCapabilities.PrimaryRgba8(), rules.SummonColors!, scratch);
    private static NativeBattleChoco97ModelResources Resources(GameAssets game, bool choco98) => choco98 ? game.ReadNativeChoco98ModelResources() : game.ReadNativeChoco97ModelResources();
    private static NativeBattleEffectOwnedModelStep Initialize(NativeBattleChoco97ModelResources resources, bool choco98)
    {
        NativeBattleModulePoolTask task = new(1, 0, null, choco98 ? 0x728210u : 0x7306A0u, new byte[0x140 - 12].ToImmutableArray());
        return choco98 ? NativeBattleChoco98Model.Initialize(task, 0x2574D98, resources) : NativeBattleChoco97Model.Initialize(task, 0x257B018, resources);
    }
    private static NativeBattleChoco97ModelAdvance Advance(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleChoco97ModelResources resources, NativeBattleChoco97ModelInputs inputs, bool choco98) => choco98 ? NativeBattleChoco98Model.Advance728210(task, state, resources, inputs) : NativeBattleChoco97Model.Advance7306A0(task, state, resources, inputs);
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
