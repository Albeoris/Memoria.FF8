using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleChoco99SecondaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_secondary_meshes_draw_terminal_callback_and_keep_lifetime_out_of_depth(bool timed)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleChoco99SecondaryResources resources = game.ReadNativeChoco99SecondaryResources();
        NativeBattleStaticMeshResource mesh = timed ? resources.File481 : resources.File480;
        NativeBattleEffectFileAsset file = Assert.Single(game.ReadNativeChoco99IoResources().Files, file => file.FileId == (timed ? 481 : 480));
        Assert.Equal(NativeBattleStorageSnapshots.Capture(file).Data.ToArray(), mesh.Bytes.ToArray());
        Assert.Equal(timed ? 0x19DE510u : 0x19DD2D0u, mesh.Address);
        Assert.Equal(timed ? 0xE70 : 0x1240, mesh.Bytes.Length);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, timed ? 0x721390u : 0x7211D0u, new byte[0x58 - 12].ToImmutableArray()).WriteWord(0x1C, unchecked((ushort)-200)).WriteWord(0x20, 300).WriteWord(0x22, 0x4321).WriteDword(0x40, 0x00112233).WriteWord(0x50, 123).WriteWord(0x52, 0x1234).WriteWord(0x54, 0x5678);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 24000, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        int accepted = 0;
        int completeCall = -1;
        for (int call = 0; call < 20; call++)
        {
            NativeBattleModulePoolTask restored = NativeBattleSnapshotJson.Deserialize<NativeBattleModulePoolTask>(NativeBattleSnapshotJson.Serialize(task))!;
            NativeBattleChoco99SecondaryStep step = Step(task);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step), NativeBattleSnapshotJson.Serialize(Step(restored)));
            Assert.Equal((ushort)0x4321, step.Task.ReadWord(0x22));
            Assert.Equal((ushort)0x1234, step.Task.ReadWord(0x52));
            Assert.Equal(0x00112233u, step.Task.ReadDword(0x40));
            Assert.Equal((ushort)123, step.Task.ReadWord(0x50));
            if (call == 0)
            {
                Assert.Equal(timed ? (ushort)0 : (ushort)5120, step.Task.ReadWord(0x1E));
                Assert.Equal(timed ? (ushort)300 : (ushort)8192, step.Task.ReadWord(0x20));
                Assert.Equal(timed ? (ushort)16 : (ushort)0x5678, step.Task.ReadWord(0x54));
            }
            NativeBattleGfGeometryBatch changedLifetime = NativeBattleChoco99Secondary.Draw721230(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(step.Task)).WriteWord(0x52, 0xFFFF).WriteWord(0x54, 0x8000), mesh, view, projection, colors);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(step.Packets), NativeBattleSnapshotJson.Serialize(changedLifetime));
            Assert.Equal(timed ? 108 : 160, step.Packets.Attempts.Length);
            accepted += step.Packets.Attempts.Count(attempt => attempt.Primitive is not null);
            task = step.Task;
            if (step.ReadyToComplete)
            {
                completeCall = call;
                Assert.True((task.ReadByte(0x26) & 1) != 0);
                Assert.Equal((byte)2, task.ReadByte(0x29));
                Assert.NotEmpty(step.Packets.Attempts);
                Assert.False(Step(task.WriteByte(0x28, 1)).ReadyToComplete);
                Assert.Empty(NativeBattleChoco99Secondary.Draw721230(task.WriteByte(0x26, 5), mesh, view, projection, colors).Attempts);
                break;
            }
        }
        Assert.True(accepted > 0);
        Assert.Equal(timed ? 16 : 1, completeCall);
        Assert.Throws<InvalidDataException>(() => timed ? NativeBattleChoco99Secondary.Advance721390(task, resources.File480, view, projection, colors) : NativeBattleChoco99Secondary.Advance7211D0(task, resources.File481, 10, view, projection, colors));

        NativeBattleChoco99SecondaryStep Step(NativeBattleModulePoolTask current) => timed ? NativeBattleChoco99Secondary.Advance721390(current, mesh, view, projection, colors) : NativeBattleChoco99Secondary.Advance7211D0(current, mesh, 10, view, projection, colors);
    }
}
