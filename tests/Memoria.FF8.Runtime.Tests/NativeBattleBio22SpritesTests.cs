using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22SpritesTests
{
    private static readonly NativeBattleCameraProjection Projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108);

    [Fact]
    public void Hidden_sprites_do_not_read_resource_projection_or_remaining_task_fields()
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0, new byte[0x27 - 12].ToImmutableArray()).WriteByte(0x26, 4);
        Assert.Empty(NativeBattleBio22Sprites.Emit89BD90(task, 0, null!, 0, null!).Primitives);
        Assert.Empty(NativeBattleBio22Sprites.Emit89BF80(task, null!, 0, null!).Attempts);
    }

    [Fact]
    public void Billboard_consumes_signed_depth_displacement_and_explicit_scale_argument()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x54, unchecked((ushort)-256)).WriteDword(0x30, 1);
        NativeBattleModulePacketBatch batch = NativeBattleBio22Sprites.Emit89BD90(task, 4096, Projection, 0, Resource());
        NativeBattleGeometryPrimitive packet = Assert.Single(batch.Primitives).Primitive;
        Assert.Equal(new[] { 768, 768, 768, 768 }, packet.VertexPayload4);
        Assert.Equal(65, packet.Bucket);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.NotEqual(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BD90(task, 2048, Projection, 0, Resource())));
    }

    [Fact]
    public void Planar_ignores_billboard_depth_and_preserves_packets_across_task_JSON_restore()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x40, 213).WriteWord(0x42, 417).WriteWord(0x44, 731);
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.RotateY8DD8A0(Projection.View!, 127) with { X = 11, Y = -23, Z = 512 } };
        NativeBattleGfGeometryBatch batch = NativeBattleBio22Sprites.Emit89BF80(task, projection, 319, Resource());
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleModulePoolTask restored = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BF80(restored.WriteWord(0x54, 32767), projection, 319, Resource())));
        Assert.NotEqual(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BF80(task.WriteWord(0x42, 0), projection, 319, Resource())));
        Assert.NotEqual(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleBio22Sprites.Emit89BF80(task, projection, 0, Resource())));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0, new byte[0x70 - 12].ToImmutableArray()).WriteWord(0x20, 1024).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);

    private static NativeBattleModuleResource Resource()
    {
        byte[] quad = new byte[20];
        quad[0] = 2;
        quad[1] = 128;
        quad[2] = 1;
        quad[3] = 0x2C;
        return new(0, 0, 0, 0, NativeBattleCuragaProjectedSpritesTests.Resource(false, quad));
    }
}
