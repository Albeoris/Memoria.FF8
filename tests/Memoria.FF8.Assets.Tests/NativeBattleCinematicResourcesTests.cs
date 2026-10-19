using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCinematicResourcesTests
{
    [Fact]
    public void Original_Ifrit_static_owner_supplies_all_twenty_three_texture_rows_and_the_full_IO_descriptor()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe"));
        NativeExecutableMemory original = new(executable);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([201])[200];
        NativeBattleEffectInitializerAsset file = binding.InitializerAssets!.Value[0];
        Assert.Equal("mag200_b.00", file.Name);
        NativeBattleCinematicResources resources = NativeBattleCinematicResourcesReader.ReadIfrit(executable, file.Data);
        resources.Validate();
        Assert.Equal(0x1874894u, resources.StaticAddress);
        Assert.Equal(0xB98, resources.StaticData.Data.Length);
        Assert.Equal(23, resources.TextureProfile.Count);
        Assert.Equal(0x1E4u, resources.InitializationOffset(0x1C));
        Assert.Equal(new byte[] { 1, 3 }, resources.ReadStatic(0x1874A70, 2).ToArray());
        for (int index = 0; index < 23; index++)
        {
            foreach (bool clut in new[] { false, true })
            {
                uint rectangleAddress = checked((clut ? 0x187496Cu : 0x18748B4u) + (uint)index * 8);
                byte[] rectangle = original.Read(rectangleAddress, 8);
                Assert.Equal(new NativeBattleEffectTextureRectangle(BinaryPrimitives.ReadInt16LittleEndian(rectangle), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(rectangle.AsSpan(6))), resources.Rectangle(index, clut));
                byte source = original.Read(checked((clut ? 0x1874A3Cu : 0x1874A24u) + (uint)index), 1)[0];
                Assert.Equal(clut ? source : (byte)(source & 0x7F), resources.Source(index, clut));
            }
        }
        Assert.Contains((byte)3, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Contains((byte)6, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Contains((byte)9, Enumerable.Range(0, 23).Select(index => resources.Source(index, false)));
        Assert.Equal((byte)127, resources.Source(0, false));
        Assert.Equal((byte)255, resources.Source(0, true));
        int vibrationStart = BinaryPrimitives.ReadInt32LittleEndian(file.Data.AsSpan().Slice(0x24));
        Assert.Equal(0x2540, vibrationStart);
        Assert.Equal(64, resources.Vibration.Offsets.Length);
        Assert.Equal(0, resources.Vibration.Offsets[0]);
        Assert.Equal(0x100, resources.Vibration.Offsets[1]);
        Assert.All(resources.Vibration.Offsets.Skip(2), offset => Assert.Equal(0, offset));
        Assert.Equal(0x2DE, resources.Vibration.Bytes.Length);
        Assert.Equal(file.Data.AsSpan().Slice(vibrationStart, resources.Vibration.Bytes.Length).ToArray(), resources.Vibration.Bytes.ToArray());
    }

    [Fact]
    public void Reader_rejects_an_unauthenticated_executable_before_reading_resource_metadata()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicResourcesReader.ReadIfrit(new byte[64], default));
    }
}
