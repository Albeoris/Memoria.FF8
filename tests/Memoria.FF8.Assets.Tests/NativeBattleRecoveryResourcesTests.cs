using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRecoveryResourcesTests
{
    [Theory]
    [InlineData((ushort)53, 0x1B38, 6, 3, 300170u, 8)]
    [InlineData((ushort)58, 0x35A8, 10, 5, 300182u, 17)]
    [InlineData((ushort)59, 0x35A8, 10, 5, 300182u, 17)]
    public void Original_directories_keep_absolute_pointers_real_kind6_sprites_and_full_mesh_envelopes(ushort effect, int length, int sources, int meshes, uint sound, int firstSpriteFrames)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe"));
        NativeBattleRecoveryResources resources = game.ReadNativeRecoveryResources(effect);
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(effect);
        NativeBattleEffectBinding binding = NativeBattleEffectCatalogReader.Read(executable)[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource(profile.TextureName, profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(sound, resources.Sound);
        Assert.Equal(length, resources.Executable.Data.Length);
        Assert.Same(resources, game.ReadNativeRecoveryResources(effect));
        NativeBattleRecoveryExecutableState current = new(effect, resources.Executable.Data);
        Assert.Equal(1u, current.InitializationFlag);
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                uint pointer = U32(profile.DirectoryAddress + 4u + (uint)(table * 64 + index * 4));
                if (pointer != 0)
                {
                    counts[table]++;
                    Assert.InRange(pointer, profile.ExecutableAddress, profile.ExecutableAddress + (uint)profile.ExecutableLength - 1);
                }
            }
        }
        Assert.Equal(new[] { sources, 2, meshes, effect == 53 ? 3 : 2 }, counts);
        uint[] spritePointers = Enumerable.Range(0, 16).Select(index => U32(profile.DirectoryAddress + 0x44u + (uint)index * 4)).Where(pointer => pointer != 0).ToArray();
        Assert.Equal(2, spritePointers.Length);
        List<ushort> frameCounts = [];
        foreach (uint spritePointer in spritePointers)
        {
            ImmutableArray<byte> sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, spritePointer);
            Assert.Equal((byte)6, sprite[3]);
            frameCounts.Add(BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan().Slice(8)));
            Assert.Equal(sprite.ToArray(), current.Read(spritePointer, sprite.Length));
        }
        Assert.Equal(new ushort[] { checked((ushort)firstSpriteFrames), effect == 53 ? (ushort)10 : (ushort)11 }, frameCounts);
        uint[] meshPointers = Enumerable.Range(0, 16).Select(index => U32(profile.DirectoryAddress + 0x84u + (uint)index * 4)).Where(pointer => pointer != 0).ToArray();
        Assert.Equal(meshes, meshPointers.Length);
        foreach (uint pointer in meshPointers)
        {
            NativeBattleStaticMeshResource mesh = NativeBattleEffectStaticResourcesReader.ReadMesh(executable, pointer);
            Assert.Equal(mesh.Bytes.ToArray(), current.Read(pointer, mesh.Bytes.Length));
            NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
            NativeBattleCameraProjection projection = new(view, 256, 160, 120);
            NativeBattleGfGeometryBatch packets = NativeBattleRecoveryMeshPackets.EmitBatch8131A0(mesh.Bytes, view, projection, new(0x30, null, null, null, null, null, 17, 2));
            int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
            int faces = 0;
            foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
            {
                int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor)));
                faces += count;
                cursor += 4 + count * stride;
            }
            Assert.Equal(faces, packets.Attempts.Length);
        }
        Assert.Same(current, current.Initialize(resources.Executable));
        Assert.Null(current.Channels);

        uint U32(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(current.Read(pointer, 4));
    }
}
