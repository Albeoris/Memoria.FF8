using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22SpritePacketTests
{
    [Fact]
    public void Every_original_particle_frame_runs_billboard_and_planar_producers_after_independent_resource_restore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Resources restored = JsonSerializer.Deserialize<NativeBattleBio22Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleCameraProjection projection = new(NativeBattleGfMatrices.Euler56CD50(new(512, 137, 0)) with { Z = 4096 }, 256, 160, 108);
        int frames = 0;
        foreach (NativeBattleModuleResource resource in resources.Sprites)
        {
            int count = BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8));
            for (int index = 0; index < count; index++)
            {
                NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0, new byte[0x70 - 12].ToImmutableArray()).WriteDword(0x4C, resource.Address).WriteWord(0x50, (ushort)index).WriteWord(0x40, 1024).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
                NativeBattleModulePacketBatch billboard = NativeBattleBio22Sprites.Emit89BD90(task, 4096, projection, 173, resource);
                NativeBattleGfGeometryBatch planar = NativeBattleBio22Sprites.Emit89BF80(task, projection, 173, resource);
                Assert.True(billboard.AllocationBytes > 0);
                Assert.True(planar.AllocationBytes > 0);
                NativeBattleModuleResource reloaded = restored.Sprite(resource.Address);
                Assert.Equal(JsonSerializer.Serialize(billboard), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BD90(task, 4096, projection, 173, reloaded)));
                Assert.Equal(JsonSerializer.Serialize(planar), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BF80(task, projection, 173, reloaded)));
                frames++;
            }
        }
        Assert.Equal(99, frames);
    }
}
