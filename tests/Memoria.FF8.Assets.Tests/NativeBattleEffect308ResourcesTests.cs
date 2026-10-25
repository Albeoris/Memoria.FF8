using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect308ResourcesTests
{
    [Fact]
    public void Original_scripts_directories_and_normalization_have_exact_selected_bounds()
    {
        using GameAssets game = Open();
        NativeBattleEffect308Resources resources = game.ReadNativeEffect308Resources();
        resources.Validate();
        Assert.Equal(4, resources.Programmes.Count);
        Assert.Equal(8, resources.Geometry.Count);
        Assert.Equal(new byte[] { 0, 1, 3 }, resources.SelectedSpritePointers.Keys.Order().ToArray());
        Assert.Equal((ushort)0xFF38, BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(NormalizationAddress, 2)));
        Assert.Equal(384, resources.ReadStatic(NormalizationNonzeroAddress, 384).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NormalizationAddress + 2, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NormalizationNonzeroAddress, 386));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(2));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x250754C, 3));
        foreach ((uint address, int length) in ScriptRanges)
        {
            Assert.Equal(length, resources.ReadScript(address).Length);
        }
        foreach (uint address in ScriptCounterAddresses)
        {
            ushort initial = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(address, 2));
            ushort reset = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(address + 2, 2));
            Assert.Equal(initial, reset);
        }
    }

    [Fact]
    public void Original_ACT_workspaces_emit_only_bounded_shapes_and_require544_morph_bytes()
    {
        using GameAssets game = Open();
        NativeBattleEffect308Resources resources = game.ReadNativeEffect308Resources();
        int[] frames = [24, 22, 18, 18];
        int[] nodes = [5, 3, 4, 4];
        int largestMorph = 0;
        for (int selector = 0; selector < 4; selector++)
        {
            NativeBattleGfAnimationSource source = resources.ReadProgramme(selector);
            Assert.Same(source, resources.Act(source.Address));
            int length = resources.WorkspaceLengths[selector];
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[length].ToImmutableArray()).Initialize7016B0(source, length);
            for (int frame = 0; frame < frames[selector]; frame++)
            {
                NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
                {
                    uint offset = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4, 4));
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(source.Address + offset);
                    int shapes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(8, 4)));
                    Assert.InRange((int)node.Word40, 0, shapes - 1);
                    Assert.InRange((int)node.Word42, 0, shapes - 1);
                    if (node.Word40 != node.Word42 && node.Scalar38 is not 0 and not 4096)
                    {
                        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(4, 4)));
                        largestMorph = Math.Max(largestMorph, vertices * 8);
                    }
                    return current;
                });
                Assert.Equal(nodes[selector], advance.EmittedNodes);
                workspace = advance.Workspace;
            }
            Assert.Equal(0, workspace.Advance701970(source, 0, (current, _) => current).EmittedNodes);
        }
        Assert.Equal(MorphLength, largestMorph);
        Assert.Equal(51992, PrivatePacketUpperBound);
    }

    [Fact]
    public void JSON_preserves_partial_smoke_front_and_canonical_completion_files()
    {
        using GameAssets game = Open();
        NativeBattleEffect308Resources original = game.ReadNativeEffect308Resources();
        NativeBattleEffect308Resources resources = JsonSerializer.Deserialize<NativeBattleEffect308Resources>(JsonSerializer.Serialize(original))!;
        resources.Validate();
        Assert.Equal(328, resources.ReadSmoke(0x1026038).Bytes.Length);
        Assert.Equal(364, resources.ReadSmoke(0x1025D20).Bytes.Length);
        Assert.Throws<InvalidDataException>(() => resources.ReadSmoke(0x1026038).ProjectedFrame571C80(12));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1026038, 352));
        Assert.True(original.MainTim.Data.AsSpan().SequenceEqual(resources.MainTim.Data.AsSpan()));
        Assert.Equal("MA8DEF_0.TIM", resources.Files[360].Name);
        Assert.Equal("MA8DEF_1.TIM", resources.Files[361].Name);
        Assert.All(resources.Files.Values, file => Assert.Equal(33824, file.Data.Length));
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            _ = bank.TrackOffset(selector);
        }
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect308 resources.");
        return new(directory!);
    }
}
