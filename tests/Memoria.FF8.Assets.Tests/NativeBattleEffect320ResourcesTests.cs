using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect320ResourcesTests
{
    [Fact]
    public void Selected_original_sources_keep_exact_cache_geometry_and_static_bounds()
    {
        using GameAssets game = Open();
        NativeBattleEffect320Resources resources = game.ReadNativeEffect320Resources();
        resources.Validate();
        Assert.Equal(17, resources.Geometry.Count);
        Assert.Equal(3, resources.Programmes.Count);
        Assert.Equal(new byte[] { 48, 48, 48, 48, 48 }, resources.ReadStatic(0xFE0FAC, 5));
        Assert.Equal(new byte[] { 49, 49, 48, 48, 48 }, resources.ReadStatic(0xFF12D4, 5));
        Assert.Equal(27, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(0xFF12A6, 2)));
        Assert.Equal(27, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(0xFF12AE, 2)));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xFE0FAC, 6));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x250539C, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(0xF54));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(0xFE0FAC));
        Assert.Equal(960, MorphLength);
    }

    [Fact]
    public void Original_programmes_emit_selected_geometry_and_bounded_nonendpoint_morphs()
    {
        using GameAssets game = Open();
        NativeBattleEffect320Resources resources = game.ReadNativeEffect320Resources();
        int[] durations = [47, 16, 53];
        int[] nodeCounts = [8, 2, 12];
        int largestMorph = 0;
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = resources.ReadProgramme(ProgrammeAddresses[index]);
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
            HashSet<int> selected = [];
            for (int frame = 0; frame < durations[index]; frame++)
            {
                NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
                {
                    selected.Add(node.GeometryIndex);
                    uint relative = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4));
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(source.Address + relative);
                    int shapes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(8, 4)));
                    Assert.InRange(node.Word40, 0, shapes - 1);
                    Assert.InRange(node.Word42, 0, shapes - 1);
                    if (node.Word40 != node.Word42 && node.Scalar38 is not 0 and not 4096)
                    {
                        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(4, 4)));
                        largestMorph = Math.Max(largestMorph, vertices * 8);
                    }
                    return current;
                });
                Assert.Equal(nodeCounts[index], advance.EmittedNodes);
                workspace = advance.Workspace;
            }
            Assert.Equal(index == 0 ? 5 : index == 1 ? 1 : 11, selected.Count);
            if (index != 0)
            {
                Assert.Equal(0, workspace.Advance701970(source, 0, (current, _) => current).EmittedNodes);
            }
        }
        Assert.Equal(MorphLength, largestMorph);
    }

    [Fact]
    public void Resource_JSON_preserves_original_camera_sprite_and_addressed_aliases()
    {
        using GameAssets game = Open();
        NativeBattleEffect320Resources original = game.ReadNativeEffect320Resources();
        NativeBattleEffect320Resources resources = JsonSerializer.Deserialize<NativeBattleEffect320Resources>(JsonSerializer.Serialize(original))!;
        resources.Validate();
        Assert.Equal(original.ReadStatic(0xFF12A0, 16), resources.ReadStatic(0xFF12A0, 16));
        Assert.True(original.MainTim.Data.AsSpan().SequenceEqual(resources.MainTim.Data.AsSpan()));
        for (int phase = 0; phase < 16; phase++)
        {
            Assert.Equal(20, resources.Sprite(SpriteAddress).ProjectedFrame571C80(phase).Quads.Length);
        }
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect320 resources.");
        return new(directory!);
    }
}
