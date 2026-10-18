using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMoogle96TextureMotionTests
{
    [Fact]
    public void OriginalThreeScriptsUseModelByteDirectoryAndContinueExactlyThroughStopOrLoop()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using GameAssets game = new(directory);
        NativeBattleMoogle96Resources resources = game.ReadNativeMoogle96Resources();
        int section = resources.Model.Section(3);
        NativeBattleMoogle96TextureTable table = new(checked(resources.Model.Address + (uint)section), resources.Model.Bytes.AsSpan().Slice(section).ToArray().ToImmutableArray());
        Assert.Equal(0x1532C4Cu, table.Address);
        Assert.Equal(24, table.Data.Length);
        Assert.Equal(3, resources.TextureSequences.Length);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x140, 1)]);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(0, 1, 0x140);
        NativeBattleModulePoolTask initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(append.Hierarchy.Read(append.Address), append.Address, resources.Model, 3, resources.DefaultColor44).Task;
        uint[] addresses = [0x1532FC8, 0x1532FD8, 0x1532FE8];
        bool anyLoop = false;
        bool anyStop = false;
        for (int script = 0; script < 3; script++)
        {
            NativeBattleMoogle96TextureProgram program = new(addresses[script], resources.TextureSequences[script].Data);
            NativeBattleModulePoolTask task = initialized.WriteDword(0x110, program.Address).WriteWord(0x12C, 0).WriteWord(0x12E, script == 0 ? (ushort)1 : (ushort)0).WriteWord(0x130, 0).WriteWord(0x132, 0);
            int requests = 0;
            bool loop = false;
            for (int callback = 0; callback < 128 && task.ReadDword(0x110) != 0; callback++)
            {
                NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
                ushort prior = task.ReadWord(0x12C);
                NativeBattleMoogle96TextureStep step = NativeBattleMoogle96TextureMotion.Advance739890(task, program, table);
                Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(NativeBattleMoogle96TextureMotion.Advance739890(restored, program, table)));
                NativeBattleEffectTextureState queue = NativeBattleEffectTextureState.FromColdProcess();
                foreach (NativeBattleTextureCopy copy in step.Copies)
                {
                    queue = queue.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
                    Assert.Equal(new NativeBattleEffectVramCopy(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY), queue.Slots[queue.Count - 1]!.Copy);
                    Assert.InRange(copy.SourceX, (short)832, (short)1087);
                    Assert.InRange(copy.SourceY, (short)384, (short)639);
                }
                Assert.Equal(step.Copies.Length, queue.Count);
                requests += step.Copies.Length;
                loop |= prior != 0 && step.Task.ReadWord(0x12C) == 0;
                task = step.Task;
            }
            Assert.True(requests > 0);
            Assert.True(loop || task.ReadDword(0x110) == 0, "The original script did not stop or exercise its loop inside the diagnostic callback budget.");
            anyLoop |= loop;
            anyStop |= task.ReadDword(0x110) == 0;
        }
        Assert.True(anyLoop);
        Assert.True(anyStop);
    }
}
