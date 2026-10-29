using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect210ResourceTests
{
    [Fact]
    public void Original_ACTs_publish_all_frames_with_exact_workspace_and_geometry_domains()
    {
        using GameAssets game = Open();
        NativeBattleEffect210Resources resources = game.ReadNativeEffect210Resources();
        int outputs = 0;
        int maximumMorph = 0;
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ActAddresses[index]);
            Assert.Equal(ActLengths[index], source.Data.Length);
            int length = resources.WorkspaceLength(source.Address);
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 0x258).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, length);
            int directory = checked((int)Dword(source.Data, 4));
            int frames = checked((int)Dword(source.Data, directory + 4));
            for (int frame = 0; frame < frames; frame++)
            {
                NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                {
                    Assert.InRange(node.GeometryIndex, (ushort)0, checked((ushort)(ActGeometryCounts[index] - 1)));
                    uint address = source.Address + Dword(source.Data, 8 + node.GeometryIndex * 4);
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(address);
                    int shapes = checked((int)Dword(geometry.Bytes, 8));
                    Assert.InRange(node.Word40, (short)0, checked((short)(shapes - 1)));
                    Assert.InRange(node.Word42, (short)0, checked((short)(shapes - 1)));
                    if (node.Word40 != node.Word42 && node.Scalar38 is > 0 and < 4096)
                    {
                        maximumMorph = Math.Max(maximumMorph, checked((int)Dword(geometry.Bytes, 4) * 8));
                    }
                    outputs++;
                    return current;
                });
                Assert.Equal(frames - frame - 1, next.Remaining);
                workspace = next.Workspace;
            }
            Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan().Slice(length).ToArray(), value => Assert.Equal((byte)0xA5, value));
        }
        Assert.Equal(1261, outputs);
        Assert.Equal(1704, maximumMorph);
    }

    [Fact]
    public void Literal_sources_streamed_files_and_JSON_keep_current_inputs_distinct()
    {
        using GameAssets game = Open();
        NativeBattleEffect210Resources resources = game.ReadNativeEffect210Resources();
        resources.Validate();
        Assert.Equal(TimLength, resources.EmbeddedTim.Data.Length);
        Assert.Equal(9u, Dword(resources.EmbeddedTim.Data, 4));
        Assert.Equal(4, resources.Meshes.Count);
        Assert.Equal(4, resources.Sprites.Count);
        Assert.Equal(4, resources.Files.Count);
        Assert.NotEmpty(resources.ReadFile(704).Data);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(BaseAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MainSelectorsAddress, 8));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(703));
        NativeBattleEffect210Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect210Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadFile(704).Data.ToArray(), restored.ReadFile(704).Data.ToArray());
        Assert.Equal(resources.ReadStatic(MainSelectorsAddress, 7), restored.ReadStatic(MainSelectorsAddress, 7));
    }

    [Fact]
    public void Original_camera_tracks_retire_through_the_existing_consumer()
    {
        using GameAssets game = Open();
        NativeBattleEffect210Resources resources = game.ReadNativeEffect210Resources();
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 4000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_RET_initializer_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect210Resources resources = game.ReadNativeEffect210Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([210])[209];
        Assert.Same(resources, binding.Effect210Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect210 resources.");
        return new(directory!);
    }
}
