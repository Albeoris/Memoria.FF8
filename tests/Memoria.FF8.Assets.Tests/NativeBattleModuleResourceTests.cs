using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModuleResourceTests
{
    [Fact]
    public void OriginalModuleDirectoryPreservesAllBindingsAndEjectionFrameTerminator()
    {
        using GameAssets assets = new(GameDirectory());
        ImmutableArray<NativeBattleModuleResource> resources = assets.ReadNativeBattleModuleResources();
        Assert.Equal(42, resources.Length);
        Assert.Equal(resources[0].Address, resources[1].Address);
        Assert.Equal(0x570CC0u, resources[21].WrapperCallback);
        Assert.Equal(0x570BC0u, resources[21].Callback);
        Assert.True(resources[21].SpriteFrame(7).NextOffset > 0);
        Assert.Equal((short)-1, resources[21].SpriteFrame(8).NextOffset);
        for (int id = 38; id <= 40; id++)
        {
            Assert.Equal((byte)3, resources[id].Kind);
            Assert.Equal(0x5705A0u, resources[id].Callback);
        }
    }

    [Fact]
    public void DefensiveWrapperAndSeedAppendTheirChildrenDuringTheSameSchedulerPassAndConsumeOneCrtDraw()
    {
        using GameAssets assets = new(GameDirectory());
        ImmutableArray<NativeBattleModuleResource> resources = assets.ReadNativeBattleModuleResources();
        NativeCrtRandomState before = new(123);
        NativeCrtRandomState after = before.Draw(out ushort orientation);
        NativeBattleModuleInputs inputs = Inputs(resources, before);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelResource(3, resources[39], 0x100027);
        NativeBattleModuleStep first = state.Advance(inputs);
        Assert.Equal(after, first.CrtRandomAfter);
        Assert.Equal([NativeBattleModuleDrawKind.Sound, NativeBattleModuleDrawKind.Defensive], first.Draws.Select(draw => draw.Kind).ToArray());
        NativeBattleModuleDraw draw = first.Draws[1];
        Assert.Equal((byte)1, draw.Variant);
        Assert.Equal((ushort)0, draw.Phase);
        Assert.Equal((short)(orientation % 4096), draw.Orientation);
        Assert.Equal(5120, draw.Scale);
        Assert.Equal(new(10, 20, 30, 40), draw.Anchor);
        Assert.Equal((byte)1, first.State.PublishedCount);
        state = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(first.State))!;
        inputs = inputs with { CrtRandom = after };
        NativeBattleModuleStep paused = state.Advance(inputs with { GraphicsFlags = 1 });
        Assert.Equal((ushort)1, Assert.Single(paused.Draws).Phase);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(paused.State));
        for (int phase = 1; phase < 8; phase++)
        {
            NativeBattleModuleStep next = state.Advance(inputs);
            Assert.Equal((ushort)phase, Assert.Single(next.Draws).Phase);
            Assert.Equal(after, next.CrtRandomAfter);
            state = next.State;
        }
        Assert.Null(state.Head);
        Assert.Equal((byte)0, state.PublishedCount);
    }

    [Fact]
    public void EjectionUsesItsActualSpriteDirectoryAndPausePreservesItsFrameCursor()
    {
        using GameAssets assets = new(GameDirectory());
        ImmutableArray<NativeBattleModuleResource> resources = assets.ReadNativeBattleModuleResources();
        NativeBattleModuleInputs inputs = Inputs(resources, new(7));
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelResource(0, resources[21], 0x200015);
        for (int phase = 0; phase < 9; phase++)
        {
            NativeBattleModuleStep next = state.Advance(inputs);
            NativeBattleModuleDraw draw = Assert.Single(next.Draws);
            Assert.Equal(NativeBattleModuleDrawKind.Sprite, draw.Kind);
            Assert.Equal((ushort)phase, draw.Phase);
            Assert.Equal(8192, draw.Scale);
            Assert.Equal(-40, draw.DepthOffset);
            Assert.Equal(resources[21].SpriteFrame(phase).NextOffset, draw.SpriteFrame!.NextOffset);
            Assert.Equal(resources[21].SpriteFrame(phase).Quads.ToArray(), draw.SpriteFrame.Quads.ToArray());
            state = next.State;
            if (phase == 0)
            {
                NativeBattleModuleStep paused = state.Advance(inputs with { GraphicsFlags = 1 });
                Assert.Equal((ushort)1, Assert.Single(paused.Draws).Phase);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(paused.State));
                state = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(paused.State))!;
            }
        }
        Assert.Null(state.Head);
        Assert.Equal((byte)0, state.PublishedCount);
    }

    [Fact]
    public void PackedPaletteOverridePublishesItsOriginal571C80FlagsAndFourByteRegisters()
    {
        using GameAssets assets = new(GameDirectory());
        ImmutableArray<NativeBattleModuleResource> resources = assets.ReadNativeBattleModuleResources();
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelResource(0, resources[21], 0x84200015);
        NativeBattleModuleDraw draw = Assert.Single(state.Advance(Inputs(resources, new(7))).Draws);
        Assert.Equal((ushort)0x13, draw.SpriteFlags);
        Assert.Equal(new byte[] { 4, 0, 0, 0 }, draw.SpritePaletteOffsets.ToArray());
    }

    private static NativeBattleModuleInputs Inputs(ImmutableArray<NativeBattleModuleResource> resources, NativeCrtRandomState crt)
    {
        return new(0) { Resources = resources, CrtRandom = crt, ReadModelAnchor = _ => new(10, 20, 30, 40), ReadModelRotation14 = _ => 123 };
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return directory;
    }
}
