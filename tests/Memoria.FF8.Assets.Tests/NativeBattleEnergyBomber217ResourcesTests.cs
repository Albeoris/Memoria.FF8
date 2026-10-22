using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEnergyBomber217ResourcesTests
{
    [Fact]
    public void Original_banks_keep_independent_workspaces_and_bounded_interpolated_shapes()
    {
        using GameAssets game = Open();
        NativeBattleEnergyBomber217Resources resources = game.ReadNativeEnergyBomber217Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEnergyBomber217Resources());
        Assert.Equal(0x4900, resources.MainAct.Data.Length);
        Assert.Equal(0x1BC0, resources.ImpactAct.Data.Length);
        Assert.Equal("FF8_EN:11355D4", resources.Camera.Identity);
        Assert.Equal(0x32C, resources.Camera.Data.Length);
        Assert.Equal(0x642C8u, resources.Sound);
        AssertShapes(resources.MainAct, [120, 25, 114, 25, 25, 25, 120], [2, 1, 1, 1, 1, 1, 2]);
        AssertShapes(resources.ImpactAct, [64, 25, 25, 25], [2, 1, 1, 1]);
        Assert.Equal(120 * 8, NativeBattleEnergyBomber217Profile.MorphBytes);
        AssertWorkspace(resources.MainAct, 0x398, 100);
        AssertWorkspace(resources.ImpactAct, 0x2A8, 23);
        ReadOnlySpan<byte> mesh = resources.TrailMesh.Bytes.AsSpan();
        Assert.Equal(25, BinaryPrimitives.ReadInt32LittleEndian(mesh.Slice(4)));
        int faces = BinaryPrimitives.ReadInt32LittleEndian(mesh);
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Slice(faces));
            Assert.Equal(group == 3 ? 16 : 0, count);
            faces += 4 + count * 24;
        }
        Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8)));
    }

    [Fact]
    public void Original_resource_JSON_preserves_strict_address_lookup_and_complete_extents()
    {
        using GameAssets game = Open();
        NativeBattleEnergyBomber217Resources source = game.ReadNativeEnergyBomber217Resources();
        NativeBattleEnergyBomber217Resources restored = JsonSerializer.Deserialize<NativeBattleEnergyBomber217Resources>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MainAct, restored.Act(NativeBattleEnergyBomber217Profile.MainActAddress));
        Assert.Same(restored.ImpactAct, restored.Act(NativeBattleEnergyBomber217Profile.ImpactActAddress));
        Assert.Throws<NotSupportedException>(() => restored.Act(NativeBattleEnergyBomber217Profile.CameraAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { ImpactAct = restored.ImpactAct with { Data = restored.ImpactAct.Data.Take(restored.ImpactAct.Data.Length - 4).ToImmutableArray() } }).Validate());
    }

    private static void AssertShapes(NativeBattleGfAnimationSource source, int[] vertices, int[] variants)
    {
        ReadOnlySpan<byte> bytes = source.Data.AsSpan();
        Assert.Equal(vertices.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            Assert.Equal(vertices[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)));
            Assert.Equal(variants[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)));
        }
    }

    private static void AssertWorkspace(NativeBattleGfAnimationSource source, int size, int duration)
    {
        NativeBattleGfAnimationWorkspace workspace = new(new byte[size].ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, size);
        int emitted = 0;
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) => current);
            emitted += step.EmittedNodes;
            workspace = step.Workspace;
        }
        Assert.True(emitted > 0);
        Assert.Equal(duration, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(source, 0, (current, node) => current).EmittedNodes);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Energy Bomber resources.");
        return new(directory!);
    }
}
