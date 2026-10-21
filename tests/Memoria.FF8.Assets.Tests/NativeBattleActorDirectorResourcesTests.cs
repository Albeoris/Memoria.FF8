using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActorDirectorResourcesTests
{
    [Theory]
    [InlineData(16, 0x4770, 0x8A0, 8, 4, 4)]
    [InlineData(61, 0x2CC0, 0x870, 6, 5, 1)]
    public void Original_profile_reads_exact_camera_sound_bank_and_all_bounded_directory_entries(ushort effect, int extent, int cameraLength, int nodes, int sprites, int meshes)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check actor director banks.");
        using GameAssets game = new(directory!);
        NativeBattleActorDirectorResources resources = game.ReadNativeActorDirectorResources(effect);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeActorDirectorResources(effect));
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
        Assert.Equal(cameraLength, resources.Animation.Data.Length);
        Assert.Equal(profile.Sound, resources.Sound);
        Assert.Equal(profile.SourceAddress, resources.NodeSource.ResourceAddress);
        Assert.Equal(extent, resources.NodeSource.Data.Length);
        Assert.Equal(0u, Read(0));
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = Read(4 + table * 64 + index * 4);
                if (pointer == 0)
                {
                    continue;
                }
                counts[table]++;
                Assert.InRange(pointer, 0x104u, (uint)extent - 1);
                if (table == 0)
                {
                    Assert.InRange(pointer + 0x158, 0x25Cu, (uint)extent);
                    for (int field = 0xC8; field <= 0x144; field += 4)
                    {
                        Assert.InRange(Read((int)pointer + field) + 80, 80u, (uint)extent);
                    }
                }
            }
        }
        Assert.Equal(new[] { nodes, sprites, meshes, 0 }, counts);

        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan().Slice(offset));
    }
}
