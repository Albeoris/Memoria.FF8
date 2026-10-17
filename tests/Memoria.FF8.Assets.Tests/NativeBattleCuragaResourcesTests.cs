using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuragaResourcesTests
{
    [Fact]
    public void Original_Curaga_resources_keep_actual_animation_sound_flash_program_and_mutable_directory_extent()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCuragaResources resources = game.ReadNativeCuragaResources();
        Assert.Equal(2976, resources.Animation.Data.Length);
        Assert.Equal(0x4946Fu, resources.Sound);
        Assert.Equal(new uint[] { 0x007F7F7F, 0x003F3F3F, 0xFF000000 }, resources.FlashProgram);
        Assert.Equal(NativeBattleCuragaExecutableState.Length, resources.Executable!.Data.Length);
        NativeBattleCuragaExecutableState initialized = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(resources.Executable);
        for (int index = 0; index < 16; index++)
        {
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(4 + 4 * index));
            Assert.Equal(0x6C34u + (uint)index * 0x158u, source);
            uint target = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(NativeBattleCuragaExecutableState.Address + 4u + 4u * (uint)index, 4));
            Assert.Equal(NativeBattleCuragaExecutableState.Address + source, target);
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint original = BinaryPrimitives.ReadUInt32LittleEndian(resources.Executable.Data.AsSpan().Slice(checked((int)source + field)));
                uint actual = BinaryPrimitives.ReadUInt32LittleEndian(initialized.Read(target + (uint)field, 4));
                Assert.Equal(unchecked(original + NativeBattleCuragaExecutableState.Address), actual);
            }
        }
    }
}
