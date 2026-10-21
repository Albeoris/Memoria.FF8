using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect60ResourcesTests
{
    [Fact]
    public void Original_effect60_resources_keep_independent_addressed_banks_and_ACT_sources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check effect60.");
        using GameAssets game = new(directory!);
        NativeBattleEffect60Resources resources = game.ReadNativeEffect60Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect60Resources());
        Assert.Equal(788, resources.Animation.Data.Length);
        Assert.Equal(0x64280u, resources.Sound);
        Assert.Equal(new short[] { 0, 2407, 3476, 3706, 3883, 4007, 4077, 4096 }, resources.TargetMotionRatios);
        NativeBattleCuragaResources first = resources.GenericNodes(0x1797CC0);
        NativeBattleCuragaResources second = resources.GenericNodes(0x179C998);
        Assert.Same(resources.PrimaryNodes, first.Executable);
        Assert.Same(resources.SecondaryNodes, second.Executable);
        Assert.Equal((ushort)60, first.Effect);
        Assert.Equal((ushort)60, second.Effect);
        Assert.Equal(19672, first.Executable!.Data.Length);
        Assert.Equal(17260, second.Executable!.Data.Length);
        NativeBattleCuragaExecutableState primary = new(0, null) { ResourceAddress = 0x1797CC0 };
        NativeBattleCuragaExecutableState secondary = new(0, null) { ResourceAddress = 0x179C998 };
        primary = primary.Initialize87EA20(resources.PrimaryNodes);
        Assert.Equal(1u, primary.InitializationFlag);
        Assert.Equal(0u, secondary.InitializationFlag);
        Assert.Null(secondary.Data);
        secondary = secondary.Initialize87EA20(resources.SecondaryNodes);
        Assert.Equal(1u, secondary.InitializationFlag);
        Assert.Equal(0x1797CC0u, primary.ResourceAddress);
        Assert.Equal(0x179C998u, secondary.ResourceAddress);
        Assert.Throws<InvalidDataException>(() => primary.Initialize87EA20(resources.SecondaryNodes));
        Assert.Throws<NotSupportedException>(() => resources.GenericNodes(0x1797CC4));
        CheckAct(resources.ActPrimary, 0x17A0D04, 228, 32);
        CheckAct(resources.ActSecondary, 0x17A3150, 36, 24);
        Assert.Same(resources.ActPrimary, resources.Act(0x17A0D04));
        Assert.Throws<NotSupportedException>(() => resources.Act(0));
        Assert.Throws<InvalidDataException>(() => (resources with { SecondaryNodes = resources.PrimaryNodes }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ActSecondary = resources.ActPrimary }).Validate());
        NativeBattleEffect60Resources restored = JsonSerializer.Deserialize<NativeBattleEffect60Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([60])[59];
        Assert.Equal(0x7EB090u, binding.Entry);
        Assert.Equal("mag059.tim", binding.InitializerAsset!.Name);
        Assert.Equal(166432, binding.InitializerAsset.Data.Length);
        Assert.NotNull(binding.Effect60Resources);
    }

    private static void CheckAct(NativeBattleGfAnimationSource source, uint address, int workspaceBytes, int duration)
    {
        Assert.Equal(address, source.Address);
        NativeBattleGfAnimationWorkspace workspace = new(ImmutableArray.CreateRange(new byte[workspaceBytes]));
        workspace = workspace.Initialize7016B0(source, workspaceBytes);
        int emitted = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, _) =>
            {
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
