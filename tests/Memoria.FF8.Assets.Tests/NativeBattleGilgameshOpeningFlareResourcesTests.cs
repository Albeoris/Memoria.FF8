using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshOpeningFlareResourcesTests
{
    [Fact]
    public void Original_opening_flare_preserves_six_frames_eight_rows_and_actual_cold_helper_matrix()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshOpeningFlareResources resources = game.ReadNativeGilgameshOpeningFlareResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshOpeningFlareResources());
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0xCC6B64, 0xA8), resources.Sprite.Bytes.ToArray());
        Assert.Equal(0xCC6B64u, resources.Sprite.Address);
        Assert.Equal(new short[] { 0, 1, 2, 5, 4, 3, 1, 2 }, resources.Rows.Select(row => row.Frame));
        Assert.Equal(new short[] { 2560, 1792, 1280, 768, 512, -512, -1280, -1792 }, resources.Rows.Select(row => row.Weight));
        byte[] rowBytes = new byte[64];
        for (int index = 0; index < 8; index++)
        {
            NativeBattleGilgameshOpeningFlareRow row = resources.Rows[index];
            BinaryPrimitives.WriteInt16LittleEndian(rowBytes.AsSpan(index * 8), row.Frame);
            BinaryPrimitives.WriteInt16LittleEndian(rowBytes.AsSpan(index * 8 + 2), row.Size);
            BinaryPrimitives.WriteInt16LittleEndian(rowBytes.AsSpan(index * 8 + 4), row.Weight);
            BinaryPrimitives.WriteInt16LittleEndian(rowBytes.AsSpan(index * 8 + 6), row.Padding);
        }
        Assert.Equal(memory.Read(0xCD03A8, 64), rowBytes);
        Assert.Equal(memory.ReadLoader(0x2201060, 32), NativeBattleGilgameshOpeningFlareState.FromColdProcess().ScreenMatrix.ToArray());
        NativeBattleGilgameshOpeningFlareResources restoredResources = RoundTrip(resources);
        restoredResources.Validate();
        NativeBattleGilgameshOpeningFlareState imported = new(NativeBattleGilgameshOpeningFlareState.FromColdProcess().ScreenMatrix.SetItem(2, 5).SetItem(18, 0xA5));
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleEffectTaskPool pool = NativeBattleGilgameshOpeningFlare.Initialize5931E0(NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100), view);
        NativeBattleModulePoolTask task = pool.Slots[0];
        NativeBattleGilgameshOpeningFlareStep paused = NativeBattleGilgameshOpeningFlare.Step593250(task, imported, resources, new(1000, 2000, -3000), view, projection, -256, 0x1000000, 0x201);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(8, paused.Packets.Length);
        Assert.Equal((byte)5, paused.State.ScreenMatrix[2]);
        Assert.Equal((byte)0xA5, paused.State.ScreenMatrix[18]);
        foreach (NativeBattleAllocatedPrimitive allocation in paused.Packets.SelectMany(batch => batch.Primitives).Where(allocation => allocation.Primitive.Words[1] >> 24 == 0xE1))
        {
            Assert.Equal(new int[] { 0, 0, 0, 0 }, allocation.Primitive.VertexPayload4.ToArray());
        }
        NativeBattleGilgameshOpeningFlareStep first = NativeBattleGilgameshOpeningFlare.Step593250(task, imported, resources, new(1000, 2000, -3000), view, projection, -256, 0x1000000, 0);
        NativeBattleGilgameshOpeningFlareStep expected = NativeBattleGilgameshOpeningFlare.Step593250(first.UpdatedTask, first.State, resources, new(1000, 2000, -3000), view, projection, -256, 0x1000000, 0);
        NativeBattleGilgameshOpeningFlareStep actual = NativeBattleGilgameshOpeningFlare.Step593250(RoundTrip(first.UpdatedTask), RoundTrip(first.State), restoredResources, new(1000, 2000, -3000), view, projection, -256, 0x1000000, 0);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Throws<InvalidDataException>(() => (resources with { Rows = resources.Rows.RemoveAt(7) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Rows = resources.Rows.SetItem(5, resources.Rows[5] with { Weight = 512 }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
