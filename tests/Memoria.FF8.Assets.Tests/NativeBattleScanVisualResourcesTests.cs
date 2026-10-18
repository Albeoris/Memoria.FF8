using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleScanVisualResourcesTests
{
    [Fact]
    public void Installed_visual_meshes_vertex_banks_tables_and_line_arrays_keep_exact_original_envelopes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleScanVisualResources resources = game.ReadNativeScanVisualResources();
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        Assert.Equal(16, resources.MeshesByAddress.Count);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(original.Read(0x15D0B3C, 4)), resources.Sound15D0B3C);
        foreach (var item in resources.MeshesByAddress)
        {
            Assert.Equal(item.Key, item.Value.Address);
            Assert.Equal(original.Read(item.Key, item.Value.Bytes.Length), item.Value.Bytes.ToArray());
        }
        Assert.Equal(0x3E8, resources.MeshesByAddress[0x15CDDEC].Bytes.Length);
        Assert.Equal(0x768, resources.MeshesByAddress[0x15CE1D4].Bytes.Length);
        Assert.Equal(0x88, resources.MeshesByAddress[0x15CF674].Bytes.Length);
        Assert.Equal(0x60, resources.MeshesByAddress[0x15CF614].Bytes.Length);
        foreach (var item in resources.VertexBanksByAddress)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(original.Read(item.Key + 4, 4));
            Assert.Equal(8 + count * 8, item.Value.Length);
            Assert.Equal(original.Read(item.Key, item.Value.Length), item.Value.ToArray());
        }
        Assert.Equal(4, resources.VertexBanksByAddress.Count);
        Assert.Equal(6, resources.TablesByAddress.Count);
        foreach (var item in resources.TablesByAddress)
        {
            Assert.Equal(original.Read(item.Key, item.Value.Length), item.Value.ToArray());
        }
        Assert.Equal(8, resources.LineArrays15D1718.Length);
        for (int index = 0; index < 8; index++)
        {
            var lines = resources.LineArrays15D1718[index];
            Assert.Equal(0x30, lines.Length);
            Assert.Equal(original.Read(0x15D1718u + (uint)(index * 0x30), 0x30), lines.ToArray());
            Assert.NotEqual((ushort)0xFF, BinaryPrimitives.ReadUInt16LittleEndian(lines.AsSpan()));
            Assert.NotEqual((ushort)0xFF, BinaryPrimitives.ReadUInt16LittleEndian(lines.AsSpan().Slice(16)));
            Assert.Equal((ushort)0xFF, BinaryPrimitives.ReadUInt16LittleEndian(lines.AsSpan().Slice(32)));
        }
    }

    [Fact]
    public void Installed_opening_sprites_retain_actual_resources_and_reached_frame_bounds()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleScanVisualResources resources = game.ReadNativeScanVisualResources();
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        Assert.Equal(0x15D07D4u, resources.Sprite15D07D4.Address);
        Assert.Equal(0x15D0AF8u, resources.Sprite15D0AF8.Address);
        Assert.Equal(original.Read(0x15D07D4, resources.Sprite15D07D4.Bytes.Length), resources.Sprite15D07D4.Bytes.ToArray());
        Assert.Equal(original.Read(0x15D0AF8, resources.Sprite15D0AF8.Bytes.Length), resources.Sprite15D0AF8.Bytes.ToArray());
        for (int phase = 0; phase <= 31; phase++)
        {
            Assert.NotNull(resources.Sprite15D07D4.ProjectedFrame571C80(phase));
        }
        for (int phase = 0; phase <= 3; phase++)
        {
            Assert.NotNull(resources.Sprite15D0AF8.ProjectedFrame571C80(phase));
        }
    }

    [Fact]
    public void Installed_reticle_trajectory_preserves_all_twenty_one_signed_records_and_terminal_record()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleScanVisualResources resources = game.ReadNativeScanVisualResources();
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        byte[] bytes = original.Read(0x15D1898, 0x54);
        Assert.Equal(21, resources.Trajectory15D1898.Length);
        for (int index = 0; index < 21; index++)
        {
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4)), resources.Trajectory15D1898[index].X);
            Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4 + 2)), resources.Trajectory15D1898[index].Y);
        }
        Assert.Equal(new NativeBattleScanReticlePoint(short.MaxValue, 0), resources.Trajectory15D1898[20]);
        Assert.Same(resources, game.ReadNativeScanVisualResources());
    }
}
