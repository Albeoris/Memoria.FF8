using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect20ResourcesTests
{
    [Fact]
    public void Original_effect20_resources_keep_distinct_sprite_mesh_camera_tables_and_vibration_pattern()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check effect20.");
        using GameAssets game = new(directory!);
        NativeBattleEffect20Resources resources = game.ReadNativeEffect20Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect20Resources());
        Assert.Equal(180, resources.Animation.Data.Length);
        Assert.Equal(0x6429Bu, resources.Sound);
        Assert.Equal(0x1629E00u, resources.Mesh.Address);
        Assert.Equal(96, resources.Mesh.Bytes.Length);
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Mesh.Bytes.AsSpan().Slice(4)));
        Assert.Equal(0x1629C54u, resources.Sprite.Address);
        Assert.Equal(428, resources.Sprite.Bytes.Length);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8)));
        for (int frame = 0; frame < 16; frame++)
        {
            _ = resources.Sprite.ProjectedFrame571C80(frame);
        }
        Assert.Equal(new short[] { 0, 6, 9, 16, 26, 28, 30, 32, 34, short.MaxValue }, resources.EmissionAges);
        Assert.Equal(12, resources.EmissionOffsets.Length);
        Assert.Equal(new NativeBattleVector(0, 1536, 2816), resources.EmissionOffsets[0]);
        Assert.Equal(new uint[] { 0xC0C0C0, 0x808080, 0x404040, 0xFF000000 }, resources.Overlay.Commands);
        Assert.Equal(0x17F36A0u, resources.Vibration.Address);
        NativeVibrationBank bank = resources.Vibration.Bank;
        Assert.Equal(new[] { 0, 0x100 }, bank.Offsets);
        Assert.Equal(0x11A, bank.Bytes.Length);
        Assert.Equal((ushort)20, bank.ReadWord(0x100));
        Assert.Equal((ushort)0, bank.ReadWord(0x102));
        Assert.Equal((byte)255, bank.Bytes[0x117]);
        NativeVibrationEnqueue queued = NativeVibrationProcessState.FromColdProcess().Enqueue(bank, 1, 128);
        Assert.True(queued.Handle > 0);
        NativeVibrationSlot slot = queued.State.Slots[queued.Handle & 3];
        Assert.Equal((ushort)20, slot.A.Length);
        Assert.Equal(bank.Bytes.Skip(0x104).Take(22).ToArray(), slot.A.Bytes.ToArray());
        Assert.Equal((byte)0, slot.B.Active);
        Assert.True(queued.State.IsPlaying(queued.Handle));
        Assert.False(queued.State.Stop4A2940(queued.Handle).IsPlaying(queued.Handle));
        NativeVibrationProcessState advanced = queued.State;
        for (int callback = 0; callback < 512; callback++)
        {
            advanced = advanced.Advance(4);
        }
        Assert.False(advanced.IsPlaying(queued.Handle));
        NativeVibrationSlot finished = advanced.Slots[queued.Handle & 3];
        Assert.Equal((byte)0, finished.Priority);
        Assert.Equal(18, finished.A.Cursor);
        Assert.Equal((byte)1, finished.A.Active);
        NativeBattleEffect20Resources restored = JsonSerializer.Deserialize<NativeBattleEffect20Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([20])[19];
        Assert.Equal(0x89FBC0u, binding.Entry);
        Assert.Equal(0x89FBA0u, binding.Initializer);
        Assert.Equal("mag019.tim", binding.InitializerAsset!.Name);
        ReadOnlySpan<byte> tim = binding.InitializerAsset.Data.AsSpan();
        Assert.Equal(167968, tim.Length);
        Assert.Equal(16u, BinaryPrimitives.ReadUInt32LittleEndian(tim));
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(tim.Slice(4)));
        Assert.Equal(4108u, BinaryPrimitives.ReadUInt32LittleEndian(tim.Slice(8)));
        Assert.Equal(163852u, BinaryPrimitives.ReadUInt32LittleEndian(tim.Slice(4116)));
        Assert.Equal(4116 + 163852, tim.Length);
    }
}
