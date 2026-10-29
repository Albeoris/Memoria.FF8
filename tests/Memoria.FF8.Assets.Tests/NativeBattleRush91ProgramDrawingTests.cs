using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRush91ProgramDrawingTests
{
    [Fact]
    public void Original_Rush_private_program_draws_all_shapes_and_restores_task_plus_shared_morph_scratch()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGfAnimationSource source = game.ReadNativeRush91Resources().ProgramSource;
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x761B60, Enumerable.Repeat((byte)0xA5, 0x108 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, 0x70).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteDword(0x104, 0x12345678).WriteWord(0x60, 0).WriteWord(0x62, 0).WriteWord(0x64, 0).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 0).WriteWord(0x86, 0xFF00).WriteWord(0x88, 0);
        task = NativeBattleRush91ProgramDrawing.InitializeWorkspace761B60(task, source);
        Assert.Equal(0x12345678u, task.ReadDword(0x104));
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x5A0).ToImmutableArray();
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int packets = 0;
        bool changedScratch = false;
        bool consumedDepth88 = false;
        for (int frame = 0; frame < 48; frame++)
        {
            string taskCheckpoint = NativeBattleSnapshotJson.Serialize(task);
            NativeBattleRush91ProgramDraw next = NativeBattleRush91ProgramDrawing.Advance761BB0(task, source, scratch, view, projection, colors);
            Assert.Equal(47 - frame, next.Remaining);
            Assert.Equal((uint)(frame + 1), next.Task.ReadDword(0x98));
            Assert.Equal(0x12345678u, next.Task.ReadDword(0x104));
            NativeBattleRush91ProgramDraw unused86 = NativeBattleRush91ProgramDrawing.Advance761BB0(NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(taskCheckpoint).WriteWord(0x86, 0), source, scratch, view, projection, colors);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(next.Packets), NativeBattleSnapshotJson.Serialize(unused86.Packets));
            NativeBattleRush91ProgramDraw biased88 = NativeBattleRush91ProgramDrawing.Advance761BB0(NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(taskCheckpoint).WriteWord(0x88, 0xFF00), source, scratch, view, projection, colors);
            consumedDepth88 |= !next.Packets.Attempts.Select(item => item.Primitive?.Bucket).SequenceEqual(biased88.Packets.Attempts.Select(item => item.Primitive?.Bucket));
            if (frame == 23)
            {
                NativeBattleModulePoolTask coldTask = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(taskCheckpoint)!;
                ImmutableArray<byte> coldScratch = NativeBattleSnapshotJson.Deserialize<ImmutableArray<byte>>(NativeBattleSnapshotJson.Serialize(scratch));
                Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(NativeBattleRush91ProgramDrawing.Advance761BB0(coldTask, source, coldScratch, view, projection, colors)));
            }
            packets += next.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            changedScratch |= !scratch.AsSpan().SequenceEqual(next.MorphScratch.AsSpan());
            for (int vertex = 0; vertex < 180; vertex++)
            {
                Assert.Equal((byte)0xA5, next.MorphScratch[vertex * 8 + 6]);
                Assert.Equal((byte)0xA5, next.MorphScratch[vertex * 8 + 7]);
            }
            task = next.Task;
            scratch = next.MorphScratch;
        }
        Assert.True(packets > 0);
        Assert.True(changedScratch);
        Assert.True(consumedDepth88);
    }
}
