using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCurse47ResourcesTests
{
    [Fact]
    public void Original_resources_keep_cold_script_three_TIM_uploads_and_their_single_immutable_owner_after_JSON()
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        Assert.Same(resources, game.ReadNativeCurse47Resources());
        resources.Validate();
        Assert.Equal("mag046.tim", resources.MainTim.Name);
        Assert.Equal(168480, resources.MainTim.Data.Length);
        Assert.Equal(512, resources.Animation.Data.Length);
        Assert.Equal(0x64221u, resources.Sound);
        Assert.Equal(NativeBattleCurse47State.SharedAddress, resources.ColdSharedPointer);
        Assert.Equal(new short[] { 388, 389, 390 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { 33312, 33312, 32832 }, resources.Files.Select(file => file.Data.Length));
        Assert.Equal(new uint[] { 0xC0C0C0, 0x808080, 0x404040, 0xFF000000 }, resources.Overlay.Commands);
        Assert.Equal(9u, Dword(resources.Files[0].Data, 4));
        Assert.Equal(9u, Dword(resources.Files[1].Data, 4));
        Assert.Equal(8u, Dword(resources.Files[2].Data, 4));
        Assert.Equal(new ushort[] { 320, 249, 256, 1 }, Block(resources.Files[0].Data, 8));
        Assert.Equal(new ushort[] { 704, 256, 64, 256 }, Block(resources.Files[0].Data, 532));
        Assert.Equal(new ushort[] { 768, 256, 64, 256 }, Block(resources.Files[1].Data, 532));
        Assert.Equal(new ushort[] { 320, 250, 16, 1 }, Block(resources.Files[2].Data, 8));
        Assert.Equal(new ushort[] { 832, 256, 64, 256 }, Block(resources.Files[2].Data, 52));
        for (int row = 0; row < 5; row++)
        {
            Assert.Equal(0u, Dword(resources.InitialIoScript.Data, row * 16 + 12));
        }
        NativeBattleCurse47Resources restored = JsonSerializer.Deserialize<NativeBattleCurse47Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        NativeBattleCurse47State warm = NativeBattleCurse47State.FromColdProcess(resources.InitialIoScript.Data);
        Assert.Equal(resources.InitialIoScript.Data.ToArray(), warm.IoScript!.Value.ToArray());
        Assert.Equal(0u, Dword(resources.InitialIoScript.Data, 28));
    }

    [Fact]
    public void Original_relative_node_bank_closes_three_sprite_directories_without_mesh_or_morph_payloads()
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        Assert.Equal(0x17B97A4u, resources.NodeSource.ResourceAddress);
        Assert.Equal(6544, resources.NodeSource.Data.Length);
        Assert.Equal(new uint[] { 0x17B98A8, 0x17B9A3C, 0x17B9BD0 }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 404, 404, 428 }, resources.Sprites.Select(sprite => sprite.Bytes.Length));
        Assert.Equal(new byte[] { 27, 27, 29 }, resources.Sprites.Select(sprite => sprite.Kind));
        for (int index = 0; index < 16; index++)
        {
            Assert.Equal(0u, Dword(resources.NodeSource.Data, 0x84 + index * 4));
            Assert.Equal(0u, Dword(resources.NodeSource.Data, 0xC4 + index * 4));
        }
        for (int index = 0; index < 3; index++)
        {
            NativeBattleModuleResource sprite = resources.Sprites[index];
            int count = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            Assert.Equal(index == 2 ? 16 : 15, count);
            for (int frame = 0; frame < count; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
        Assert.Equal(0u, Dword(resources.NodeSource.Data, 0));
    }

    [Theory]
    [InlineData(0, 340, 7)]
    [InlineData(1, 340, 7)]
    [InlineData(2, 500, 10)]
    public void Embedded_ACT_workspace_continues_the_actual_next_frame_after_JSON(int index, int workspaceBytes, int geometries)
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        NativeBattleGfAnimationSource source = resources.Acts[index];
        Assert.Same(source, resources.Act(source.Address));
        Assert.Equal((uint)geometries, Dword(source.Data, 0));
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaceBytes].ToImmutableArray()).Initialize7016B0(source, workspaceBytes);
        NativeBattleGfAnimationAdvance first = Advance(workspace);
        Assert.Equal(39, first.Remaining);
        Assert.True(first.EmittedNodes > 0);
        NativeBattleGfAnimationWorkspace restored = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(first.Workspace))!;
        Assert.Equal(JsonSerializer.Serialize(Advance(first.Workspace)), JsonSerializer.Serialize(Advance(restored)));
        for (int frame = 0; frame < 40; frame++)
        {
            NativeBattleGfAnimationAdvance next = Advance(workspace);
            Assert.Equal(39 - frame, next.Remaining);
            workspace = next.Workspace;
        }

        NativeBattleGfAnimationAdvance Advance(NativeBattleGfAnimationWorkspace current)
        {
            return current.Advance701970(source, 0, (retained, node) =>
            {
                Assert.InRange(node.GeometryIndex, 0, geometries - 1);
                return retained;
            });
        }
    }

    [Fact]
    public void Ingress_rejects_changed_upload_geometry_truncated_ACT_unbounded_channels_and_live_script_destinations()
    {
        using GameAssets game = OpenGame();
        NativeBattleCurse47Resources resources = game.ReadNativeCurse47Resources();
        byte[] executable = game.ReadNativeExecutable();
        NativeBattleEffectFileAsset original = resources.Files[2];
        NativeBattleEffectFileAsset changedUpload = original with { Data = original.Data.SetItem(56, 0) };
        Assert.Throws<InvalidDataException>(() => NativeBattleCurse47ResourcesReader.Read(executable, resources.MainTim, resources.Files.SetItem(2, changedUpload)));
        NativeBattleGfAnimationSource truncated = resources.Acts[0] with { Data = resources.Acts[0].Data.RemoveAt(resources.Acts[0].Data.Length - 1) };
        Assert.Throws<InvalidDataException>(() => (resources with { Acts = resources.Acts.SetItem(0, truncated) }).Validate());
        NativeBattleGfAnimationSource invalidVertex = resources.Acts[0] with { Data = SetDword(resources.Acts[0].Data, 40, 106) };
        Assert.Throws<InvalidDataException>(() => (resources with { Acts = resources.Acts.SetItem(0, invalidVertex) }).Validate());
        NativeBattleCuragaExecutableSource invalidChannel = resources.NodeSource with { Data = SetDword(resources.NodeSource.Data, 0x1588 + 0xC8, 6544) };
        Assert.Throws<InvalidDataException>(() => (resources with { NodeSource = invalidChannel }).Validate());
        NativeBattleEffectHeapAllocation liveScript = resources.InitialIoScript with { Data = SetDword(resources.InitialIoScript.Data, 28, 0x19EB018) };
        Assert.Throws<InvalidDataException>(() => (resources with { InitialIoScript = liveScript }).Validate());
        Assert.Equal(0u, Dword(resources.InitialIoScript.Data, 28));
    }

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Curse ingress.");
        return new(directory!);
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static ushort[] Block(ImmutableArray<byte> bytes, int offset) => Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset + 4 + index * 2))).ToArray();
    private static ImmutableArray<byte> SetDword(ImmutableArray<byte> bytes, int offset, uint value)
    {
        byte[] result = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), value);
        return result.ToImmutableArray();
    }
}
