using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshPointResourcesTests
{
    [Fact]
    public void Original_sixteen_colors_and_real_projector_preserve_point_order_and_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshPointResources resources = game.ReadNativeGilgameshPointResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshPointResources());
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0xCD0428, 64), resources.Colors.Data.ToArray());
        uint[] colors = [0x6A404040, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A808080, 0x6A707070, 0x6A606060, 0x6A505050, 0x6A404040, 0x6A303030, 0x6A202020, 0x6A101010];
        Assert.Equal(colors, Enumerable.Range(0, 16).Select(resources.Color));
        NativeBattleGilgameshPointResources restored = RoundTrip(resources);
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        NativeBattleActionMatrix worldView = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, -50, 3000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleModuleProjectedVertex Project(NativeBattleVector point) => NativeBattleModuleProjection.Project45FAA0(worldView, point, projection, -256, 0x1000000);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 16; index++)
        {
            byte[] record = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(record, 1);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), (ushort)index);
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(8), (ushort)(index * 10));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(10), unchecked((ushort)(-index * 5)));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12), (ushort)(index * 20));
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(16), -7);
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(18), -17);
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(20), -9);
            arena = arena.Write(0x2F800 + index * 32, record);
        }
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x593E80, new byte[24].ToImmutableArray()).WriteWord(12, 41);
        NativeBattleGilgameshPointStep first = NativeBattleGilgameshPointBurst.Step593E80(task, arena, new(1), resources, 0, Project);
        Assert.Equal(16 * 12, first.Packets.AllocationBytes);
        Assert.Equal(16, first.Packets.Attempts.Length);
        for (int index = 0; index < 16; index++)
        {
            NativeBattleModuleProjectedVertex vertex = Project(new((short)(index * 10), (short)(-index * 5), (short)(index * 20)));
            NativeBattleGfGeometryAttempt attempt = first.Packets.Attempts[index];
            Assert.Equal(index * 12, attempt.ByteOffsetRelative);
            Assert.Equal(new uint[] { 0x02000000, colors[index], vertex.Screen }, attempt.Primitive!.Words.ToArray());
            Assert.Equal(17 + (vertex.Depth >> 4), attempt.Primitive.Bucket);
            Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4.ToArray());
        }
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(first.Arena.Read(0x2F800 + 15 * 32, 4)));
        NativeBattleGilgameshPointStep expected = NativeBattleGilgameshPointBurst.Step593E80(first.Task, first.Arena, first.Random, resources, 0, Project);
        NativeBattleGilgameshPointStep actual = NativeBattleGilgameshPointBurst.Step593E80(RoundTrip(first.Task), RoundTrip(first.Arena), RoundTrip(first.Random), restored, 0, Project);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors with { Data = resources.Colors.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors with { Data = resources.Colors.Data.SetItem(3, 0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => resources.Color(-1));
        Assert.Throws<InvalidDataException>(() => resources.Color(16));
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
