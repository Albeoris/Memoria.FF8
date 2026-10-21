using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMightyGuardResourcesTests
{
    [Theory]
    [InlineData(78, 188, 0x176E194u, 0x642B3u)]
    [InlineData(73, 344, 0x178557Cu, 0x557B4u)]
    public void Original_resources_close_all_typed_channels_sprite_frames_and_mesh_groups(ushort effect, int cameraLength, uint sourceAddress, uint sound)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to validate Mighty Guard.");
        using GameAssets game = new(directory!);
        NativeBattleMightyGuardResources resources = game.ReadNativeMightyGuardResources(effect);
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMightyGuardResources(effect));
        Assert.Equal(cameraLength, resources.Animation.Data.Length);
        Assert.Equal(sound, resources.Sound);
        Assert.Equal(sourceAddress, resources.NodeSource.ResourceAddress);
        Assert.Equal(0x3DC8, resources.NodeSource.Data.Length);
        Assert.Equal("974F57752BFC73397618243E15EDD3BD5FFDBBA27AED776CA1E3B9C5D7BA0ACB", Convert.ToHexString(SHA256.HashData(resources.NodeSource.Data.AsSpan())));
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint relative = Read(4 + table * 64 + index * 4);
                if (relative == 0)
                {
                    continue;
                }
                counts[table]++;
                Assert.InRange(relative, 0x104u, 0x3DC7u);
                int offset = checked((int)relative);
                if (table == 0)
                {
                    Assert.InRange(index, 3, 8);
                    Assert.InRange(offset + 0x158, 0x25C, 0x3DC8);
                    Assert.Contains(resources.NodeSource.Data[offset + 0x16], new byte[] { 0, 4 });
                    Assert.Equal((byte)0, resources.NodeSource.Data[offset + 0x21]);
                    for (int field = 0xC8; field < 0x148; field += 4)
                    {
                        Assert.InRange(Read(offset + field) + 80, 80u, 0x3DC8u);
                    }
                }
                else if (table == 1)
                {
                    Assert.Equal(3, index);
                    Assert.Equal((ushort)19, BinaryPrimitives.ReadUInt16LittleEndian(resources.NodeSource.Data.AsSpan().Slice(offset + 8)));
                    for (int frame = 0; frame < 19; frame++)
                    {
                        int at = offset + BinaryPrimitives.ReadUInt16LittleEndian(resources.NodeSource.Data.AsSpan().Slice(offset + 10 + frame * 2));
                        uint faces = Read(at) & 0x7FFFFFFF;
                        Assert.InRange(checked(at + 4 + (int)faces * 20), 0, 0x3DC8);
                    }
                }
                else if (table == 2)
                {
                    int cursor = checked((int)Read(offset));
                    Assert.Equal(8u + Read(offset + 4) * 8, (uint)cursor);
                    int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                    int[] groups = new int[8];
                    for (int group = 0; group < strides.Length; group++)
                    {
                        groups[group] = checked((int)Read(offset + cursor));
                        cursor = checked(cursor + 4 + groups[group] * strides[group]);
                    }
                    Assert.InRange(offset + cursor, offset + 8, 0x3DC8);
                    Assert.Equal(index switch { 0 => new[] { 0, 0, 0, 0, 0, 0, 0, 64 }, 1 => [0, 0, 0, 0, 4, 12, 0, 0], _ => [0, 0, 0, 4, 0, 0, 0, 0] }, groups);
                }
                else
                {
                    Assert.Fail("Mighty Guard has no morph directory.");
                }
            }
        }
        Assert.Equal(new[] { 6, 1, 4, 0 }, counts);
        NativeBattleMightyGuardState fixedBank = NativeBattleMightyGuardState.FromColdProcess(effect).InitializeSource(resources);
        Assert.Equal(1u, fixedBank.InitializationFlag);
        Assert.Equal(sourceAddress + Read(4 + 3 * 4), BinaryPrimitives.ReadUInt32LittleEndian(fixedBank.Executable!.Value.AsSpan().Slice(4 + 3 * 4)));
        if (effect == 73)
        {
            Assert.Equal(0x1B74, resources.CasterDat!.Value.Length);
        }
        else
        {
            Assert.Null(resources.CasterDat);
        }
        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(resources.NodeSource.Data.AsSpan().Slice(offset));
    }
}
