using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa86ResourcesTests
{
    [Theory]
    [InlineData(0, 56)]
    [InlineData(1, 416)]
    [InlineData(2, 932)]
    public void Original_body_programs_execute_all_reached_nodes_shapes_and_packets_with_independent_next_restore(int program, int clearBytes)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRinoa86Resources resources = assets.ReadNativeRinoa86Resources();
        NativeBattleGfAnimationSource source = resources.Programs[program];
        NativeBattleSummonColors colors = assets.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 18000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 956).ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, clearBytes);
        ImmutableArray<byte> morph = Enumerable.Repeat((byte)0xA5, 640).ToImmutableArray();
        int nodes = 0;
        int packets = 0;
        int remaining = -1;
        for (int call = 0; call < 256 && remaining != 0; call++)
        {
            NativeBattleGfAnimationWorkspace clone = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            ImmutableArray<byte> before = morph;
            List<NativeBattleGfGeometryBatch> first = [];
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa86NodeDraw drawn = NativeBattleRinoa86ProgramDrawing.Draw7838C0(source, new(view, node, (short)program, 16, -256, 0x25BBF60), morph, projection, colors);
                morph = drawn.MorphScratch;
                first.Add(drawn.Packets);
                nodes++;
                packets += drawn.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
                return current;
            });
            List<NativeBattleGfGeometryBatch> second = [];
            ImmutableArray<byte> restoredMorph = before;
            NativeBattleGfAnimationAdvance restored = clone.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleRinoa86NodeDraw drawn = NativeBattleRinoa86ProgramDrawing.Draw7838C0(source, new(view, node, (short)program, 16, -256, 0x25BBF60), restoredMorph, projection, colors);
                restoredMorph = drawn.MorphScratch;
                second.Add(drawn.Packets);
                return current;
            });
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored));
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(morph.ToArray(), restoredMorph.ToArray());
            workspace = next.Workspace;
            remaining = next.Remaining;
        }
        Assert.Equal(0, remaining);
        Assert.True(nodes > 0);
        Assert.True(packets > 0);
        for (int vertex = 0; vertex < 80; vertex++)
        {
            Assert.Equal((byte)0xA5, morph[vertex * 8 + 6]);
            Assert.Equal((byte)0xA5, morph[vertex * 8 + 7]);
        }
    }

    [Fact]
    public void Original_Rinoa86_binds_its_owned_rig_two_TIM_files_and_caster_override_without_scene_renderer_substitution()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleRinoa86Resources resources = assets.ReadNativeRinoa86Resources();
        Assert.Equal(0x220, resources.Animation.Data.Length);
        Assert.Equal(64, resources.Io.Script.Data.Length);
        Assert.Equal(new short[] { 405, 406 }, resources.Io.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG085_B.1T0", "MAG085_B.1T1" }, resources.Io.Files.Select(file => file.Name));
        Assert.All(resources.Io.Files, file => Assert.Equal(33312, file.Data.Length));
        Assert.Equal(34, resources.Model.PoseSource.BoneCount);
        Assert.Equal(0x4934, resources.Model.Bytes.Length);
        Assert.Equal(new[] { 0x18, 0x688, 0x3344, 0x4928, 0x4934 }, Enumerable.Range(0, 5).Select(index => resources.Model.Section(index)));
        Assert.Equal(0x1D60, resources.ActorOverride.Data.Length);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25BBEE8, 0x25BBD88, 352, 1);
        (pool, int slot) = pool.Append(0x77FF90);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(pool.Slots[slot], 0x25BBD88, resources.Model, 3, resources.DefaultColor44);
        NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(initialized.Task, initialized.State, resources.Model, 0);
        Assert.NotNull(selected.State.DecodedPose);
        NativeBattleEffectOwnedModelStep restored = JsonSerializer.Deserialize<NativeBattleEffectOwnedModelStep>(JsonSerializer.Serialize(selected))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(selected.Task, selected.State, resources.Model)), JsonSerializer.Serialize(NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(restored.Task, restored.State, resources.Model)));
        NativeBattleRinoa86IoState io = NativeBattleRinoa86Io.ReleaseWait77FDB0(NativeBattleRinoa86Io.Initialize77F680(resources.Io.Script.Data, 0x20DFAB8 + 0x20000)).State;
        NativeBattleModulePoolTask task = new(1, 0, null, 0x77F780, new byte[36].ToImmutableArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(86);
        for (int index = 0; index < 2; index++)
        {
            NativeBattleRinoa86IoAdvance begun = NativeBattleRinoa86Io.Advance77F780(task, io);
            NativeBattleRinoa86IoQueued queued = NativeBattleRinoa86Io.CopyAndQueueFile(begun.State, memory, NativeBattleFileIoState.FromColdProcess(), resources.Io.Files[index]);
            NativeBattleRinoa86IoCompletion complete = NativeBattleRinoa86Io.Complete77F980(begun.State, queued.Memory, 0x77F980);
            Assert.Equal((ushort)0, complete.State.Busy);
            memory = complete.Memory;
            NativeBattleRinoa86IoAdvance polled = NativeBattleRinoa86Io.Advance77F780(begun.Task, complete.State);
            io = polled.State;
            task = polled.Task;
        }
        Assert.True(NativeBattleRinoa86Io.Advance77F780(task, io).ReadyToComplete);
        Assert.Equal(2, memory.Textures!.Count);
    }
}
