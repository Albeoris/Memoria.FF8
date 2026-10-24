using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect166ResourceTests
{
    [Fact]
    public void Original_selected_fronts_are_complete_and_addressed_tables_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect166Resources resources = game.ReadNativeEffect166Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(SpriteLength, resources.Sprite(SpriteAddress).Bytes.Length);
        Assert.Equal(12, resources.Palette.Length);
        Assert.Equal(3, resources.MeshTable.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(192u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadMesh(0xD722E4).Bytes.AsSpan(4, 4)));
        Assert.Equal(176u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadMesh(0xD7398C).Bytes.AsSpan(4, 4)));
        foreach (uint address in resources.MeshTable)
        {
            Assert.Equal(696, resources.ReadMesh(address).Bytes.Length);
        }
        Assert.Equal(24, resources.ReadOriginalCapturePacket().Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(EnvironmentPacketAddress, 24));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(EnvironmentPacketAddress, 25));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MatrixAddress, 32));
    }

    [Fact]
    public void Original_camera_tracks_parse_to_retirement_and_resources_survive_JSON()
    {
        using GameAssets game = Open();
        NativeBattleEffect166Resources resources = game.ReadNativeEffect166Resources();
        NativeBattleEffect166Resources restored = JsonSerializer.Deserialize<NativeBattleEffect166Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.MeshTable.ToArray(), restored.MeshTable.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(32).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    [Fact]
    public void Physical_capture_packet_binds_once_and_retains_current_link_TAG_after_JSON()
    {
        using GameAssets game = Open();
        NativeBattleEffect166Resources resources = game.ReadNativeEffect166Resources();
        NativeBattleEffect166State state = NativeBattleEffect166State.FromColdProcess().BindCapturePacket(resources);
        byte[] payload = state.ReadCapturePacket().Read(4, 20);
        state = state.WriteCapturePacket(state.ReadCapturePacket().Write(0, BitConverter.GetBytes(0x05ABCDEFu)));
        state = JsonSerializer.Deserialize<NativeBattleEffect166State>(JsonSerializer.Serialize(state))!;
        state = state.BindCapturePacket(resources);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes(0x05ABCDEFu), state.ReadCapturePacket().Read(0, 4));
        Assert.Equal(payload, state.ReadCapturePacket().Read(4, 20));
        Assert.Equal(resources.ReadOriginalCapturePacket().AsSpan(4, 20).ToArray(), payload);
    }

    [Fact]
    public void Selected_catalog_hydrates_the_same_original_resource_binding()
    {
        using GameAssets game = Open();
        NativeBattleEffect166Resources resources = game.ReadNativeEffect166Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([166])[165];
        Assert.Same(resources, binding.Effect166Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag311.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect166 resources.");
        return new(directory!);
    }
}
