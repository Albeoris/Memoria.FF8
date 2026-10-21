using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePsychoBlast23ResourcesTests
{
    [Fact]
    public void Original_Psycho_Blast_resources_preserve_separate_ACT_sources_and_bounded_channels()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Psycho Blast.");
        using GameAssets game = new(directory!);
        NativeBattlePsychoBlast23Resources resources = game.ReadNativePsychoBlast23Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativePsychoBlast23Resources());
        Assert.Equal(832, resources.Animation.Data.Length);
        Assert.Equal(0x17EA4F0u, resources.Executable.ResourceAddress);
        Assert.Equal(15716, resources.Executable.Data.Length);
        Assert.Equal((ushort)23, resources.GenericNodes().Effect);
        Assert.Same(resources.Executable, resources.GenericNodes().Executable);
        Assert.Equal(0x6427Du, resources.Sound);
        Assert.Equal(0x161428Cu, resources.Sprite.Address);
        Assert.Equal(248, resources.Sprite.Bytes.Length);
        for (int frame = 0; frame < 9; frame++)
        {
            _ = resources.Sprite.ProjectedFrame571C80(frame);
        }
        CheckAct(resources.ActPrimary, 0x17EE254, 216, 32);
        CheckAct(resources.ActDelayed, 0x17F01B0, 88, 40);
        CheckAct(resources.ActFollowup, 0x17F1744, 216, 32);
        Assert.NotEqual(resources.ActPrimary.Address, resources.ActFollowup.Address);
        Assert.Same(resources.ActDelayed, resources.Act(0x17F01B0));
        Assert.Throws<NotSupportedException>(() => resources.Act(0));
        NativeBattlePsychoBlast23Resources restored = JsonSerializer.Deserialize<NativeBattlePsychoBlast23Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { ActDelayed = resources.ActPrimary }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Executable = resources.Executable with { Data = resources.Executable.Data.RemoveAt(resources.Executable.Data.Length - 1) } }).Validate());
    }

    private static void CheckAct(NativeBattleGfAnimationSource source, uint address, int workspaceBytes, int duration)
    {
        Assert.Equal(address, source.Address);
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[workspaceBytes]));
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        int emitted = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan());
                Assert.InRange(node.GeometryIndex, 0, count - 1);
                emitted++;
                return current;
            });
            workspace = step.Workspace;
            Assert.Equal(duration - frame - 1, step.Remaining);
        }
        Assert.True(emitted > 0);
        NativeBattleGfAnimationAdvance completed = workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("Completed ACT emitted another node."));
        Assert.Equal(0, completed.Remaining);
        Assert.Equal(0, completed.EmittedNodes);
    }
}
